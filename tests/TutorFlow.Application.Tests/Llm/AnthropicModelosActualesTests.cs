using System.Net;
using System.Text.Json;
using TutorFlow.Application.Llm;
using TutorFlow.Application.Ports;
using TutorFlow.Infrastructure.Llm;
using static TutorFlow.Application.Tests.Llm.Dobles;

namespace TutorFlow.Application.Tests.Llm;

/// <summary>T-06.5 (INT-023): compatibilidad del adaptador Anthropic con los modelos 5.x (respuestas HTTP grabadas, sin red).</summary>
public class AnthropicModelosActualesTests
{
    private const string RespuestaConThinking =
        """{"id":"msg_1","type":"message","role":"assistant","content":[{"type":"thinking","thinking":"","signature":"SIG-OPACA-123=="},{"type":"redacted_thinking","data":"RED-456"},{"type":"tool_use","id":"toolu_A","name":"obtener_notas","input":{}},{"type":"tool_use","id":"toolu_B","name":"obtener_asistencia","input":{}}],"stop_reason":"tool_use","usage":{"input_tokens":10,"output_tokens":5}}""";

    private const string RespuestaTexto =
        """{"content":[{"type":"text","text":"x"}],"usage":{"input_tokens":1,"output_tokens":1}}""";

    private static readonly LlmRequest Base = new("S", [new LlmMensaje(RolMensaje.Usuario, "q")], [], 100);

    private static LlmOptions Opciones() =>
        new() { Provider = LlmProviders.Anthropic, ApiKey = LlaveCentinela, Model = "modelo-x" };

    private static AnthropicLlmClient Cliente(ManejadorGrabado h, Action<string>? log = null) =>
        new(new HttpClient(h), Opciones(), log);

    [Fact]
    [Trait("Criterio", "RES-07.c")]
    public async Task El_cuerpo_no_contiene_parametros_de_muestreo_ni_tool_choice()
    {
        var h = new ManejadorGrabado(ManejadorGrabado.Respuesta(HttpStatusCode.OK, RespuestaTexto));
        await Cliente(h).CompletarAsync(Base, CancellationToken.None);
        using var doc = JsonDocument.Parse(h.Cuerpos[0]);
        Assert.Equal("low", doc.RootElement.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.False(doc.RootElement.TryGetProperty("thinking", out _));
        foreach (var campo in new[] { "temperature", "top_p", "top_k", "tool_choice", "budget_tokens" })
        {
            Assert.DoesNotContain("\"" + campo + "\"", h.Cuerpos[0], StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Criterio", "RES-07.c")]
    public async Task Los_bloques_thinking_vuelven_identicos_y_en_orden_y_el_aviso_va_en_el_mismo_mensaje_user()
    {
        var h = new ManejadorGrabado(
            ManejadorGrabado.Respuesta(HttpStatusCode.OK, RespuestaConThinking),
            ManejadorGrabado.Respuesta(HttpStatusCode.OK, RespuestaTexto));
        var c = Cliente(h);
        var r1 = await c.CompletarAsync(Base, CancellationToken.None);
        Assert.Equal(2, r1.Invocaciones.Count);

        var historial = Base with
        {
            Mensajes =
            [
                .. Base.Mensajes,
                new LlmMensaje(RolMensaje.Asistente, "{}", "toolu_A", "obtener_notas"),
                new LlmMensaje(RolMensaje.Asistente, "{}", "toolu_B", "obtener_asistencia"),
                new LlmMensaje(RolMensaje.Herramienta, """{"a":1}""", "toolu_A", "obtener_notas"),
                new LlmMensaje(RolMensaje.Herramienta, """{"b":2}""", "toolu_B", "obtener_asistencia"),
                new LlmMensaje(RolMensaje.Usuario, "AVISO"),
            ],
        };
        await c.CompletarAsync(historial, CancellationToken.None);

        using var enviado = JsonDocument.Parse(h.Cuerpos[1]);
        using var original = JsonDocument.Parse(RespuestaConThinking);
        var msgs = enviado.RootElement.GetProperty("messages");
        Assert.Equal(3, msgs.GetArrayLength());
        Assert.Equal("assistant", msgs[1].GetProperty("role").GetString());
        var esperado = original.RootElement.GetProperty("content");
        var recibido = msgs[1].GetProperty("content");
        Assert.Equal(esperado.GetArrayLength(), recibido.GetArrayLength());
        for (var k = 0; k < esperado.GetArrayLength(); k++)
        {
            Assert.Equal(esperado[k].GetRawText(), recibido[k].GetRawText());
        }

        Assert.Equal("user", msgs[2].GetProperty("role").GetString());
        var bloques = msgs[2].GetProperty("content");
        Assert.Equal(
            ["tool_result", "tool_result", "text"],
            bloques.EnumerateArray().Select(b => b.GetProperty("type").GetString()!).ToArray());
        Assert.Equal("AVISO", bloques[2].GetProperty("text").GetString());
    }

    [Fact]
    [Trait("Criterio", "RES-07.c")]
    public async Task Los_bloques_thinking_no_se_registran_en_logs()
    {
        var logs = new List<string>();
        var h = new ManejadorGrabado(ManejadorGrabado.Respuesta(HttpStatusCode.OK, RespuestaConThinking));
        await Cliente(h, logs.Add).CompletarAsync(Base, CancellationToken.None);
        Assert.DoesNotContain(logs, l => l.Contains("SIG-OPACA", StringComparison.Ordinal) || l.Contains("RED-456", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{"content":[],"stop_reason":"refusal","usage":{"input_tokens":3,"output_tokens":0}}""")]
    [InlineData("""{"stop_reason":"refusal"}""")]
    [InlineData("""{"content":[{"type":"thinking","thinking":"","signature":"S"}],"stop_reason":"max_tokens","usage":{"input_tokens":3,"output_tokens":9}}""")]
    [InlineData("""{"content":[{"type":"text","text":"{\"respuesta\":\"cortad"}],"stop_reason":"max_tokens","usage":{"input_tokens":3,"output_tokens":9}}""")]
    [Trait("Criterio", "RES-07.c")]
    public async Task Refusal_y_max_tokens_no_lanzan_y_no_devuelven_invocaciones(string cuerpo)
    {
        var h = new ManejadorGrabado(ManejadorGrabado.Respuesta(HttpStatusCode.OK, cuerpo));
        var r = await Cliente(h).CompletarAsync(Base, CancellationToken.None);
        Assert.Empty(r.Invocaciones);
    }

    [Fact]
    [Trait("Criterio", "RES-07.c")]
    public async Task Un_refusal_lleva_a_abstencion_en_el_generador()
    {
        var h = new ManejadorGrabado(ManejadorGrabado.Respuesta(
            HttpStatusCode.OK, """{"content":[],"stop_reason":"refusal","usage":{"input_tokens":3,"output_tokens":0}}"""));
        var r = await new GeneradorRespuesta(Cliente(h), new GeneradorOptions())
            .GenerarAsync(Estudiante(), "q", [], new DespachadorGuionizado(), CancellationToken.None);
        Assert.True(r.Abstuvo);
    }
}
