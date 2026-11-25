using System.Net;
using System.Text;
using System.Text.Json;
using TutorFlow.Application.Ports;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Tests.Llm;

internal static class Dobles
{
    public const string LlaveCentinela = "test-key-NOT-REAL";

    public static JsonElement Json(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public static PrincipalAcademico Estudiante() => new(RolAcademico.Estudiante, "X-1");

    public static PrincipalAcademico Coordinador() => new(RolAcademico.Coordinador, "Y-1", ["P-1"]);

    public static string Salida(string respuesta, params string[] chunkIds) =>
        JsonSerializer.Serialize(new { respuesta, chunkIdsCitados = chunkIds });

    public static LlmResponse Texto(string texto, int entrada = 100, int salida = 20) =>
        new(texto, [], new UsoTokens(entrada, salida));

    public static LlmResponse Invoca(string nombre, string argsJson = "{}", int entrada = 100, int salida = 20, string? id = "call-1") =>
        new(string.Empty, [new InvocacionPropuesta(id, nombre, Json(argsJson))], new UsoTokens(entrada, salida));

    /// <summary>Todo el texto que el LLM llega a ver en una solicitud (sistema, mensajes y herramientas).</summary>
    public static string Prompt(LlmRequest r) =>
        r.Sistema + "\n" + string.Join("\n", r.Mensajes.Select(m => m.Contenido)) + "\n" +
        string.Join("\n", r.Herramientas.Select(t => t.Nombre + t.Descripcion + t.EsquemaArgumentos.GetRawText()));
}

/// <summary>Doble guionizado de ILlmClient. Cuando se agota el guion repite la ultima respuesta.</summary>
internal sealed class LlmGuionizado(params LlmResponse[] guion) : ILlmClient
{
    private int _n;

    public List<LlmRequest> Solicitudes { get; } = [];

    public Task<LlmResponse> CompletarAsync(LlmRequest solicitud, CancellationToken ct)
    {
        Solicitudes.Add(solicitud);
        var r = guion[Math.Min(_n, guion.Length - 1)];
        _n++;
        return Task.FromResult(r);
    }
}

internal sealed class DespachadorGuionizado : IToolDispatcher
{
    private readonly Func<InvocacionPropuesta, ResultadoHerramienta> _resultado;

    public DespachadorGuionizado(Func<InvocacionPropuesta, ResultadoHerramienta>? resultado = null)
    {
        _resultado = resultado ?? (_ => new ResultadoHerramienta("{}", [], false, null));
    }

    public List<InvocacionPropuesta> Despachadas { get; } = [];

    public PrincipalAcademico? PrincipalRecibido { get; private set; }

    public IReadOnlyList<ToolDefinition> HerramientasExpuestas(PrincipalAcademico principal) =>
    [
        new ToolDefinition("obtener_notas", "Notas del estudiante.", Dobles.Json("""{"type":"object","properties":{"estudianteId":{"type":"string"}},"additionalProperties":false}""")),
    ];

    public Task<ResultadoHerramienta> DespacharAsync(PrincipalAcademico principal, InvocacionPropuesta invocacion, CancellationToken ct)
    {
        PrincipalRecibido = principal;
        Despachadas.Add(invocacion);
        return Task.FromResult(_resultado(invocacion));
    }
}

/// <summary>Respuestas HTTP grabadas: sin red. Cada llamada consume la siguiente; la ultima se repite.</summary>
internal sealed class ManejadorGrabado(params Func<HttpResponseMessage>[] respuestas) : HttpMessageHandler
{
    public List<HttpRequestMessage> Solicitudes { get; } = [];

    public List<string> Cuerpos { get; } = [];

    public int Llamadas => Solicitudes.Count;

    public static Func<HttpResponseMessage> Respuesta(HttpStatusCode codigo, string cuerpo = "{}", int? retryAfterSegundos = null) => () =>
    {
        var r = new HttpResponseMessage(codigo) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };
        if (retryAfterSegundos is { } s)
        {
            r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(s));
        }

        return r;
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Solicitudes.Add(request);
        Cuerpos.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
        return respuestas[Math.Min(Solicitudes.Count - 1, respuestas.Length - 1)]();
    }
}
