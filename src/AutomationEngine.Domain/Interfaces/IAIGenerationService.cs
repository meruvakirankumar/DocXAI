namespace AutomationEngine.Domain.Interfaces;

public interface IAIGenerationService
{
    Task<string> GenerateFunctionalSpecAsync(
        string designDocumentContent,
        CancellationToken ct = default);

    Task<string> GeneratePlaywrightTestsAsync(string functionalSpecContent, CancellationToken ct = default);

    Task<string> GenerateTestSuiteAsync(string functionalSpecContent, CancellationToken ct = default);
}
