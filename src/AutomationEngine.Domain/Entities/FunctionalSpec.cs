namespace AutomationEngine.Domain.Entities;

public sealed class FunctionalSpec
{
    public string Content { get; init; } = string.Empty;
    public string SourceDocumentName { get; init; } = string.Empty;
    public string OutputFileName { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
    public string OutputPath { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Creates a FunctionalSpec with a fully resolved (possibly versioned) output path.
    /// The caller is responsible for resolving versioning before calling this method.
    /// </summary>
    public static FunctionalSpec Create(string content, DesignDocument source, string resolvedOutputPath)
    {
        return new FunctionalSpec
        {
            Content = content,
            SourceDocumentName = source.ObjectName,
            OutputFileName = Path.GetFileName(resolvedOutputPath),
            BucketName = source.BucketName,
            OutputPath = resolvedOutputPath,
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Derives the base output filename from the uploaded object name.
    /// e.g. "MyProject/design.docx" → "functional_design.docx"
    ///      "MyProject/spec-001.md"  → "functional_spec-001.docx"
    /// </summary>
    public static string DeriveBaseFileName(string objectName)
    {
        var fileName = Path.GetFileName(objectName);          // strip folder prefix
        var stem     = Path.GetFileNameWithoutExtension(fileName);
        return $"functional_{stem}.docx";
    }
}
