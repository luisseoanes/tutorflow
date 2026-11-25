using System.Net;
using System.Text.Json;
using TutorFlow.Application.Llm;
using TutorFlow.Application.Ports;
using static TutorFlow.Application.Tests.Llm.Dobles;

namespace TutorFlow.Application.Tests.Llm;

public class ProveedoresTests
{
    private static readonly LlmRequest Solicitud = new(
        "Sistema",
        [new LlmMensaje(RolMensaje.Usuario, "Hola")],
        [new ToolDefinition("obtener_notas", "Notas.", Json("""{"type":"object","properties":{}}"""))],
        500);

    private static LlmOptions Opciones(string proveedor) =>
        new() { Provider = proveedor, ApiKey = LlaveCentinela, Model = "modelo-x" };

    private sealed class Registro
    {
        public List<string> Logs { get; } = [];

        public List<TimeSpan> Esperas { get; } = [];

        public void Log(string m) => Logs.Add(m);

        public Task Espera(TimeSpan t, CancellationToken ct)
        {
            Esperas.Add(t);
            return Task.CompletedTask;
        }
    }

    private static ILlmClient Cliente(string proveedor, ManejadorGrabado h, Registro reg, LlmOptions? o = null) =>
        LlmClientFactory.Crear(o ?? Opciones(proveedor), new HttpClient(h), reg.Log, reg.Espera);

    [Theory]
    [InlineData(LlmProviders.Anthropic)]
    [InlineData(LlmProviders.OpenAiCompatible)]
    [Trait("Criterio", "RES-05.a")]
    public async Task Un_401_no_se_reintenta_y_produce_LlmNoDisponible(string proveedor)
    {
        var h = new ManejadorGrabado(ManejadorGrabado.Respuesta(HttpStatusCode.Unauthorized, """{"error":"invalid x-api-key"}"""));
        var reg = new Registro();

        var ex = await Assert.ThrowsAsync<LlmNoDisponibleException>(() => Cliente(proveedor, h, reg).CompletarAsync(Solicitud, CancellationToken.None));

        Assert.Equal(1, h.Llamadas);
        Assert.Equal(401, ex.StatusCode);
        Assert.Empty(reg.Esperas);
    }

    [Fact]
    [Trait("Criterio", "RES-05.a")]
    public async Task Un_403_tampoco_se_reintenta()
    {
        var h = new ManejadorGrabado(ManejadorGrabado.Respuesta(HttpStatusCode.Forbidden));

        await Assert.ThrowsAsync<LlmNoDisponibleException>(() => Cliente(LlmProviders.Anthropic, h, new Registro()).CompletarAsync(Solicitud, CancellationToken.None));

        Assert.Equal(1, h.Llamadas);
    }

