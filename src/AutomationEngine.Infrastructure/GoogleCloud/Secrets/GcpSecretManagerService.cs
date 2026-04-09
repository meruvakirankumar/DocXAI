using AutomationEngine.Domain.Interfaces;
using Google.Cloud.SecretManager.V1;
using Microsoft.Extensions.Logging;

namespace AutomationEngine.Infrastructure.GoogleCloud.Secrets;

public sealed class GcpSecretManagerService : ISecretService
{
    private readonly SecretManagerServiceClient _client;
    private readonly ILogger<GcpSecretManagerService> _logger;

    public GcpSecretManagerService(ILogger<GcpSecretManagerService> logger)
    {
        // Uses Application Default Credentials automatically on Cloud Run
        _client = SecretManagerServiceClient.Create();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GetSecretAsync(
        string secretResourceName,
        string version = "latest",
        CancellationToken ct = default)
    {
        // Full resource name: projects/{project}/secrets/{secretId}/versions/{version}
        var versionName = $"{secretResourceName}/versions/{version}";

        _logger.LogInformation("Accessing secret version: {ResourceName}", versionName);

        var response = await _client.AccessSecretVersionAsync(versionName, ct);
        return response.Payload.Data.ToStringUtf8();
    }
}
