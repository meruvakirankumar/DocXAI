using System.Text;
using System.Text.Json;
using AutomationEngine.Application.DTOs;
using AutomationEngine.Application.Options;
using AutomationEngine.Application.UseCases;
using AutomationEngine.Domain.Interfaces;
using AutomationEngineService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace AutomationEngineService.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrchestratorController : ControllerBase
{
    private readonly IProcessDocumentUseCase _processDocumentUseCase;
    private readonly IStorageRepository _storage;
    private readonly IDocumentSerializer _documentSerializer;
    private readonly ProcessDocumentOptions _options;
    private readonly ILogger<OrchestratorController> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OrchestratorController(
        IProcessDocumentUseCase processDocumentUseCase,
        IStorageRepository storage,
        IDocumentSerializer documentSerializer,
        IOptions<ProcessDocumentOptions> options,
        ILogger<OrchestratorController> logger)
    {
        _processDocumentUseCase = processDocumentUseCase;
        _storage = storage;
        _documentSerializer = documentSerializer;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Eventarc entry point — triggered when a file is finalised in Cloud Storage.
    /// Eventarc sends a CloudEvent (application/cloudevents+json) as the HTTP body.
    /// </summary>
    [HttpPost("trigger")]
    public async Task<IActionResult> TriggerAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var bodyJson = await reader.ReadToEndAsync(ct);

        _logger.LogInformation(
            "Eventarc trigger received. ContentType={ContentType}, BodyLength={Length}",
            Request.ContentType, bodyJson.Length);

        CloudStorageEvent? cloudEvent;
        try
        {
            cloudEvent = JsonSerializer.Deserialize<CloudStorageEvent>(bodyJson, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Eventarc CloudEvent payload");
            return BadRequest(new { error = "Invalid CloudEvent payload", detail = ex.Message });
        }

        if (cloudEvent?.Data is null)
        {
            _logger.LogWarning("CloudEvent data is null. Raw body: {Body}", bodyJson);
            return BadRequest(new { error = "Missing CloudEvent data field" });
        }

        // Guard: ignore output files to prevent Eventarc → Cloud Run → Cloud Storage infinite loops
        if (cloudEvent.Data.Name.StartsWith("output/", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Skipping output file to prevent re-trigger loop. File={Name}", cloudEvent.Data.Name);
            return Ok(new { message = "Output file skipped", file = cloudEvent.Data.Name });
        }

        _logger.LogInformation(
            "Dispatching pipeline for Bucket={Bucket}, Object={Object}",
            cloudEvent.Data.Bucket, cloudEvent.Data.Name);

        var storageEvent = new StorageEventDto
        {
            Bucket = cloudEvent.Data.Bucket,
            Name = cloudEvent.Data.Name,
            ContentType = cloudEvent.Data.ContentType,
            Size = cloudEvent.Data.Size,
            TimeCreated = cloudEvent.Data.TimeCreated
        };

        var result = await _processDocumentUseCase.ExecuteAsync(storageEvent, ct);

        if (!result.Success)
        {
            _logger.LogError(
                "Pipeline failed for Object={Object}. CorrelationId={CorrelationId}, Error={Error}",
                cloudEvent.Data.Name, result.CorrelationId, result.ErrorMessage);

            return StatusCode(StatusCodes.Status500InternalServerError, result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Frontend upload endpoint — accepts a design document file and a solution name,
    /// stores it under {solutionName}/{filename} in Cloud Storage (with automatic
    /// version suffixes -001, -002 … when a file with the same name already exists),
    /// runs the AI pipeline, and returns the generated functional specification.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB
    public async Task<IActionResult> UploadAsync(IFormFile file, [FromForm] string solutionName, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided or file is empty." });

        if (string.IsNullOrWhiteSpace(solutionName))
            return BadRequest(new { error = "solutionName is required. Provide the solution name before uploading." });

        // Sanitise solution name: allow alphanumeric, hyphens, underscores only
        var safeSolution = System.Text.RegularExpressions.Regex.Replace(
            solutionName.Trim(), @"[^a-zA-Z0-9\-_]", "-").Trim('-');

        if (string.IsNullOrWhiteSpace(safeSolution))
            return BadRequest(new { error = "solutionName contains no valid characters. Use letters, numbers, hyphens or underscores." });

        var bucket = _options.UploadBucket;
        if (string.IsNullOrWhiteSpace(bucket))
        {
            _logger.LogError("UploadBucket is not configured in GoogleCloud settings.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Upload bucket is not configured." });
        }

        // ── Resolve versioned object path ─────────────────────────────────
        // Base path: {solutionName}/{originalFileName}
        // If already exists → {solutionName}/{stem}-001.{ext}, -002, …
        var originalFileName = Path.GetFileName(file.FileName);
        var stem = Path.GetFileNameWithoutExtension(originalFileName);
        var ext  = Path.GetExtension(originalFileName);   // includes the dot

        var basePath    = $"{safeSolution}/{originalFileName}";
        var prefix      = $"{safeSolution}/";

        var existingNames = await _storage.ListObjectNamesAsync(bucket, prefix, ct);
        var existingSet   = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        string objectName;
        if (!existingSet.Contains(basePath))
        {
            objectName = basePath;
        }
        else
        {
            // Find next free version slot: -001, -002, …
            int version = 1;
            string candidate;
            do
            {
                candidate = $"{safeSolution}/{stem}-{version:D3}{ext}";
                version++;
            }
            while (existingSet.Contains(candidate) && version <= 999);

            objectName = candidate;
        }

        _logger.LogInformation(
            "Upload received. Solution={Solution}, FileName={FileName}, Size={Size}, Destination=gs://{Bucket}/{Object}",
            safeSolution, file.FileName, file.Length, bucket, objectName);

        // Store file in Cloud Storage
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var fileBytes = ms.ToArray();

        await _storage.SaveFileBytesAsync(bucket, objectName, fileBytes, file.ContentType, ct);
        _logger.LogInformation("File uploaded to Cloud Storage. gs://{Bucket}/{Object}", bucket, objectName);

        // Run the processing pipeline
        var storageEvent = new StorageEventDto
        {
            Bucket = bucket,
            Name = objectName,
            ContentType = file.ContentType,
            Size = file.Length.ToString(),
            TimeCreated = DateTimeOffset.UtcNow
        };

        var result = await _processDocumentUseCase.ExecuteAsync(storageEvent, ct);

        if (!result.Success)
        {
            _logger.LogError(
                "Pipeline failed for uploaded file. Object={Object}, CorrelationId={CorrelationId}, Error={Error}",
                objectName, result.CorrelationId, result.ErrorMessage);

            return StatusCode(StatusCodes.Status500InternalServerError, result);
        }

        _logger.LogInformation(
            "Upload pipeline completed. CorrelationId={CorrelationId}", result.CorrelationId);

        return Ok(result);
    }

    /// <summary>
    /// Converts a functional specification (Markdown text) to a .docx file and
    /// streams it back as a file download. Called by the frontend Save button.
    /// </summary>
    [HttpPost("save-docx")]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5 MB — spec content only
    public IActionResult SaveDocxAsync([FromBody] SaveDocxRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Content))
            return BadRequest(new { error = "Content is required." });

        var docxBytes = _documentSerializer.SerializeToDocx(request.Content);

        var fileName = string.IsNullOrWhiteSpace(request.FileName)
            ? "functional-specification.docx"
            : request.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
                ? request.FileName
                : request.FileName + ".docx";

        return File(
            docxBytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            fileName);
    }

    /// <summary>
    /// Health check endpoint consumed by Cloud Run liveness/readiness probes.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health() =>
        Ok(new
        {
            status = "healthy",
            service = "AutomationEngineService",
            timestamp = DateTimeOffset.UtcNow
        });
}

/// <summary>Request body for the save-docx endpoint.</summary>
public sealed record SaveDocxRequest(
    [Required] string Content,
    string? FileName = null
);
