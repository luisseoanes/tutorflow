using System.Globalization;

namespace TutorFlow.Application.Llm;

public static class LlmProviders
{
    public const string Anthropic = "anthropic";
    public const string OpenAiCompatible = "openai-compatible";
}

/// <summary>Configuracion del proveedor LLM (SPEC 11). <see cref="ApiKey"/> jamas se imprime.</summary>
public sealed record LlmOptions
{
    public const string DefaultModel = "claude-haiku-5-5";
    public const int DefaultMaxTokensPerRequest = 20000;
    public const string DefaultAnthropicBaseUrl = "https://api.anthropic.com";
    public const string DefaultOpenAiBaseUrl = "https://api.openai.com/v1";

    public string Provider { get; init; } = LlmProviders.Anthropic;

    public string Model { get; init; } = DefaultModel;

    public string? BaseUrl { get; init; }

    public string? ApiKey { get; init; }

    public int MaxTokensPerRequest { get; init; } = DefaultMaxTokensPerRequest;

    public decimal PrecioEntradaPorMtok { get; init; }

    public decimal PrecioSalidaPorMtok { get; init; }

    // Evita que la llave salga por ToString() del record.
    public override string ToString() => $"LlmOptions(Provider={Provider}, Model={Model})";

    /// <summary>Lee LLM_PROVIDER, LLM_MODEL, LLM_BASE_URL, LLM_API_KEY, LLM_MAX_TOKENS_PER_REQUEST y los precios.</summary>
    public static LlmOptions FromConfig(Func<string, string?> get)
    {
        ArgumentNullException.ThrowIfNull(get);
        var provider = (Valor(get("LLM_PROVIDER")) ?? LlmProviders.Anthropic).ToLowerInvariant();
        if (provider is not (LlmProviders.Anthropic or LlmProviders.OpenAiCompatible))
        {
            throw new LlmConfiguracionException(
                $"LLM_PROVIDER '{provider}' no es valido. Valores admitidos: {LlmProviders.Anthropic} | {LlmProviders.OpenAiCompatible}.");
        }

        return new LlmOptions
        {
            Provider = provider,
            Model = Valor(get("LLM_MODEL")) ?? DefaultModel,
            BaseUrl = Valor(get("LLM_BASE_URL")),
            ApiKey = Valor(get("LLM_API_KEY")),
            MaxTokensPerRequest = Entero(get("LLM_MAX_TOKENS_PER_REQUEST"), "LLM_MAX_TOKENS_PER_REQUEST", DefaultMaxTokensPerRequest),
            PrecioEntradaPorMtok = Decimal(get("LLM_PRICE_INPUT_PER_MTOK"), "LLM_PRICE_INPUT_PER_MTOK"),
            PrecioSalidaPorMtok = Decimal(get("LLM_PRICE_OUTPUT_PER_MTOK"), "LLM_PRICE_OUTPUT_PER_MTOK"),
        };
    }

    private static string? Valor(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static int Entero(string? v, string nombre, int porDefecto)
    {
        if (Valor(v) is not { } s)
        {
            return porDefecto;
        }

        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n < 1)
        {
            throw new LlmConfiguracionException($"{nombre} debe ser un entero positivo.");
        }

        return n;
    }

    private static decimal Decimal(string? v, string nombre)
    {
        if (Valor(v) is not { } s)
        {
            return 0m;
        }

        if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) || d < 0)
        {
            throw new LlmConfiguracionException($"{nombre} debe ser un decimal no negativo (punto decimal).");
        }

        return d;
    }
}
