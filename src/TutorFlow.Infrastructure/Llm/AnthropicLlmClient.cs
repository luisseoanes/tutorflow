using System.Text;
using System.Text.Json;
using TutorFlow.Application.Ports;

using TutorFlow.Application.Llm;

namespace TutorFlow.Infrastructure.Llm;

/// <summary>Adaptador de la Messages API de Anthropic con herramientas. Temperatura 0.</summary>
public sealed class AnthropicLlmClient(
    HttpClient http,
    LlmOptions options,
    Action<string>? log = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null) : HttpLlmClientBase(http, options, log, delay)
{
    private const string Version = "2023-06-01";

    protected override string Endpoint =>
        (Options.BaseUrl ?? LlmOptions.DefaultAnthropicBaseUrl).TrimEnd('/') + "/v1/messages";

    protected override void ApplyAuth(HttpRequestMessage request, string apiKey)
    {
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", Version);
    }

    protected override string BuildBody(LlmRequest r)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("model", Options.Model);
            w.WriteNumber("max_tokens", r.MaxTokens);
            w.WriteNumber("temperature", 0);
            w.WriteString("system", r.Sistema);
            w.WriteStartArray("messages");
            var msgs = r.Mensajes;
            for (var i = 0; i < msgs.Count;)
            {
                var m = msgs[i];
                if (EsInvocacionAsistente(m))
                {
                    w.WriteStartObject();
                    w.WriteString("role", "assistant");
                    w.WriteStartArray("content");
                    while (i < msgs.Count && EsInvocacionAsistente(msgs[i]))
                    {
                        w.WriteStartObject();
                        w.WriteString("type", "tool_use");
                        w.WriteString("id", msgs[i].InvocacionId);
                        w.WriteString("name", msgs[i].NombreHerramienta);
                        w.WritePropertyName("input");
                        ArgumentosComoJson(msgs[i].Contenido).WriteTo(w);
                        w.WriteEndObject();
                        i++;
                    }

                    w.WriteEndArray();
                    w.WriteEndObject();
                }
                else if (m.Rol == RolMensaje.Herramienta)
                {
                    w.WriteStartObject();
                    w.WriteString("role", "user");
                    w.WriteStartArray("content");
                    while (i < msgs.Count && msgs[i].Rol == RolMensaje.Herramienta)
                    {
                        w.WriteStartObject();
                        w.WriteString("type", "tool_result");
                        w.WriteString("tool_use_id", msgs[i].InvocacionId);
                        w.WriteString("content", msgs[i].Contenido);
                        w.WriteEndObject();
                        i++;
                    }

                    w.WriteEndArray();
                    w.WriteEndObject();
                }
                else
                {
                    w.WriteStartObject();
                    w.WriteString("role", m.Rol == RolMensaje.Asistente ? "assistant" : "user");
                    w.WriteString("content", m.Contenido);
                    w.WriteEndObject();
                    i++;
                }
            }

            w.WriteEndArray();
            if (r.Herramientas.Count > 0)
            {
                w.WriteStartArray("tools");
                foreach (var t in r.Herramientas)
                {
                    w.WriteStartObject();
                    w.WriteString("name", t.Nombre);
                    w.WriteString("description", t.Descripcion);
                    w.WritePropertyName("input_schema");
                    t.EsquemaArgumentos.WriteTo(w);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
            }

            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    protected override LlmResponse ParseResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var texto = new StringBuilder();
        var invocaciones = new List<InvocacionPropuesta>();
        if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var b in content.EnumerateArray())
            {
                var tipo = b.GetProperty("type").GetString();
                if (tipo == "text")
                {
                    texto.Append(b.GetProperty("text").GetString());
                }
                else if (tipo == "tool_use")
                {
                    var input = b.TryGetProperty("input", out var inp) ? inp.Clone() : ArgumentosComoJson("{}");
                    invocaciones.Add(new InvocacionPropuesta(
                        b.TryGetProperty("id", out var id) ? id.GetString() : null,
                        b.GetProperty("name").GetString() ?? string.Empty,
                        input));
                }
            }
        }

        var usage = root.GetProperty("usage");
        var entrada = Int(usage, "input_tokens") + Int(usage, "cache_creation_input_tokens") + Int(usage, "cache_read_input_tokens");
        return new LlmResponse(texto.ToString(), invocaciones, new UsoTokens(entrada, Int(usage, "output_tokens")));
    }

    private static int Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
}
