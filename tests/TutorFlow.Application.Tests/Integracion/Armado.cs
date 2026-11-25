using System.Text.Json;
using TutorFlow.Application.Audit;
using TutorFlow.Application.Identity;
using TutorFlow.Application.Llm;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Preguntar;
using TutorFlow.Application.Retrieval;
using TutorFlow.Application.Tests.Retrieval;
using TutorFlow.Application.Tests.Tools;
using TutorFlow.Application.Tools;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Tests.Integracion;

/// <summary>LLM guionizado del flujo integrado (solo tests, A-11). Cada llamada consume un paso; el ultimo se repite.</summary>
internal sealed class LlmEscrito(params Func<LlmRequest, LlmResponse>[] pasos) : ILlmClient
{
    private int _n;

    public List<LlmRequest> Solicitudes { get; } = [];

    public async Task<LlmResponse> CompletarAsync(LlmRequest solicitud, CancellationToken ct)
    {
        Solicitudes.Add(solicitud);
        var paso = pasos[Math.Min(_n++, pasos.Length - 1)];
        await Task.Yield();
        return paso(solicitud);
    }

    public static Func<LlmRequest, LlmResponse> Invoca(int entrada, int salida, params (string Nombre, string ArgsJson)[] invocaciones) =>
        _ => new LlmResponse(
            string.Empty,
            invocaciones.Select((x, i) => new InvocacionPropuesta($"call-{i + 1}", x.Nombre, Json(x.ArgsJson))).ToList(),
            new UsoTokens(entrada, salida));

    public static Func<LlmRequest, LlmResponse> Invoca(string nombre, string argsJson = "{}") => Invoca(100, 20, (nombre, argsJson));

    public static Func<LlmRequest, LlmResponse> Final(string respuesta, string[]? chunkIds = null, int entrada = 200, int salida = 30) =>
        _ => new LlmResponse(
            JsonSerializer.Serialize(new { respuesta, chunkIdsCitados = chunkIds ?? [] }),
            [],
            new UsoTokens(entrada, salida));

    public static Func<LlmRequest, LlmResponse> Falla(Exception ex) => _ => throw ex;

    public static JsonElement Json(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public static string Prompt(LlmRequest r) =>
        r.Sistema + "\n" + string.Join("\n", r.Mensajes.Select(m => m.Contenido)) + "\n" +
        string.Join("\n", r.Herramientas.Select(t => t.Nombre + t.Descripcion + t.EsquemaArgumentos.GetRawText()));

    /// <summary>Contenido de la herramienta n-esima que recibio el LLM en su ultima solicitud.</summary>
    public JsonDocument Herramienta(int indice = 0) =>
        JsonDocument.Parse(Solicitudes.Last().Mensajes.Where(m => m.Rol == RolMensaje.Herramienta).ElementAt(indice).Contenido);
}

internal sealed class ValidadorFijo(PrincipalAcademico principal) : IValidadorIdentidad
{
    public Task<PrincipalAcademico> ValidarAsync(string? rol, string? id, CancellationToken ct) => Task.FromResult(principal);
}

internal sealed class RetrieverEspia(IRetriever interno) : IRetriever
{
    public int Llamadas { get; private set; }

    public async Task<IReadOnlyList<Fragmento>> RecuperarAsync(string pregunta, CancellationToken ct)
    {
        Llamadas++;
        return await interno.RecuperarAsync(pregunta, ct);
    }
}

/// <summary>Pieza completa del flujo: caso de uso real + generador real (IA) + despachador real (Core) + datos del material.</summary>
internal sealed class Montaje
{
    public Montaje(PrincipalAcademico principal, LlmEscrito llm, IRetriever? retriever = null, TimeSpan? timeout = null)
    {
        Principal = principal;
        Llm = llm;
        Fuente = new FuenteEspia();
        Roster = new RosterEnMemoria();
        Contexto = new ContextoAuditoria();
        var catalogo = CorpusHelper.DefaultCatalog();
        Retriever = new RetrieverEspia(retriever ?? new FullContextRetriever(catalogo));
        Despachador = new ToolDispatcher(Fuente, Roster, new AuthorizationPolicy(), Contexto);
        Generador = new GeneradorRespuesta(llm, new GeneradorOptions(), catalogo);
        Caso = new PreguntarAsistente(
            new ValidadorFijo(principal),
            Roster,
            () => Retriever,
            () => Generador,
            Despachador,
            new PreguntarOpciones(timeout ?? PreguntarOpciones.TimeoutPorDefecto),
            new InfoFlujoLlm("full-context", "modelo-de-prueba"),
            Contexto);
    }

    public PrincipalAcademico Principal { get; }

    public LlmEscrito Llm { get; }

    public FuenteEspia Fuente { get; }

    public RosterEnMemoria Roster { get; }

    public ContextoAuditoria Contexto { get; }

    public RetrieverEspia Retriever { get; }

    public ToolDispatcher Despachador { get; }

    public GeneradorRespuesta Generador { get; }

    public PreguntarAsistente Caso { get; }

    public Task<PreguntarResultado> PreguntarAsync(string pregunta, CancellationToken ct = default) =>
        Caso.EjecutarAsync(new PreguntarComando(pregunta, "x", "x", "req-1"), ct);

    /// <summary>Invoca el bucle y el despachador directamente (sin L2a/L2b), como piden los criterios de contencion.</summary>
    public Task<RespuestaGenerada> BucleDirectoAsync(string pregunta, CancellationToken ct = default) =>
        Generador.GenerarAsync(Principal, pregunta, [], Despachador, ct);

    public EventoPregunta Evento(int resultado = 200) => Contexto.ConstruirUnaVez("req-1", DateTimeOffset.UnixEpoch, resultado, 1)!;
}
