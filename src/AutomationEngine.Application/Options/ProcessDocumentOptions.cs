namespace AutomationEngine.Application.Options;

public sealed class ProcessDocumentOptions
{
    public const string SectionName = "GoogleCloud";

    public string ProjectId { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = "output";
    public string CloudBuildPlaywrightImage { get; set; } = "node:22-bullseye-slim";

    /// <summary>GCS bucket for files uploaded via the frontend.</summary>
    public string UploadBucket { get; set; } = string.Empty;
}
