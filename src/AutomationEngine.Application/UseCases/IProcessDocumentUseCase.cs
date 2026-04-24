using AutomationEngine.Application.DTOs;

namespace AutomationEngine.Application.UseCases;

public interface IProcessDocumentUseCase
{
    Task<GenerationResultDto> ExecuteAsync(StorageEventDto storageEvent, CancellationToken ct = default);
    Task<GenerationResultDto> GenerateTestsAsync(string functionalSpecContent, string functionalSpecPath, CancellationToken ct = default);
    Task<GenerationResultDto> GenerateTestSuiteAsync(string functionalSpecContent, string functionalSpecPath, CancellationToken ct = default);
}
