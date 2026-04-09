using AutomationEngine.Domain.Enums;

namespace AutomationEngine.Domain.Entities;

public sealed class ProcessingContext
{
    public string CorrelationId { get; } = Guid.NewGuid().ToString();
    public string BucketName { get; init; } = string.Empty;
    public string ObjectName { get; init; } = string.Empty;
    public ProcessingStatus Status { get; private set; } = ProcessingStatus.Pending;
    public DesignDocument? DesignDocument { get; private set; }
    public FunctionalSpec? FunctionalSpec { get; private set; }
    public TestScript? TestScript { get; private set; }
    public BuildJob? BuildJob { get; private set; }
    public string? FailureReason { get; private set; }

    public static ProcessingContext Start(string bucketName, string objectName) =>
        new() { BucketName = bucketName, ObjectName = objectName, Status = ProcessingStatus.Processing };

    public void SetDesignDocument(DesignDocument doc)
    {
        DesignDocument = doc;
        Status = ProcessingStatus.DocumentLoaded;
    }

    public void SetFunctionalSpec(FunctionalSpec spec)
    {
        FunctionalSpec = spec;
        Status = ProcessingStatus.SpecGenerated;
    }

    public void SetTestScript(TestScript script)
    {
        TestScript = script;
        Status = ProcessingStatus.TestsGenerated;
    }

    public void SetBuildJob(BuildJob job)
    {
        BuildJob = job;
        Status = ProcessingStatus.BuildTriggered;
    }

    public void MarkCompleted() => Status = ProcessingStatus.Completed;

    public void MarkFailed(string reason)
    {
        FailureReason = reason;
        Status = ProcessingStatus.Failed;
    }
}
