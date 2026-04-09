using System.Text.Json.Serialization;

namespace AutomationEngine.Application.DTOs;

public sealed record StorageEventDto
{
    [JsonPropertyName("bucket")]
    public string Bucket { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string ContentType { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public string Size { get; init; } = string.Empty;

    [JsonPropertyName("timeCreated")]
    public DateTimeOffset TimeCreated { get; init; }
}
