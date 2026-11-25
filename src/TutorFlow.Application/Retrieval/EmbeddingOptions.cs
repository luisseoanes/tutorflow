using System.Globalization;

namespace TutorFlow.Application.Retrieval;

/// <summary>Configuracion invalida de embeddings (vector/hybrid sin EMBEDDING_* completo). Falla al arrancar.</summary>
public sealed class EmbeddingConfiguracionException(string message) : InvalidOperationException(message);

/// <summary>Uso acumulado de embeddings (tokens de entrada y costo en USD).</summary>
public interface IEmbeddingUsageReporter
{
    long Tokens { get; }

    decimal CostoUsd { get; }
}

/// <summary>
/// Configuracion de embeddings (SPEC 11): EMBEDDING_PROVIDER (openai-compatible), EMBEDDING_MODEL, EMBEDDING_API_KEY,
/// EMBEDDING_PRICE_PER_MTOK y, opcionales, EMBEDDING_BASE_URL (si falta se usa LLM_BASE_URL solo cuando
/// LLM_PROVIDER es openai-compatible; si no, https://api.openai.com/v1) y EMBEDDING_CACHE_PATH (archivo de cache).
/// <see cref="ApiKey"/> jamas se imprime.
/// </summary>
public sealed record EmbeddingOptions
{
    public const string OpenAiCompatible = "openai-compatible";
    public const string DefaultBaseUrl = "https://api.openai.com/v1";

    public string? Provider { get; init; }

    public string? Model { get; init; }

    public string? ApiKey { get; init; }

    public string? BaseUrl { get; init; }

    public decimal PrecioPorMtok { get; init; }

    public string? CachePath { get; init; }

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(Provider) && !string.IsNullOrWhiteSpace(Model) && !string.IsNullOrWhiteSpace(ApiKey);

    public override string ToString() => $"EmbeddingOptions(Provider={Provider}, Model={Model})";

    public static EmbeddingOptions FromConfig(Func<string, string?> get)
    {
        ArgumentNullException.ThrowIfNull(get);
        var baseUrl = Valor(get("EMBEDDING_BASE_URL"));
        if (baseUrl is null && string.Equals(Valor(get("LLM_PROVIDER")), "openai-compatible", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = Valor(get("LLM_BASE_URL"));
        }

        decimal precio = 0m;
        if (Valor(get("EMBEDDING_PRICE_PER_MTOK")) is { } p
            && (!decimal.TryParse(p, NumberStyles.Number, CultureInfo.InvariantCulture, out precio) || precio < 0))
        {
            throw new EmbeddingConfiguracionException("EMBEDDING_PRICE_PER_MTOK debe ser un decimal no negativo (punto decimal).");
        }

        return new EmbeddingOptions
        {
            Provider = Valor(get("EMBEDDING_PROVIDER"))?.ToLowerInvariant(),
            Model = Valor(get("EMBEDDING_MODEL")),
            ApiKey = Valor(get("EMBEDDING_API_KEY")),
            BaseUrl = baseUrl,
            PrecioPorMtok = precio,
            CachePath = Valor(get("EMBEDDING_CACHE_PATH")),
        };
    }

    /// <summary>Lanza si falta algo para usar vector/hybrid. El mensaje nombra variables, nunca valores.</summary>
    public void ExigirParaVectorial(string estrategia)
    {
        var faltan = new List<string>();
        if (string.IsNullOrWhiteSpace(Provider))
        {
            faltan.Add("EMBEDDING_PROVIDER");
        }
        else if (Provider != OpenAiCompatible)
        {
            throw new EmbeddingConfiguracionException(
                $"EMBEDDING_PROVIDER '{Provider}' no es valido. Valor admitido: {OpenAiCompatible}.");
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            faltan.Add("EMBEDDING_MODEL");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            faltan.Add("EMBEDDING_API_KEY");
        }

        if (faltan.Count > 0)
        {
            throw new EmbeddingConfiguracionException(
                $"RETRIEVAL_STRATEGY '{estrategia}' requiere embeddings configurados; faltan: {string.Join(", ", faltan)}.");
        }

        if (CachePath is not null && EstaBajoSrc(CachePath))
        {
            throw new EmbeddingConfiguracionException("EMBEDDING_CACHE_PATH no puede apuntar dentro de src/.");
        }
    }

    private static bool EstaBajoSrc(string path)
    {
        var partes = Path.GetFullPath(path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return partes.Contains("src", StringComparer.Ordinal);
    }

    private static string? Valor(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
