using System.Text;
using System.Text.Json;
using AutomationEngine.Application.DTOs;
using AutomationEngine.Application.Options;
using AutomationEngine.Application.UseCases;
using AutomationEngine.Domain.Interfaces;
using AutomationEngineService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AutomationEngineService.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrchestratorController : ControllerBase
{
    private readonly IProcessDocumentUseCase _processDocumentUseCase;
    private readonly IStorageRepository _storage;
    private readonly ProcessDocumentOptions _options;
    private readonly ILogger<OrchestratorController> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OrchestratorController(
        IProcessDocumentUseCase processDocumentUseCase,
        IStorageRepository storage,
        IOptions<ProcessDocumentOptions> options,
        ILogger<OrchestratorController> logger)
    {
        _processDocumentUseCase = processDocumentUseCase;
        _storage = storage;
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
    /// Frontend upload endpoint — accepts a design document file,
    /// stores it in Cloud Storage, runs the AI pipeline, and returns
    /// the generated functional specification for on-screen display.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB
    public async Task<IActionResult> UploadAsync(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided or file is empty." });

        var bucket = _options.UploadBucket;
        if (string.IsNullOrWhiteSpace(bucket))
        {
            _logger.LogError("UploadBucket is not configured in GoogleCloud settings.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Upload bucket is not configured." });
        }

        var objectName = $"uploads/{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{file.FileName}";

        _logger.LogInformation(
            "Upload received. FileName={FileName}, Size={Size}, Destination=gs://{Bucket}/{Object}",
            file.FileName, file.Length, bucket, objectName);

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
