using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TutorFlow.Application.Llm;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Retrieval;

namespace TutorFlow.Infrastructure.Llm;

public static class LlmServiceCollectionExtensions
{
    /// <summary>
    /// Registra el proveedor (LLM_PROVIDER) y el generador. Un proveedor desconocido lanza
    /// <see cref="LlmConfiguracionException"/> aqui, al arrancar. Sin LLM_API_KEY la aplicacion arranca y
    /// las peticiones que llegan al LLM lanzan <see cref="LlmNoDisponibleException"/> (503).
    /// Invocado por <c>AddTutorFlowIa</c>.
    /// </summary>
    public static IServiceCollection AddTutorFlowLlm(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var opciones = LlmOptions.FromConfig(k => configuration[k]);
        services.AddSingleton(opciones);
        services.AddSingleton<ILlmClient>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("TutorFlow.Llm");
            // El HttpClient aplica su propio timeout por intento (20 s) en el adaptador.
            var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            return LlmClientFactory.Crear(opciones, http, mensaje => logger.LogWarning("{Mensaje}", mensaje));
        });
        services.AddSingleton<IGeneradorRespuesta>(sp => new GeneradorRespuesta(
            sp.GetRequiredService<ILlmClient>(),
            new GeneradorOptions(opciones.MaxTokensPerRequest, opciones.PrecioEntradaPorMtok, opciones.PrecioSalidaPorMtok),
            sp.GetService<IChunkCatalog>()));
        return services;
    }
}