    [Theory]
    [InlineData(LlmProviders.Anthropic)]
    [InlineData(LlmProviders.OpenAiCompatible)]
    [Trait("Criterio", "RES-05.b")]
    public async Task Un_429_y_luego_5xx_da_3_llamadas_respeta_retry_after_y_termina_en_LlmNoDisponible(string proveedor)
    {
        var h = new ManejadorGrabado(
            ManejadorGrabado.Respuesta(HttpStatusCode.TooManyRequests, retryAfterSegundos: 2),
            ManejadorGrabado.Respuesta(HttpStatusCode.ServiceUnavailable, retryAfterSegundos: 3),
            ManejadorGrabado.Respuesta(HttpStatusCode.InternalServerError));
        var reg = new Registro();

        var ex = await Assert.ThrowsAsync<LlmNoDisponibleException>(() => Cliente(proveedor, h, reg).CompletarAsync(Solicitud, CancellationToken.None));

        Assert.Equal(3, h.Llamadas);
        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)], reg.Esperas);
        Assert.Equal(500, ex.StatusCode);
    }

    [Fact]
    [Trait("Criterio", "RES-05.b")]
    public async Task Un_5xx_transitorio_se_recupera_con_el_reintento()
    {
        var h = new ManejadorGrabado(
            ManejadorGrabado.Respuesta(HttpStatusCode.BadGateway),
            ManejadorGrabado.Respuesta(HttpStatusCode.OK, AnthropicTexto));

        var r = await Cliente(LlmProviders.Anthropic, h, new Registro()).CompletarAsync(Solicitud, CancellationToken.None);

        Assert.Equal(2, h.Llamadas);
        Assert.Equal("hola", r.Texto);
    }

    [Fact]
    [Trait("Criterio", "RES-05.d")]
    public async Task Ni_los_logs_ni_las_excepciones_contienen_la_llave()
    {
        var reg = new Registro();
        var mensajes = new List<string>();
        foreach (var proveedor in new[] { LlmProviders.Anthropic, LlmProviders.OpenAiCompatible })
        {
            ManejadorGrabado[] escenarios =
            [
                new(ManejadorGrabado.Respuesta(HttpStatusCode.Unauthorized, $"{{\"error\":\"bad key {LlaveCentinela}\"}}")),
                new(ManejadorGrabado.Respuesta(HttpStatusCode.TooManyRequests, retryAfterSegundos: 1), ManejadorGrabado.Respuesta(HttpStatusCode.BadGateway)),
                new(ManejadorGrabado.Respuesta(HttpStatusCode.BadRequest, $"echo {LlaveCentinela}")),
            ];
            foreach (var h in escenarios)
            {
                var ex = await Assert.ThrowsAsync<LlmNoDisponibleException>(() => Cliente(proveedor, h, reg).CompletarAsync(Solicitud, CancellationToken.None));
                mensajes.Add(ex.ToString());
            }

            var sinLlave = new LlmOptions { Provider = proveedor, ApiKey = null };
            var ex2 = await Assert.ThrowsAsync<LlmNoDisponibleException>(() =>
                Cliente(proveedor, new ManejadorGrabado(), reg, sinLlave).CompletarAsync(Solicitud, CancellationToken.None));
            mensajes.Add(ex2.ToString());
        }

        Assert.NotEmpty(reg.Logs);
        Assert.DoesNotContain(reg.Logs, l => l.Contains(LlaveCentinela, StringComparison.Ordinal));
        Assert.DoesNotContain(mensajes, m => m.Contains(LlaveCentinela, StringComparison.Ordinal));
        Assert.DoesNotContain(Opciones("anthropic").ToString(), LlaveCentinela, StringComparison.Ordinal);
    }

    private const string AnthropicTexto =
        """{"id":"msg_1","type":"message","role":"assistant","content":[{"type":"text","text":"hola"}],"stop_reason":"end_turn","usage":{"input_tokens":321,"output_tokens":45}}""";

    private const string AnthropicHerramienta =
        """{"id":"msg_2","type":"message","role":"assistant","content":[{"type":"text","text":"Consulto."},{"type":"tool_use","id":"toolu_01","name":"obtener_notas","input":{"estudianteId":"X-1"}}],"stop_reason":"tool_use","usage":{"input_tokens":1000,"output_tokens":200,"cache_read_input_tokens":50}}""";

    private const string OpenAiTexto =
        """{"id":"c1","object":"chat.completion","choices":[{"index":0,"message":{"role":"assistant","content":"hola"},"finish_reason":"stop"}],"usage":{"prompt_tokens":654,"completion_tokens":33,"total_tokens":687}}""";

    private const string OpenAiHerramienta =
        """{"id":"c2","object":"chat.completion","choices":[{"index":0,"message":{"role":"assistant","content":null,"tool_calls":[{"id":"call_9","type":"function","function":{"name":"obtener_notas","arguments":"{\"estudianteId\":\"X-1\"}"}}]},"finish_reason":"tool_calls"}],"usage":{"prompt_tokens":1200,"completion_tokens":150,"total_tokens":1350}}""";

    [Fact]
    [Trait("Criterio", "RES-07.c")]
    public async Task Anthropic_extrae_usage_texto_e_invocaciones_de_respuestas_grabadas()
    {
        var h = new ManejadorGrabado(
            ManejadorGrabado.Respuesta(HttpStatusCode.OK, AnthropicTexto),
            ManejadorGrabado.Respuesta(HttpStatusCode.OK, AnthropicHerramienta));
        var c = Cliente(LlmProviders.Anthropic, h, new Registro());

        var r1 = await c.CompletarAsync(Solicitud, CancellationToken.None);
        Assert.Equal(new UsoTokens(321, 45), r1.Uso);
        Assert.Equal("hola", r1.Texto);
        Assert.Empty(r1.Invocaciones);

        var r2 = await c.CompletarAsync(Solicitud, CancellationToken.None);
        Assert.Equal(new UsoTokens(1050, 200), r2.Uso);
        var inv = Assert.Single(r2.Invocaciones);
        Assert.Equal("toolu_01", inv.Id);
        Assert.Equal("obtener_notas", inv.Nombre);
        Assert.Equal("X-1", inv.Argumentos.GetProperty("estudianteId").GetString());

        Assert.Equal("https://api.anthropic.com/v1/messages", h.Solicitudes[0].RequestUri!.ToString());
        Assert.Equal(LlaveCentinela, h.Solicitudes[0].Headers.GetValues("x-api-key").Single());
        using var cuerpo = JsonDocument.Parse(h.Cuerpos[0]);
        Assert.Equal("modelo-x", cuerpo.RootElement.GetProperty("model").GetString());
        Assert.Equal(0, cuerpo.RootElement.GetProperty("temperature").GetInt32());
        Assert.Equal(500, cuerpo.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal("obtener_notas", cuerpo.RootElement.GetProperty("tools")[0].GetProperty("name").GetString());
        Assert.DoesNotContain(LlaveCentinela, h.Cuerpos[0], StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Criterio", "RES-07.c")]
    public async Task OpenAiCompatible_extrae_usage_texto_e_invocaciones_de_respuestas_grabadas()
    {
        var h = new ManejadorGrabado(
            ManejadorGrabado.Respuesta(HttpStatusCode.OK, OpenAiTexto),
            ManejadorGrabado.Respuesta(HttpStatusCode.OK, OpenAiHerramienta));
        var o = Opciones(LlmProviders.OpenAiCompatible) with { BaseUrl = "http://localhost:11434/v1/" };
        var c = Cliente(LlmProviders.OpenAiCompatible, h, new Registro(), o);

        var r1 = await c.CompletarAsync(Solicitud, CancellationToken.None);
        Assert.Equal(new UsoTokens(654, 33), r1.Uso);
        Assert.Equal("hola", r1.Texto);

        var r2 = await c.CompletarAsync(Solicitud, CancellationToken.None);
        Assert.Equal(new UsoTokens(1200, 150), r2.Uso);
        var inv = Assert.Single(r2.Invocaciones);
        Assert.Equal("call_9", inv.Id);
        Assert.Equal("obtener_notas", inv.Nombre);
        Assert.Equal("X-1", inv.Argumentos.GetProperty("estudianteId").GetString());

        Assert.Equal("http://localhost:11434/v1/chat/completions", h.Solicitudes[0].RequestUri!.ToString());
        Assert.Equal("Bearer", h.Solicitudes[0].Headers.Authorization!.Scheme);
        using var cuerpo = JsonDocument.Parse(h.Cuerpos[0]);
        Assert.Equal(0, cuerpo.RootElement.GetProperty("temperature").GetInt32());
        Assert.Equal("system", cuerpo.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("function", cuerpo.RootElement.GetProperty("tools")[0].GetProperty("type").GetString());
    }

    [Fact]
    [Trait("Criterio", "RES-07.c")]
    public async Task Los_mensajes_de_herramienta_se_traducen_al_formato_de_cada_proveedor()
    {
        var historial = new LlmRequest(
            "S",
            [
                new LlmMensaje(RolMensaje.Usuario, "q"),
                new LlmMensaje(RolMensaje.Asistente, """{"estudianteId":"X-1"}""", "id-1", "obtener_notas"),
                new LlmMensaje(RolMensaje.Herramienta, """{"d":1}""", "id-1", "obtener_notas"),
            ],
            [],
            100);
        var ha = new ManejadorGrabado(ManejadorGrabado.Respuesta(HttpStatusCode.OK, AnthropicTexto));
        await Cliente(LlmProviders.Anthropic, ha, new Registro()).CompletarAsync(historial, CancellationToken.None);
        using var a = JsonDocument.Parse(ha.Cuerpos[0]);
        var msgsA = a.RootElement.GetProperty("messages");
        Assert.Equal("tool_use", msgsA[1].GetProperty("content")[0].GetProperty("type").GetString());
        Assert.Equal("X-1", msgsA[1].GetProperty("content")[0].GetProperty("input").GetProperty("estudianteId").GetString());
        Assert.Equal("tool_result", msgsA[2].GetProperty("content")[0].GetProperty("type").GetString());
        Assert.Equal("id-1", msgsA[2].GetProperty("content")[0].GetProperty("tool_use_id").GetString());

        var ho = new ManejadorGrabado(ManejadorGrabado.Respuesta(HttpStatusCode.OK, OpenAiTexto));
        await Cliente(LlmProviders.OpenAiCompatible, ho, new Registro()).CompletarAsync(historial, CancellationToken.None);
        using var o = JsonDocument.Parse(ho.Cuerpos[0]);
        var msgsO = o.RootElement.GetProperty("messages");
        Assert.Equal("id-1", msgsO[2].GetProperty("tool_calls")[0].GetProperty("id").GetString());
        Assert.Equal("tool", msgsO[3].GetProperty("role").GetString());
        Assert.Equal("id-1", msgsO[3].GetProperty("tool_call_id").GetString());
    }

    [Fact]
    [Trait("Criterio", "RES-13.a")]
    public void El_proveedor_configurado_resuelve_su_adaptador_y_uno_desconocido_falla_con_error_claro()
    {
        var http = new HttpClient(new ManejadorGrabado());
        var anthropic = LlmClientFactory.Crear(LlmOptions.FromConfig(Cfg(("LLM_PROVIDER", "anthropic"))), http);
        var openai = LlmClientFactory.Crear(LlmOptions.FromConfig(Cfg(("LLM_PROVIDER", "openai-compatible"))), http);
        var porDefecto = LlmOptions.FromConfig(Cfg());

        Assert.IsType<AnthropicLlmClient>(anthropic);
        Assert.IsType<OpenAiCompatibleLlmClient>(openai);
        Assert.Equal("anthropic", porDefecto.Provider);
        Assert.Equal("claude-haiku-5-5", porDefecto.Model);
        Assert.Equal(8000, porDefecto.MaxTokensPerRequest);

        var ex = Assert.Throws<LlmConfiguracionException>(() => LlmOptions.FromConfig(Cfg(("LLM_PROVIDER", "gemini"))));
        Assert.Contains("LLM_PROVIDER", ex.Message, StringComparison.Ordinal);
        Assert.Contains("gemini", ex.Message, StringComparison.Ordinal);
        Assert.Contains("openai-compatible", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void La_configuracion_lee_precios_con_punto_decimal_y_tope()
    {
        var o = LlmOptions.FromConfig(Cfg(
            ("LLM_PRICE_INPUT_PER_MTOK", "1.00"), ("LLM_PRICE_OUTPUT_PER_MTOK", "5.00"), ("LLM_MAX_TOKENS_PER_REQUEST", "4000")));

        Assert.Equal(1.00m, o.PrecioEntradaPorMtok);
        Assert.Equal(5.00m, o.PrecioSalidaPorMtok);
        Assert.Equal(4000, o.MaxTokensPerRequest);
        Assert.Throws<LlmConfiguracionException>(() => LlmOptions.FromConfig(Cfg(("LLM_PRICE_INPUT_PER_MTOK", "abc"))));
    }

    private static Func<string, string?> Cfg(params (string, string)[] valores)
    {
        var d = valores.ToDictionary(v => v.Item1, v => v.Item2, StringComparer.Ordinal);
        return k => d.GetValueOrDefault(k);
    }
}
