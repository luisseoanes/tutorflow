using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TutorFlow.Infrastructure.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    /// <summary>Registro de los servicios de Core. Vacio en T-01; se llena en T-02..T-08.</summary>
    public static IServiceCollection AddTutorFlowCore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }
}
