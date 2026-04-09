namespace AutomationEngine.Domain.Entities;

public sealed class DesignDocument
{
    public string BucketName { get; init; } = string.Empty;
    public string ObjectName { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public DateTimeOffset UploadedAt { get; init; }

    public static DesignDocument Create(string bucketName, string objectName, string content)
    {
        return new DesignDocument
        {
            BucketName = bucketName,
            ObjectName = objectName,
            Content = content,
            Version = ExtractVersion(objectName),
            UploadedAt = DateTimeOffset.UtcNow
        };
    }

    // Extracts version tag: "design_v2.md" → "v2"
    private static string ExtractVersion(string objectName)
    {
        var fileName = Path.GetFileNameWithoutExtension(objectName);
        var parts = fileName.Split('_');
        return parts.Length > 1 ? parts[^1] : "v1";
    }
}
