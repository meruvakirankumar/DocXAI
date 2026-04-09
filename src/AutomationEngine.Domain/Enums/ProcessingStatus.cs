namespace AutomationEngine.Domain.Enums;

public enum ProcessingStatus
{
    Pending,
    Processing,
    DocumentLoaded,
    SpecGenerated,
    TestsGenerated,
    BuildTriggered,
    Completed,
    Failed
}
