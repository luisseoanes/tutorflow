using TutorFlow.Application.Ports;

using TutorFlow.Application.Llm;

namespace TutorFlow.Infrastructure.Llm;

/// <summary>Resuelve el adaptador segun LLM_PROVIDER. Sin proveedor falso en ejecucion (SPEC A-11).</summary>
public static class LlmClientFactory
{
    public static ILlmClient Crear(
        LlmOptions options,
        HttpClient http,
        Action<string>? log = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(http);
        return options.Provider switch
        {
            LlmProviders.Anthropic => new AnthropicLlmClient(http, options, log, delay),
            LlmProviders.OpenAiCompatible => new OpenAiCompatibleLlmClient(http, options, log, delay),
            _ => throw new LlmConfiguracionException(
                $"LLM_PROVIDER '{options.Provider}' no es valido. Valores admitidos: {LlmProviders.Anthropic} | {LlmProviders.OpenAiCompatible}."),
        };
    }
}
