using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TutorFlow.Application.Ports;
using TutorFlow.Infrastructure.Llm;
using TutorFlow.Application.Retrieval;

namespace TutorFlow.Infrastructure.Retrieval;

public static class IaServiceCollectionExtensions
{
    public const string DefaultKnowledgePath = "/app/knowledge";
    public const string DefaultQuarantine = "bienestar_y_becas.md#Anexo";
    public const string DefaultStrategy = "full-context";

    /// <summary>
    /// Registro de los servicios de IA (INT-008). Core lo enlaza en Program.cs (T-08).
    /// Claves: KNOWLEDGE_PATH, QUARANTINE_CHUNKS (lista separada por comas), RETRIEVAL_STRATEGY (full-context|bm25).
    /// </summary>
    public static IServiceCollection AddTutorFlowIa(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var path = Valor(configuration["KNOWLEDGE_PATH"], DefaultKnowledgePath);
        var cuarentena = Valor(configuration["QUARANTINE_CHUNKS"], DefaultQuarantine)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var estrategia = Valor(configuration["RETRIEVAL_STRATEGY"], DefaultStrategy).ToLowerInvariant();

        // vector/hybrid solo con EMBEDDING_* completo: se valida al registrar (arranque), antes de cualquier peticion.
        var embedding = EmbeddingOptions.FromConfig(k => configuration[k]);
        RetrieverFactory.Validar(estrategia, embedding);

        services.AddSingleton<IChunkCatalog>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("TutorFlow.Knowledge");
            return new KnowledgeCatalog(path, cuarentena, m => Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(logger, "{Mensaje}", m));
        });
        services.AddSingleton(embedding);
        services.AddSingleton<IRetriever>(sp =>
        {
            var catalog = sp.GetRequiredService<IChunkCatalog>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("TutorFlow.Embeddings");
            return RetrieverFactory.Crear(estrategia, catalog, embedding, null, m => Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(logger, "{Mensaje}", m));
        });
        services.AddTutorFlowLlm(configuration);
        return services;
    }

    private static string Valor(string? v, string porDefecto) => string.IsNullOrWhiteSpace(v) ? porDefecto : v;
}
