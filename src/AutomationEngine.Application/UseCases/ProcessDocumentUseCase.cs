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
            var outputFolder  = $"{solutionName}/functional";
            var outputPrefix  = $"{outputFolder}/";

            var existingOutputs = await _storage.ListObjectNamesAsync(storageEvent.Bucket, outputPrefix, ct);
            var existingSet     = new HashSet<string>(existingOutputs, StringComparer.OrdinalIgnoreCase);

            // Naming pattern: {SolutionName}_functional_v1.docx, v2, …
            int specVersion = 1;
            string resolvedOutputPath;
            do
            {
                resolvedOutputPath = $"{outputFolder}/{solutionName}_functional_v{specVersion}.docx";
                specVersion++;
            }
            while (existingSet.Contains(resolvedOutputPath) && specVersion <= 9999);

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

            context.MarkCompleted();

            _logger.LogInformation(
                "Pipeline completed successfully. CorrelationId={CorrelationId}", context.CorrelationId);

            return new GenerationResultDto(
                Success: true,
                CorrelationId: context.CorrelationId,
                FunctionalSpecPath: functionalSpec.OutputPath,
                TestScriptPath: null,
                BuildJobId: null,
                BuildLogUrl: null,
                ErrorMessage: null,
                FunctionalSpecContent: functionalSpec.Content);
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

    // ── GenerateTestsAsync ────────────────────────────────────────────────────

    public async Task<GenerationResultDto> GenerateTestsAsync(
        string functionalSpecContent,
        string functionalSpecPath,
        CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid().ToString();

        _logger.LogInformation(
            "Starting test generation. SpecPath={Path}, CorrelationId={CorrelationId}",
            functionalSpecPath, correlationId);

        try
        {
            var bucket = _options.UploadBucket;

            // ── Resolve versioned output path for the test script ─────────────
            // Folder:    {solutionName}/testcases/
            // Base name: testcases_{stem}.spec.ts
            // e.g. "MyProject/functional/functional_design.docx"
            //      → "MyProject/testcases/testcases_design.spec.ts"
            // Versioning: -001, -002, … applied when the base name already exists.
            var solutionName   = ExtractSolutionName(functionalSpecPath);
            var testFolder     = $"{solutionName}/test cases";
            var testPrefix     = $"{testFolder}/";

            var existingTests = await _storage.ListObjectNamesAsync(bucket, testPrefix, ct);
            var existingSet   = new HashSet<string>(existingTests, StringComparer.OrdinalIgnoreCase);

            // Naming pattern: {SolutionName}_testcases_v1.spec.ts, v2, …
            int tcVersion = 1;
            string resolvedTestPath;
            do
            {
                resolvedTestPath = $"{testFolder}/{solutionName}_testcases_v{tcVersion}.spec.ts";
                tcVersion++;
            }
            while (existingSet.Contains(resolvedTestPath) && tcVersion <= 9999);

            _logger.LogInformation("Resolved output path for test script. Path={Path}", resolvedTestPath);

            // ── Step 1: Vertex AI (Gemini) → Generate Playwright Test Script ──
            _logger.LogInformation("Invoking Vertex AI (Gemini) to generate Playwright test script...");
            var testContent = await _aiService.GeneratePlaywrightTestsAsync(functionalSpecContent, ct);
            var testScript  = TestScript.Create(testContent, bucket, resolvedTestPath);

            _logger.LogInformation("Test script generated. StoragePath={Path}", testScript.StoragePath);

            // ── Step 2: Save Playwright test script to Cloud Storage ──────────
            await _storage.SaveFileAsync(
                testScript.BucketName, testScript.StoragePath, testScript.Content, "text/plain", ct);

            _logger.LogInformation("Playwright test script saved to Cloud Storage. Path={Path}", testScript.StoragePath);

            // ── Step 3: Trigger Cloud Build ───────────────────────────────────
            BuildJob? buildJob    = null;
            string?  buildWarning = null;

            try
            {
                buildJob = await _buildService.TriggerTestExecutionAsync(
                    _options.ProjectId, testScript.StoragePath, ct);

                _logger.LogInformation(
                    "Cloud Build triggered. JobId={JobId}, Status={Status}, LogUrl={LogUrl}",
                    buildJob.JobId, buildJob.Status, buildJob.LogUrl);
            }
            catch (Exception buildEx)
            {
                buildWarning = $"Cloud Build step skipped: {buildEx.Message}";
                _logger.LogWarning(buildEx,
                    "Cloud Build trigger failed (non-fatal). Reason={Reason}", buildEx.Message);
            }

            return new GenerationResultDto(
                Success:             true,
                CorrelationId:       correlationId,
                FunctionalSpecPath:  functionalSpecPath,
                TestScriptPath:      testScript.StoragePath,
                BuildJobId:          buildJob?.JobId,
                BuildLogUrl:         buildJob?.LogUrl,
                ErrorMessage:        null,
                BuildWarning:        buildWarning,
                TestScriptContent:   testScript.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Test generation failed. SpecPath={Path}, CorrelationId={CorrelationId}",
                functionalSpecPath, correlationId);

            return new GenerationResultDto(
                Success:            false,
                CorrelationId:      correlationId,
                FunctionalSpecPath: functionalSpecPath,
                TestScriptPath:     null,
                BuildJobId:         null,
                BuildLogUrl:        null,
                ErrorMessage:       ex.Message);
        }
    }

    // ── GenerateTestSuiteAsync ────────────────────────────────────────────────

    public async Task<GenerationResultDto> GenerateTestSuiteAsync(
        string functionalSpecContent,
        string functionalSpecPath,
        CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid().ToString();

        _logger.LogInformation(
            "Starting test suite generation. SpecPath={Path}, CorrelationId={CorrelationId}",
            functionalSpecPath, correlationId);

        try
        {
            var bucket = _options.UploadBucket;

            var solutionName   = ExtractSolutionName(functionalSpecPath);
            var testFolder     = $"{solutionName}/test suites";
            var testPrefix     = $"{testFolder}/";

            var existingTests = await _storage.ListObjectNamesAsync(bucket, testPrefix, ct);
            var existingSet   = new HashSet<string>(existingTests, StringComparer.OrdinalIgnoreCase);

            int tcVersion = 1;
            string resolvedTestPath;
            do
            {
                resolvedTestPath = $"{testFolder}/{solutionName}_testsuite_v{tcVersion}.md";
                tcVersion++;
            }
            while (existingSet.Contains(resolvedTestPath) && tcVersion <= 9999);

            _logger.LogInformation("Resolved output path for test suite. Path={Path}", resolvedTestPath);

            _logger.LogInformation("Invoking Vertex AI (Gemini) to generate human-readable test suite...");
            var testContent = await _aiService.GenerateTestSuiteAsync(functionalSpecContent, ct);
            
            // We can reuse TestScript entity just to hold the content/path, or just upload directly
            var testScript  = TestScript.Create(testContent, bucket, resolvedTestPath);

            await _storage.SaveFileAsync(
                testScript.BucketName, testScript.StoragePath, testScript.Content, "text/markdown", ct);

            _logger.LogInformation("Test suite saved to Cloud Storage. Path={Path}", testScript.StoragePath);

            // Return the same DTO structure, putting the markdown in TestScriptContent
            return new GenerationResultDto(
                Success:             true,
                CorrelationId:       correlationId,
                FunctionalSpecPath:  functionalSpecPath,
                TestScriptPath:      testScript.StoragePath,
                BuildJobId:          null,
                BuildLogUrl:         null,
                ErrorMessage:        null,
                BuildWarning:        null,
                TestScriptContent:   testScript.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Test suite generation failed. SpecPath={Path}, CorrelationId={CorrelationId}",
                functionalSpecPath, correlationId);

            return new GenerationResultDto(
                Success:            false,
                CorrelationId:      correlationId,
                FunctionalSpecPath: functionalSpecPath,
                TestScriptPath:     null,
                BuildJobId:         null,
                BuildLogUrl:        null,
                ErrorMessage:       ex.Message);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
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
