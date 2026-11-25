using System.Text;
using System.Text.Json;
using TutorFlow.Application.Ports;

using TutorFlow.Application.Llm;

namespace TutorFlow.Infrastructure.Llm;

/// <summary>Adaptador de la Messages API de Anthropic con herramientas. No envia parametros de muestreo (los modelos 5.x los rechazan) ni tool_choice.</summary>
public sealed class AnthropicLlmClient(
    HttpClient http,
    LlmOptions options,
    Action<string>? log = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null) : HttpLlmClientBase(http, options, log, delay)
{
    private const string Version = "2023-06-01";
    private const int MaxTurnosRecordados = 256;

    // Bloques de contenido del turno assistant (thinking, redacted_thinking, text, tool_use) tal como llegaron, indexados por
    // id de tool_use. Se reenvian sin interpretar ni modificar (preserved thinking). Nunca se registran en logs.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, TurnoRecordado> _turnos = new(StringComparer.Ordinal);
    private long _secuencia;

    private sealed record TurnoRecordado(long Orden, IReadOnlyList<(string Tipo, string? Id, string Json)> Bloques);

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
            w.WriteString("system", r.Sistema);
            w.WriteStartObject("output_config");
            w.WriteString("effort", "low");
            w.WriteEndObject();
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
                    var grupo = new List<LlmMensaje>();
                    while (i < msgs.Count && EsInvocacionAsistente(msgs[i]))
                    {
                        grupo.Add(msgs[i]);
                        i++;
                    }

                    EscribirTurnoAsistente(w, grupo);
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

                    // El texto del usuario que sigue a los tool_result va en el MISMO mensaje user, como bloque text al final.
                    while (i < msgs.Count && msgs[i].Rol == RolMensaje.Usuario)
                    {
                        w.WriteStartObject();
                        w.WriteString("type", "text");
                        w.WriteString("text", msgs[i].Contenido);
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

    private void EscribirTurnoAsistente(Utf8JsonWriter w, List<LlmMensaje> grupo)
    {
        var emitidos = new HashSet<string>(StringComparer.Ordinal);
        if (_turnos.TryGetValue(grupo[0].InvocacionId!, out var turno))
        {
            var ids = grupo.Select(g => g.InvocacionId!).ToHashSet(StringComparer.Ordinal);
            foreach (var (tipo, id, json) in turno.Bloques)
            {
                if (tipo == "tool_use")
                {
                    if (id is null || !ids.Contains(id))
                    {
                        continue;
                    }

                    emitidos.Add(id);
                }

                using var bloque = JsonDocument.Parse(json);
                bloque.RootElement.WriteTo(w);
            }
        }

        foreach (var g in grupo.Where(g => !emitidos.Contains(g.InvocacionId!)))
        {
            w.WriteStartObject();
            w.WriteString("type", "tool_use");
            w.WriteString("id", g.InvocacionId);
            w.WriteString("name", g.NombreHerramienta);
            w.WritePropertyName("input");
            ArgumentosComoJson(g.Contenido).WriteTo(w);
            w.WriteEndObject();
        }
    }

    protected override LlmResponse ParseResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var texto = new StringBuilder();
        var invocaciones = new List<InvocacionPropuesta>();
        var bloques = new List<(string Tipo, string? Id, string Json)>();
        var opacos = false;
        var rechazo = root.TryGetProperty("stop_reason", out var sr) && sr.ValueKind == JsonValueKind.String
            && sr.GetString() == "refusal";
        if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var b in content.EnumerateArray())
            {
                var tipo = b.ValueKind == JsonValueKind.Object && b.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
                    ? t.GetString() ?? string.Empty
                    : string.Empty;
                string? idBloque = null;
                if (tipo == "text")
                {
                    texto.Append(b.TryGetProperty("text", out var tx) && tx.ValueKind == JsonValueKind.String ? tx.GetString() : null);
                }
                else if (tipo == "tool_use")
                {
                    var input = b.TryGetProperty("input", out var inp) ? inp.Clone() : ArgumentosComoJson("{}");
                    idBloque = b.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
                    invocaciones.Add(new InvocacionPropuesta(
                        idBloque,
                        b.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String ? nm.GetString() ?? string.Empty : string.Empty,
                        input));
                }
                else
                {
                    opacos = true;
                }

                bloques.Add((tipo, idBloque, b.GetRawText()));
            }
        }

        if (rechazo)
        {
            // Sin salida valida: el flujo existente termina en abstencion.
            texto.Clear();
            invocaciones.Clear();
        }
        else if (opacos)
        {
            Recordar(invocaciones.Where(i => i.Id is not null).Select(i => i.Id!).ToList(), bloques);
        }

        var entrada = 0;
        var salida = 0;
        if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            entrada = Int(usage, "input_tokens") + Int(usage, "cache_creation_input_tokens") + Int(usage, "cache_read_input_tokens");
            salida = Int(usage, "output_tokens");
        }

        return new LlmResponse(texto.ToString(), invocaciones, new UsoTokens(entrada, salida));
    }

    private void Recordar(List<string> ids, List<(string Tipo, string? Id, string Json)> bloques)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var turno = new TurnoRecordado(Interlocked.Increment(ref _secuencia), bloques);
        foreach (var id in ids)
        {
            _turnos[id] = turno;
        }

        if (_turnos.Count > MaxTurnosRecordados)
        {
            foreach (var viejo in _turnos.OrderBy(kv => kv.Value.Orden).Take(_turnos.Count - MaxTurnosRecordados / 2).Select(kv => kv.Key))
            {
                _turnos.TryRemove(viejo, out _);
            }
        }
    }

    private static int Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
}
