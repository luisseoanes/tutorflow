using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TutorFlow.Application.Ports;

using TutorFlow.Application.Llm;

namespace TutorFlow.Infrastructure.Llm;

/// <summary>Adaptador de chat completions con herramientas (OpenAI, Ollama, LM Studio...). Temperatura 0.</summary>
public sealed class OpenAiCompatibleLlmClient(
    HttpClient http,
    LlmOptions options,
    Action<string>? log = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null) : HttpLlmClientBase(http, options, log, delay)
{
    // Campos desconocidos de cada tool_call recibido (p. ej. extra_content.google.thought_signature de Gemini),
    // conservados de forma opaca por id de invocacion para reenviarlos intactos en el turno siguiente.
    // El puerto ILlmClient no transporta datos opacos; el adaptador (singleton) los recuerda con tope de tamano.
    private const int MaxExtrasRecordados = 512;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ExtrasInvocacion> _extras = new(StringComparer.Ordinal);
    private long _secuencia;

    private sealed record ExtrasInvocacion(long Orden, IReadOnlyList<(string Nombre, string Json)> Llamada, IReadOnlyList<(string Nombre, string Json)> Funcion);

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
                        _extras.TryGetValue(msgs[i].InvocacionId!, out var extras);
                        if (extras is not null)
                        {
                            foreach (var (nombre, json) in extras.Funcion)
                            {
                                w.WritePropertyName(nombre);
                                w.WriteRawValue(json, skipInputValidation: true);
                            }
                        }

                        w.WriteEndObject();
                        if (extras is not null)
                        {
                            foreach (var (nombre, json) in extras.Llamada)
                            {
                                w.WritePropertyName(nombre);
                                w.WriteRawValue(json, skipInputValidation: true);
                            }
                        }

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
                    if (m.NombreHerramienta is not null && m.InvocacionId is not null && _extras.ContainsKey(m.InvocacionId))
                    {
                        // Proveedores que exigen reenviar extras (Gemini) mapean la respuesta por nombre de funcion.
                        w.WriteString("name", m.NombreHerramienta);
                    }
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

                var idProveedor = call.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
                var extraLlamada = Desconocidos(call, "id", "type", "function");
                var extraFuncion = Desconocidos(fn, "name", "arguments");
                if (extraLlamada.Count > 0 || extraFuncion.Count > 0)
                {
                    if (string.IsNullOrEmpty(idProveedor))
                    {
                        idProveedor = "tf-" + Guid.NewGuid().ToString("N");
                    }

                    Recordar(idProveedor, new ExtrasInvocacion(Interlocked.Increment(ref _secuencia), extraLlamada, extraFuncion));
                }

                invocaciones.Add(new InvocacionPropuesta(
                    string.IsNullOrEmpty(idProveedor) ? null : idProveedor,
                    fn.GetProperty("name").GetString() ?? string.Empty,
                    argumentos));
            }
        }

        var usage = root.GetProperty("usage");
        return new LlmResponse(texto, invocaciones, new UsoTokens(Int(usage, "prompt_tokens"), Int(usage, "completion_tokens")));
    }

    private static List<(string, string)> Desconocidos(JsonElement e, params string[] conocidos)
    {
        var lista = new List<(string, string)>();
        foreach (var p in e.EnumerateObject())
        {
            if (!conocidos.Contains(p.Name, StringComparer.Ordinal))
            {
                lista.Add((p.Name, p.Value.GetRawText()));
            }
        }

        return lista;
    }

    private void Recordar(string id, ExtrasInvocacion extras)
    {
        _extras[id] = extras;
        if (_extras.Count > MaxExtrasRecordados)
        {
            foreach (var viejo in _extras.OrderBy(kv => kv.Value.Orden).Take(_extras.Count - MaxExtrasRecordados / 2).Select(kv => kv.Key))
            {
                _extras.TryRemove(viejo, out _);
            }
        }
    }

    private static int Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
}
