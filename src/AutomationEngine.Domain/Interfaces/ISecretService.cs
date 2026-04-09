namespace AutomationEngine.Domain.Interfaces;

public interface ISecretService
{
    /// <summary>
    /// Retrieves a secret value from Google Secret Manager.
    /// </summary>
    /// <param name="secretResourceName">
    /// Full resource name: projects/{projectId}/secrets/{secretId}
    /// </param>
    /// <param name="version">Secret version (default: "latest")</param>
    Task<string> GetSecretAsync(string secretResourceName, string version = "latest", CancellationToken ct = default);
}
