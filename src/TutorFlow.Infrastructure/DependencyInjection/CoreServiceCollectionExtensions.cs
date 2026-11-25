using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TutorFlow.Application.Audit;
using TutorFlow.Application.Identity;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Preguntar;
using TutorFlow.Infrastructure.Audit;
using TutorFlow.Infrastructure.MockApi;
using TutorFlow.Infrastructure.Roster;

namespace TutorFlow.Infrastructure.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    public const int DefaultRosterTtlSeconds = 300;
    private const string MockApiClientName = "mock-api";

    /// <summary>
    /// Registro de los servicios de Core. T-05a: fuente de datos (MOCK_API_BASE_URL) y roster (ROSTER_TTL_SECONDS).
    /// T-05b: auditoria minima a stdout, L1 y caso de uso Preguntar (hasta el roster).
    /// </summary>
    public static IServiceCollection AddTutorFlowCore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var baseUrl = configuration["MOCK_API_BASE_URL"];
        var options = new MockApiOptions
        {
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? MockApiOptions.DefaultBaseUrl : baseUrl.TrimEnd('/'),
        };
        var ttlSeconds = int.TryParse(configuration["ROSTER_TTL_SECONDS"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) && s > 0
            ? s
            : DefaultRosterTtlSeconds;
        var ttl = TimeSpan.FromSeconds(ttlSeconds);

        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton(new AuditoriaStdout(Console.Out));
        var rutaAuditoria = configuration["AUDIT_LOG_PATH"];
        if (!string.IsNullOrWhiteSpace(rutaAuditoria))
        {
            services.TryAddSingleton<IAuditoriaArchivo>(new AuditoriaArchivo(rutaAuditoria));
        }

        services.TryAddSingleton<IAuditLog>(sp => new JsonLineAuditLog(
            sp.GetRequiredService<AuditoriaStdout>(),
            sp.GetService<IAuditoriaArchivo>(),
            sp.GetRequiredService<ILoggerFactory>()));
        services.TryAddScoped<IContextoAuditoria, ContextoAuditoria>();
        services.AddHttpClient(MockApiClientName, http =>
        {
            http.BaseAddress = new Uri(options.BaseUrl + "/", UriKind.Absolute);
            http.Timeout = Timeout.InfiniteTimeSpan; // el timeout lo controla el adaptador por intento
        });
        services.AddSingleton<IAcademicDataSource>(sp =>
            new MockApiAcademicDataSource(sp.GetRequiredService<IHttpClientFactory>().CreateClient(MockApiClientName), options));

        services.AddSingleton(sp => new RosterProvider(
            sp.GetRequiredService<IAcademicDataSource>(),
            sp.GetRequiredService<IAuditLog>(),
            sp.GetRequiredService<IClock>(),
            ttl,
            sp.GetService<ILogger<RosterProvider>>()));
        services.AddSingleton<IRosterProvider>(sp => sp.GetRequiredService<RosterProvider>());
        services.AddSingleton<IHostedService>(sp => new RosterRefreshService(sp.GetRequiredService<RosterProvider>(), ttl));

        services.AddTutorFlowTools();
        services.AddSingleton<IValidadorIdentidad, ValidadorIdentidad>();
        services.AddScoped<IPreguntarAsistente, PreguntarAsistente>();
        return services;
    }
}
