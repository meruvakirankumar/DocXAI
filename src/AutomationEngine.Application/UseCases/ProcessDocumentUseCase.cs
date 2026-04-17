using AutomationEngine.Application.DTOs;
using AutomationEngine.Application.Options;
using AutomationEngine.Domain.Entities;
using AutomationEngine.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutomationEngine.Application.UseCases;

public sealed class ProcessDocumentUseCase : IProcessDocumentUseCase
{
    private readonly IStorageRepository _storage;
    private readonly IAIGenerationService _aiService;
    private readonly IBuildService _buildService;
    private readonly IDocumentSerializer _documentSerializer;
    private readonly ILogger<ProcessDocumentUseCase> _logger;
    private readonly ProcessDocumentOptions _options;

    public ProcessDocumentUseCase(
        IStorageRepository storage,
        IAIGenerationService aiService,
        IBuildService buildService,
        IDocumentSerializer documentSerializer,
        ILogger<ProcessDocumentUseCase> logger,
        IOptions<ProcessDocumentOptions> options)
    {
        _storage = storage;
        _aiService = aiService;
        _buildService = buildService;
        _documentSerializer = documentSerializer;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<GenerationResultDto> ExecuteAsync(StorageEventDto storageEvent, CancellationToken ct = default)
    {
        var context = ProcessingContext.Start(storageEvent.Bucket, storageEvent.Name);

        _logger.LogInformation(
            "Starting document processing pipeline. Bucket={Bucket}, Object={Object}, CorrelationId={CorrelationId}",
            storageEvent.Bucket, storageEvent.Name, context.CorrelationId);

        try
        {
            // ── Step 1: Read design document from Cloud Storage ──────────────
            var rawContent = await _storage.ReadFileContentAsync(storageEvent.Bucket, storageEvent.Name, ct);
            var designDoc = DesignDocument.Create(storageEvent.Bucket, storageEvent.Name, rawContent);
            context.SetDesignDocument(designDoc);

            _logger.LogInformation("Design document loaded. Version={Version}, Length={Length}",
                designDoc.Version, designDoc.Content.Length);

            // ── Step 2: Vertex AI (Gemini) → Generate Functional Specification ─
            _logger.LogInformation("Invoking Vertex AI (Gemini) to generate Functional Specification...");
            var specContent = await _aiService.GenerateFunctionalSpecAsync(designDoc.Content, ct);
            var functionalSpec = FunctionalSpec.Create(specContent, designDoc, _options.OutputFolder);
            context.SetFunctionalSpec(functionalSpec);

            _logger.LogInformation("Functional spec generated. OutputPath={Path}", functionalSpec.OutputPath);

            // ── Step 3: Save functional spec as .docx to Cloud Storage ────────
            var docxBytes = await Task.Run(() => _documentSerializer.SerializeToDocx(functionalSpec.Content), ct);
            await _storage.SaveFileBytesAsync(
                functionalSpec.BucketName,
                functionalSpec.OutputPath,
                docxBytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ct);

            _logger.LogInformation("Functional spec saved as .docx. Path={Path}", functionalSpec.OutputPath);

            // ── Step 4: Vertex AI (Gemini) → Generate Playwright Test Script ──
            _logger.LogInformation("Invoking Vertex AI (Gemini) to generate Playwright test script...");
            var testContent = await _aiService.GeneratePlaywrightTestsAsync(functionalSpec.Content, ct);
            var testScript = TestScript.Create(testContent, functionalSpec, _options.OutputFolder);
            context.SetTestScript(testScript);

            _logger.LogInformation("Test script generated. StoragePath={Path}", testScript.StoragePath);

            // ── Step 5: Save Playwright test script to Cloud Storage ──────────
            await _storage.SaveFileAsync(testScript.BucketName, testScript.StoragePath, testScript.Content, "text/plain", ct);
            _logger.LogInformation("Playwright test script saved to Cloud Storage. Path={Path}", testScript.StoragePath);

            // ── Step 6: Trigger Cloud Build to execute the test script ────────
            _logger.LogInformation("Triggering Cloud Build job for test execution...");

            BuildJob? buildJob = null;
            string? buildWarning = null;

            try
            {
                buildJob = await _buildService.TriggerTestExecutionAsync(
                    _options.ProjectId,
                    testScript.StoragePath,
                    ct);
                context.SetBuildJob(buildJob);

                _logger.LogInformation(
                    "Cloud Build triggered. JobId={JobId}, Status={Status}, LogUrl={LogUrl}",
                    buildJob.JobId, buildJob.Status, buildJob.LogUrl);
            }
            catch (Exception buildEx)
            {
                // Cloud Build failure is non-fatal — the functional spec was already generated.
                // Surface a warning so the caller knows Cloud Build was skipped.
                buildWarning = $"Cloud Build step skipped: {buildEx.Message}";
                _logger.LogWarning(buildEx,
                    "Cloud Build trigger failed (non-fatal). Pipeline will still return the functional spec. Reason={Reason}",
                    buildEx.Message);
            }

            context.MarkCompleted();

            _logger.LogInformation(
                "Pipeline completed successfully. CorrelationId={CorrelationId}", context.CorrelationId);

            return new GenerationResultDto(
                Success: true,
                CorrelationId: context.CorrelationId,
                FunctionalSpecPath: functionalSpec.OutputPath,
                TestScriptPath: testScript.StoragePath,
                BuildJobId: buildJob?.JobId,
                BuildLogUrl: buildJob?.LogUrl,
                ErrorMessage: null,
                FunctionalSpecContent: functionalSpec.Content,
                BuildWarning: buildWarning);
        }
        catch (Exception ex)
        {
            context.MarkFailed(ex.Message);

            _logger.LogError(ex,
                "Pipeline failed. Object={Object}, CorrelationId={CorrelationId}, Status={Status}",
                storageEvent.Name, context.CorrelationId, context.Status);

            return new GenerationResultDto(
                Success: false,
                CorrelationId: context.CorrelationId,
                FunctionalSpecPath: null,
                TestScriptPath: null,
                BuildJobId: null,
                BuildLogUrl: null,
                ErrorMessage: ex.Message);
        }
    }
}
