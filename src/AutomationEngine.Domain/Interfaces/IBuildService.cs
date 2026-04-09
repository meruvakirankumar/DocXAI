using AutomationEngine.Domain.Entities;

namespace AutomationEngine.Domain.Interfaces;

public interface IBuildService
{
    Task<BuildJob> TriggerTestExecutionAsync(
        string projectId,
        string testScriptBucketPath,
        CancellationToken ct = default);
}
