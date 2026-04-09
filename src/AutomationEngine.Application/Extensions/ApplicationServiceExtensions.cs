using AutomationEngine.Application.Options;
using AutomationEngine.Application.UseCases;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutomationEngine.Application.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ProcessDocumentOptions>(
            configuration.GetSection(ProcessDocumentOptions.SectionName));

        services.AddScoped<IProcessDocumentUseCase, ProcessDocumentUseCase>();

        return services;
    }
}
