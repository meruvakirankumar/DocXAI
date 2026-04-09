namespace AutomationEngine.Domain.Entities;

public sealed class TestScript
{
    public string Content { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
    public string StoragePath { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; init; }

    public static TestScript Create(string content, FunctionalSpec spec, string outputFolder = "output")
    {
        // e.g. functional_spec_v2.docx → playwright_tests_v2.spec.ts
        var version = spec.OutputFileName
            .Replace("functional_spec_", string.Empty)
            .Replace(".docx", string.Empty);
        var fileName = $"playwright_tests_{version}.spec.ts";

        return new TestScript
        {
            Content = content,
            FileName = fileName,
            BucketName = spec.BucketName,
            StoragePath = $"{outputFolder}/{fileName}",
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }
}
