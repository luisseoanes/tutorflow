using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TutorFlow.Application.Audit;
using TutorFlow.Application.Identity;
using TutorFlow.Application.Llm;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Preguntar;
using TutorFlow.Infrastructure.Audit;
using TutorFlow.Infrastructure.MockApi;
using TutorFlow.Infrastructure.Roster;

namespace TutorFlow.Infrastructure.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    public const int DefaultRosterTtlSeconds = 300;
    private const string DefaultEstrategia = "full-context";
    private const string MockApiClientName = "mock-api";

    /// <summary>
    /// Registro de los servicios de Core. T-05a: fuente de datos (MOCK_API_BASE_URL) y roster (ROSTER_TTL_SECONDS).
    /// T-05b: auditoria a stdout, L1. T-08: caso de uso Preguntar completo (el LLM y la recuperacion los registra AddTutorFlowIa).
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

        // T-08: flujo completo (SPEC 12.3). Solo se consumen puertos y servicios que registra AddTutorFlowIa (IRetriever,
        // IGeneradorRespuesta); se resuelven de forma perezosa para que su configuracion no tumbe los 400 ni los 403 de L1/L2.
        var estrategia = Valor(configuration["RETRIEVAL_STRATEGY"], DefaultEstrategia).ToLowerInvariant();
        var modelo = Valor(configuration["LLM_MODEL"], LlmOptions.DefaultModel);
        services.TryAddSingleton(PreguntarOpciones.PorDefecto);
        services.TryAddSingleton(new InfoFlujoLlm(estrategia, modelo));
        services.AddScoped<IPreguntarAsistente>(sp => new PreguntarAsistente(
            sp.GetRequiredService<IValidadorIdentidad>(),
            sp.GetRequiredService<IRosterProvider>(),
            () => sp.GetRequiredService<IRetriever>(),
            () => sp.GetRequiredService<IGeneradorRespuesta>(),
            sp.GetRequiredService<IToolDispatcher>(),
            sp.GetRequiredService<PreguntarOpciones>(),
            sp.GetRequiredService<InfoFlujoLlm>(),
            sp.GetService<IContextoAuditoria>()));
        return services;
    }

    private static string Valor(string? v, string porDefecto) => string.IsNullOrWhiteSpace(v) ? porDefecto : v.Trim();
}
