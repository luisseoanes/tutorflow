using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Tools;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Infrastructure.DependencyInjection;

public static class ToolsServiceCollectionExtensions
{
    /// <summary>Registra la politica de autorizacion determinista y el despachador de herramientas (L3 + esquema + L4).</summary>
    public static IServiceCollection AddTutorFlowTools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IAuthorizationPolicy, AuthorizationPolicy>();
        services.TryAddSingleton<IToolDispatcher>(sp => new ToolDispatcher(
            sp.GetRequiredService<IAcademicDataSource>(),
            sp.GetRequiredService<IRosterProvider>(),
            sp.GetRequiredService<IAuthorizationPolicy>()));
        return services;
    }
}
