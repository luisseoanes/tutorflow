using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TutorFlow.Application.Ports;

namespace TutorFlow.Application.Llm;

/// <summary>Adaptador de chat completions con herramientas (OpenAI, Ollama, LM Studio...). Temperatura 0.</summary>
public sealed class OpenAiCompatibleLlmClient(
    HttpClient http,
    LlmOptions options,
    Action<string>? log = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null) : HttpLlmClientBase(http, options, log, delay)
{
    protected override string Endpoint =>
        (Options.BaseUrl ?? LlmOptions.DefaultOpenAiBaseUrl).TrimEnd('/') + "/chat/completions";

    protected override void ApplyAuth(HttpRequestMessage request, string apiKey) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

    protected override string BuildBody(LlmRequest r)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("model", Options.Model);
            w.WriteNumber("max_tokens", r.MaxTokens);
            w.WriteNumber("temperature", 0);
            w.WriteStartArray("messages");
            w.WriteStartObject();
            w.WriteString("role", "system");
            w.WriteString("content", r.Sistema);
            w.WriteEndObject();
            var msgs = r.Mensajes;
            for (var i = 0; i < msgs.Count;)
            {
                var m = msgs[i];
                if (EsInvocacionAsistente(m))
                {
                    w.WriteStartObject();
                    w.WriteString("role", "assistant");
                    w.WriteNull("content");
                    w.WriteStartArray("tool_calls");
                    while (i < msgs.Count && EsInvocacionAsistente(msgs[i]))
                    {
                        w.WriteStartObject();
                        w.WriteString("id", msgs[i].InvocacionId);
                        w.WriteString("type", "function");
                        w.WriteStartObject("function");
                        w.WriteString("name", msgs[i].NombreHerramienta);
                        w.WriteString("arguments", ArgumentosComoJson(msgs[i].Contenido).GetRawText());
                        w.WriteEndObject();
                        w.WriteEndObject();
                        i++;
                    }

                    w.WriteEndArray();
                    w.WriteEndObject();
                    continue;
                }

                w.WriteStartObject();
                if (m.Rol == RolMensaje.Herramienta)
                {
                    w.WriteString("role", "tool");
                    w.WriteString("tool_call_id", m.InvocacionId);
                }
                else
                {
                    w.WriteString("role", m.Rol == RolMensaje.Asistente ? "assistant" : "user");
                }

                w.WriteString("content", m.Contenido);
                w.WriteEndObject();
                i++;
            }

            w.WriteEndArray();
            if (r.Herramientas.Count > 0)
            {
                w.WriteStartArray("tools");
                foreach (var t in r.Herramientas)
                {
                    w.WriteStartObject();
                    w.WriteString("type", "function");
                    w.WriteStartObject("function");
                    w.WriteString("name", t.Nombre);
                    w.WriteString("description", t.Descripcion);
                    w.WritePropertyName("parameters");
                    t.EsquemaArgumentos.WriteTo(w);
                    w.WriteEndObject();
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
        var message = root.GetProperty("choices")[0].GetProperty("message");
        var texto = message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString() ?? string.Empty
            : string.Empty;
        var invocaciones = new List<InvocacionPropuesta>();
        if (message.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array)
        {
            foreach (var call in calls.EnumerateArray())
            {
                var fn = call.GetProperty("function");
                var args = fn.TryGetProperty("arguments", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null;
                JsonElement argumentos;
                try
                {
                    using var d = JsonDocument.Parse(string.IsNullOrWhiteSpace(args) ? "{}" : args);
                    argumentos = d.RootElement.Clone();
                }
                catch (JsonException)
                {
                    // Argumentos ilegibles: se entregan como cadena; el despachador los rechaza por esquema.
                    using var d = JsonDocument.Parse(JsonSerializer.Serialize(args));
                    argumentos = d.RootElement.Clone();
                }

                invocaciones.Add(new InvocacionPropuesta(
                    call.TryGetProperty("id", out var id) ? id.GetString() : null,
                    fn.GetProperty("name").GetString() ?? string.Empty,
                    argumentos));
            }
        }

        var usage = root.GetProperty("usage");
        return new LlmResponse(texto, invocaciones, new UsoTokens(Int(usage, "prompt_tokens"), Int(usage, "completion_tokens")));
    }

    private static int Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
}
