namespace AutomationEngine.Application.DTOs;

public sealed record GenerationResultDto(
    bool Success,
    string CorrelationId,
    string? FunctionalSpecPath,
    string? TestScriptPath,
    string? BuildJobId,
    string? BuildLogUrl,
    string? ErrorMessage
);
