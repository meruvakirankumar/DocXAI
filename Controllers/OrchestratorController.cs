using System.Text;
using System.Text.Json;
using AutomationEngine.Application.DTOs;
using AutomationEngine.Application.UseCases;
using AutomationEngineService.Models;
using Microsoft.AspNetCore.Mvc;

namespace AutomationEngineService.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrchestratorController : ControllerBase
{
    private readonly IProcessDocumentUseCase _processDocumentUseCase;
    private readonly ILogger<OrchestratorController> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OrchestratorController(
        IProcessDocumentUseCase processDocumentUseCase,
        ILogger<OrchestratorController> logger)
    {
        _processDocumentUseCase = processDocumentUseCase;
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
