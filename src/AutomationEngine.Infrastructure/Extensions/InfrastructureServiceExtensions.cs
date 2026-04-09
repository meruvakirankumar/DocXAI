using AutomationEngine.Domain.Interfaces;
using AutomationEngine.Infrastructure.GoogleCloud.AI;
using AutomationEngine.Infrastructure.GoogleCloud.Build;
using AutomationEngine.Infrastructure.GoogleCloud.Credentials;
using AutomationEngine.Infrastructure.GoogleCloud.Documents;
using AutomationEngine.Infrastructure.GoogleCloud.Secrets;
using AutomationEngine.Infrastructure.GoogleCloud.Storage;
using AutomationEngine.Infrastructure.Options;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutomationEngine.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddGoogleCloudInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind Google Cloud configuration section
        services.Configure<GoogleCloudOptions>(
            configuration.GetSection(GoogleCloudOptions.SectionName));

        // Secret Manager client (bootstraps with ADC — attached SA on Cloud Run,
        // GOOGLE_APPLICATION_CREDENTIALS locally)
        services.AddSingleton<ISecretService, GcpSecretManagerService>();

        // GoogleCredential singleton — loaded once at startup from Secret Manager
        // (when ServiceAccountSecretName is set) or from ADC (local dev / fallback).
        // All downstream GCP clients share this single credential instance.
        services.AddSingleton<GoogleCredential>(sp =>
        {
            var opts    = sp.GetRequiredService<IOptions<GoogleCloudOptions>>().Value;
            var secrets = sp.GetRequiredService<ISecretService>();
            var logger  = sp.GetRequiredService<ILoggerFactory>()
                            .CreateLogger(nameof(GoogleCredentialFactory));

            return GoogleCredentialFactory
                .CreateAsync(opts, secrets, logger)
                .GetAwaiter()
                .GetResult();
        });

        // Register Google Cloud service implementations against Domain interfaces
        services.AddSingleton<IStorageRepository, GcpCloudStorageService>();
        services.AddSingleton<IAIGenerationService, GcpVertexAIService>();
        services.AddSingleton<IBuildService, GcpCloudBuildService>();
        services.AddSingleton<IDocumentSerializer, OpenXmlDocumentSerializer>();

        return services;
    }
}

