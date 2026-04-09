using System.Text.Json.Serialization;

namespace AutomationEngineService.Models;

/// <summary>
/// Represents the CloudEvent envelope sent by Eventarc for Cloud Storage triggers.
/// Type: google.cloud.storage.object.v1.finalized
/// </summary>
public sealed class CloudStorageEvent
{
    [JsonPropertyName("specversion")]
    public string SpecVersion { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    [JsonPropertyName("subject")]
    public string Subject { get; init; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("time")]
    public DateTimeOffset Time { get; init; }

    /// <summary>Cloud Storage object metadata populated by Eventarc.</summary>
    [JsonPropertyName("data")]
    public CloudStorageObjectData? Data { get; init; }
}

public sealed class CloudStorageObjectData
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

    [JsonPropertyName("generation")]
    public string Generation { get; init; } = string.Empty;

    [JsonPropertyName("metageneration")]
    public string Metageneration { get; init; } = string.Empty;
}
