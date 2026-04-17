using AutomationEngine.Application.DTOs;
using AutomationEngine.Application.Options;
using AutomationEngine.Domain.Entities;
using AutomationEngine.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

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
            // .docx files are binary (Office Open XML ZIP). Read raw bytes and
            // extract the plain text before passing to Gemini.
            string rawContent;
            var objectNameLower = storageEvent.Name.ToLowerInvariant();
            var contentTypeLower = (storageEvent.ContentType ?? string.Empty).ToLowerInvariant();
            bool isDocx = objectNameLower.EndsWith(".docx") ||
                          contentTypeLower.Contains("wordprocessingml") ||
                          contentTypeLower.Contains("vnd.openxmlformats-officedocument");

            if (isDocx)
            {
                _logger.LogInformation("Detected .docx file — extracting plain text. Object={Object}", storageEvent.Name);
                var inputDocxBytes = await _storage.ReadFileBytesAsync(storageEvent.Bucket, storageEvent.Name, ct);
                rawContent = _documentSerializer.ExtractTextFromDocx(inputDocxBytes);
                _logger.LogInformation("Extracted {Chars} chars of plain text from .docx", rawContent.Length);
            }
            else
            {
                rawContent = await _storage.ReadFileContentAsync(storageEvent.Bucket, storageEvent.Name, ct);
            }

            var designDoc = DesignDocument.Create(storageEvent.Bucket, storageEvent.Name, rawContent);
            context.SetDesignDocument(designDoc);

            _logger.LogInformation("Design document loaded. Version={Version}, Length={Length}",
                designDoc.Version, designDoc.Content.Length);

            // ── Step 2: Vertex AI (Gemini) → Generate Functional Specification ─
            _logger.LogInformation("Invoking Vertex AI (Gemini) to generate Functional Specification...");
            var specContent = await _aiService.GenerateFunctionalSpecAsync(designDoc.Content, ct);

            // ── Resolve versioned output path for the functional spec ─────────
            // Output folder: {solutionName}/functional/
            // Base name:     functional_{uploadedFileStem}.docx
            // e.g. "MyProject/design.docx" → "MyProject/functional/functional_design.docx"
            // Versioning:    "MyProject/functional/functional_design-001.docx", -002, …
            var solutionName  = ExtractSolutionName(storageEvent.Name);
            var baseFileName  = FunctionalSpec.DeriveBaseFileName(storageEvent.Name);
            var outputFolder  = $"{solutionName}/functional";
            var basePath      = $"{outputFolder}/{baseFileName}";
            var outputPrefix  = $"{outputFolder}/";

            var existingOutputs = await _storage.ListObjectNamesAsync(storageEvent.Bucket, outputPrefix, ct);
            var existingSet     = new HashSet<string>(existingOutputs, StringComparer.OrdinalIgnoreCase);

            string resolvedOutputPath;
            if (!existingSet.Contains(basePath))
            {
                resolvedOutputPath = basePath;
            }
            else
            {
                var stem = Path.GetFileNameWithoutExtension(baseFileName); // e.g. "functional_design"
                int version = 1;
                string candidate;
                do
                {
                    candidate = $"{outputFolder}/{stem}-{version:D3}.docx";
                    version++;
                }
                while (existingSet.Contains(candidate) && version <= 999);
                resolvedOutputPath = candidate;
            }

            _logger.LogInformation("Resolved output path for functional spec. Path={Path}", resolvedOutputPath);

            var functionalSpec = FunctionalSpec.Create(specContent, designDoc, resolvedOutputPath);
            context.SetFunctionalSpec(functionalSpec);

            _logger.LogInformation("Functional spec generated. OutputPath={Path}", functionalSpec.OutputPath);

            // ── Step 3: Save functional spec as .docx to Cloud Storage ──────────
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the solution name (first path segment) from the GCS object name.
    /// "MyProject/design.docx" → "MyProject"
    /// "design.docx"           → "documents"  (fallback)
    /// </summary>
    private static string ExtractSolutionName(string objectName)
    {
        var slash = objectName.IndexOf('/');
        return slash > 0 ? objectName[..slash] : "documents";
    }

    /// <summary>
    /// Extracts section headings from the design document plain text.
    /// Detects Markdown headings (# ## ###) and common title-case/ALL-CAPS lines.
    /// </summary>
    private static IReadOnlyList<string> ExtractSectionHeadings(string content)
    {
        var headings = new List<string>();
        var seen     = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            string? heading = null;

            // Markdown headings: # Title / ## Title / ### Title
            var mdMatch = Regex.Match(line, @"^#{1,3}\s+(.+)$");
            if (mdMatch.Success)
            {
                heading = mdMatch.Groups[1].Value.Trim().TrimEnd('#').Trim();
            }
            // Short ALL-CAPS or Title-Case lines (3–80 chars, no sentence punctuation)
            else if (line.Length is >= 3 and <= 80
                     && !line.EndsWith('.') && !line.EndsWith(',')
                     && (line == line.ToUpper() || Regex.IsMatch(line, @"^[A-Z][a-zA-Z\s\-&/()]+$")))
            {
                heading = line;
            }

            if (heading != null && seen.Add(heading))
                headings.Add(heading);

            // Cap at 20 sections — anything beyond that is noise
            if (headings.Count >= 20) break;
        }

        return headings;
    }
}
