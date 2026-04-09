namespace AutomationEngine.Domain.Entities;

public sealed class FunctionalSpec
{
    public string Content { get; init; } = string.Empty;
    public string SourceDocumentName { get; init; } = string.Empty;
    public string OutputFileName { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
    public string OutputPath { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; init; }

    public static FunctionalSpec Create(string content, DesignDocument source, string outputFolder = "output")
    {
        var outputFileName = $"functional_spec_{source.Version}.docx";
        return new FunctionalSpec
        {
            Content = content,
            SourceDocumentName = source.ObjectName,
            OutputFileName = outputFileName,
            BucketName = source.BucketName,
            OutputPath = $"{outputFolder}/{outputFileName}",
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }
}
