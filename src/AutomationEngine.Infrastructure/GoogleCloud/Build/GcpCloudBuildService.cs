using AutomationEngine.Domain.Entities;
using AutomationEngine.Domain.Interfaces;
using AutomationEngine.Infrastructure.Options;
using Google.Apis.Auth.OAuth2;
using Google.Apis.CloudBuild.v1;
using Google.Apis.CloudBuild.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutomationEngine.Infrastructure.GoogleCloud.Build;

public sealed class GcpCloudBuildService : IBuildService
{
    private readonly GoogleCloudOptions _options;
    private readonly GoogleCredential _credential;
    private readonly ILogger<GcpCloudBuildService> _logger;

    public GcpCloudBuildService(
        GoogleCredential credential,
        IOptions<GoogleCloudOptions> options,
        ILogger<GcpCloudBuildService> logger)
    {
        _credential = credential;   // loaded from Secret Manager (or ADC in local dev)
        _options = options.Value;
        _logger = logger;
    }

    public async Task<BuildJob> TriggerTestExecutionAsync(
        string projectId,
        string testScriptBucketPath,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Submitting Cloud Build job for Playwright tests. ScriptPath=gs://{Path}, Project={Project}",
            testScriptBucketPath, projectId);

        // Use the injected credential (already scoped to cloud-platform)
        var cloudBuildService = new CloudBuildService(new BaseClientService.Initializer
        {
            HttpClientInitializer = _credential,
            ApplicationName = "AutomationEngineService"
        });

        var build = CreatePlaywrightBuild(testScriptBucketPath);

        // projects.builds.create returns a long-running Operation
        var operation = await cloudBuildService.Projects.Builds
            .Create(build, projectId)
            .ExecuteAsync(ct);

        var buildId = ExtractBuildId(operation.Name);
        var logUrl = $"https://console.cloud.google.com/cloud-build/builds/{buildId}?project={projectId}";

        _logger.LogInformation(
            "Cloud Build job queued. BuildId={BuildId}, LogUrl={LogUrl}", buildId, logUrl);

        return new BuildJob
        {
            JobId = buildId,
            ProjectId = projectId,
            Status = "QUEUED",
            LogUrl = logUrl,
            TriggeredAt = DateTimeOffset.UtcNow
        };
    }

    // ── Build definition ─────────────────────────────────────────────────────

    private Google.Apis.CloudBuild.v1.Data.Build CreatePlaywrightBuild(string testScriptBucketPath)
    {
        // Step 1: Download the generated test script from Cloud Storage into /workspace
        var downloadStep = new BuildStep
        {
            Name = "gcr.io/google.com/cloudsdktool/cloud-sdk:slim",
            Args = new List<string>
            {
                "gsutil", "cp",
                $"gs://{testScriptBucketPath}",
                "/workspace/test.spec.ts"
            }
        };

        // Step 2: Run Playwright tests using the configured Node image
        var runStep = new BuildStep
        {
            Name = _options.CloudBuildPlaywrightImage,
            Entrypoint = "bash",
            Args = new List<string>
            {
                "-c",
                "cd /workspace && " +
                "npm init -y && " +
                "npm install -D @playwright/test && " +
                "npx playwright install --with-deps chromium && " +
                "npx playwright test test.spec.ts --reporter=list 2>&1"
            },
            Env = new List<string>
            {
                "HOME=/root",
                "PLAYWRIGHT_BROWSERS_PATH=/root/.cache/ms-playwright"
            }
        };

        return new Google.Apis.CloudBuild.v1.Data.Build
        {
            Steps = new List<BuildStep> { downloadStep, runStep },
            Tags = new List<string> { "automation-engine", "playwright-tests" },
            Timeout = "3600s"   // 1-hour timeout
        };
    }

    // Extracts the build ID from an operation name.
    // Format: "operations/build/{project}/{buildId}"
    private static string ExtractBuildId(string? operationName)
    {
        if (string.IsNullOrWhiteSpace(operationName))
            return Guid.NewGuid().ToString();

        var parts = operationName.Split('/');
        return parts.Length > 0 ? parts[^1] : Guid.NewGuid().ToString();
    }
}
