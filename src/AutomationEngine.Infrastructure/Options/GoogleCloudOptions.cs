namespace AutomationEngine.Infrastructure.Options;

public sealed class GoogleCloudOptions
{
    public const string SectionName = "GoogleCloud";

    public string ProjectId { get; set; } = string.Empty;
    public string Location { get; set; } = "us-central1";

    /// <summary>Gemini model ID, e.g. "gemini-2.5-pro"</summary>
    public string GeminiModelId { get; set; } = "gemini-2.5-pro";

    /// <summary>GCS folder for generated output files.</summary>
    public string OutputFolder { get; set; } = "output";

    /// <summary>Docker image used in the Cloud Build step that runs Playwright tests.</summary>
    public string CloudBuildPlaywrightImage { get; set; } = "node:22-bullseye-slim";

    /// <summary>
    /// Secret Manager resource names for service account credentials.
    /// Leave empty in local dev — ADC (GOOGLE_APPLICATION_CREDENTIALS) is used instead.
    /// In production, set to the full resource name returned by setup-secrets.ps1.
    /// </summary>
    public SecretOptions Secrets { get; set; } = new();
}

public sealed class SecretOptions
{
    /// <summary>
    /// Full resource name of the secret holding the service account JSON.
    /// Example: projects/in-300000000123933-mfg/secrets/automation-engine-sa-json
    /// When set, the app loads credentials from Secret Manager instead of ADC.
    /// </summary>
    public string ServiceAccountSecretName { get; set; } = string.Empty;

    /// <summary>
    /// Full resource name of the private key PEM secret (audit/reference).
    /// Example: projects/in-300000000123933-mfg/secrets/automation-engine-sa-private-key
    /// </summary>
    public string PrivateKeySecretName { get; set; } = string.Empty;

    /// <summary>
    /// Full resource name of the private key ID secret (audit/reference).
    /// Example: projects/in-300000000123933-mfg/secrets/automation-engine-sa-private-key-id
    /// </summary>
    public string PrivateKeyIdSecretName { get; set; } = string.Empty;
}
