namespace AutomationEngine.Domain.Entities;

public sealed class BuildJob
{
    public string JobId { get; init; } = string.Empty;
    public string ProjectId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string LogUrl { get; init; } = string.Empty;
    public DateTimeOffset TriggeredAt { get; init; }
}
