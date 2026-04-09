using AutomationEngine.Domain.Interfaces;
using AutomationEngine.Infrastructure.Options;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AutomationEngine.Infrastructure.GoogleCloud.Credentials;

/// <summary>
/// Loads a scoped <see cref="GoogleCredential"/> from one of two sources:
/// <list type="bullet">
///   <item><b>Secret Manager</b> — when <c>GoogleCloud:Secrets:ServiceAccountSecretName</c>
///   is configured (production / Cloud Run). The service account JSON stored in Secret Manager
///   is fetched at startup and used for all Google Cloud API calls.</item>
///   <item><b>Application Default Credentials (ADC)</b> — when the secret name is empty
///   (local development). Reads the key file pointed to by
///   <c>GOOGLE_APPLICATION_CREDENTIALS</c>.</item>
/// </list>
/// </summary>
public static class GoogleCredentialFactory
{
    private static readonly string[] Scopes =
    [
        "https://www.googleapis.com/auth/cloud-platform"
    ];

    public static async Task<GoogleCredential> CreateAsync(
        GoogleCloudOptions options,
        ISecretService secretService,
        ILogger logger,
        CancellationToken ct = default)
    {
        var secretName = options.Secrets.ServiceAccountSecretName;

        if (!string.IsNullOrWhiteSpace(secretName))
        {
            logger.LogInformation(
                "Loading GoogleCredential from Secret Manager. Secret={SecretName}", secretName);

            var saJson = await secretService.GetSecretAsync(secretName, ct: ct);

            // FromStreamAsync is the most straightforward way to load SA credentials from JSON.
            // The replacement CredentialFactory API is not yet available in this library version.
#pragma warning disable CS0618
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(saJson));
            var credential = await GoogleCredential.FromStreamAsync(stream, ct);
#pragma warning restore CS0618
            var scoped = credential.CreateScoped(Scopes);

            logger.LogInformation("GoogleCredential loaded successfully from Secret Manager.");
            return scoped;
        }

        logger.LogInformation(
            "ServiceAccountSecretName not configured — falling back to Application Default Credentials (ADC).");

        var adcCredential = (await GoogleCredential.GetApplicationDefaultAsync(ct))
            .CreateScoped(Scopes);

        logger.LogInformation("ADC credential loaded successfully.");
        return adcCredential;
    }
}
