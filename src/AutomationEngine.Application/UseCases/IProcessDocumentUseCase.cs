using AutomationEngine.Application.DTOs;

namespace AutomationEngine.Application.UseCases;

public interface IProcessDocumentUseCase
{
    Task<GenerationResultDto> ExecuteAsync(StorageEventDto storageEvent, CancellationToken ct = default);
}
