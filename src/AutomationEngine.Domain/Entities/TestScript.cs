namespace AutomationEngine.Domain.Entities;

public sealed class TestScript
{
    public string Content { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
    public string StoragePath { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Creates a TestScript with a fully resolved (possibly versioned) output path.
    /// The caller is responsible for resolving versioning before calling this method.
    /// </summary>
    public static TestScript Create(string content, string bucketName, string resolvedOutputPath)
    {
        return new TestScript
        {
            Content     = content,
            FileName    = Path.GetFileName(resolvedOutputPath),
            BucketName  = bucketName,
            StoragePath = resolvedOutputPath,
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Derives the base test-case filename from the solution name.
    /// e.g. "MyProject" → "MyProject_testcases_0001.spec.ts"
    /// </summary>
    public static string DeriveBaseFileName(string solutionName)
    {
        return $"{solutionName}_testcases_0001.spec.ts";
    }
}
