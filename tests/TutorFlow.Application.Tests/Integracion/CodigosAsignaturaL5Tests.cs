using TutorFlow.Application.Audit;
using TutorFlow.Application.Errores;
using TutorFlow.Application.Llm;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Preguntar;
using TutorFlow.Application.Retrieval;
using TutorFlow.Application.Tests.Retrieval;
using TutorFlow.Application.Tests.Tools;
using TutorFlow.Application.Tools;
using TutorFlow.Domain.Authorization;
using static TutorFlow.Application.Tests.Tools.MaterialDatos;

namespace TutorFlow.Application.Tests.Integracion;

/// <summary>
/// T-05f: L5 no debe confundir un codigo de asignatura devuelto por las herramientas de la misma peticion (p. ej. "EST-103")
/// con un id de estudiante (SPEC 6.2 L5). Un estudiante real fuera de alcance sigue marcandose (ADV-13.a).
/// Sin criterio nuevo: regresion del defecto hallado con el LLM real.
/// </summary>
public class CodigosAsignaturaL5Tests
{
    private const string Perdidas = "asignaturas_perdidas_programa";

    [Fact]
    public async Task Coordinador_con_codigo_de_asignatura_con_forma_de_id_en_la_respuesta_recibe_200()
    {
        var m = new Montaje(Coord2(), new LlmEscrito(
            LlmEscrito.Invoca(Perdidas),
            LlmEscrito.Final("Hay estudiantes con Estadística (EST-103) perdida.")));

        var r = await m.PreguntarAsync("¿Qué estudiantes de mi programa tienen alguna asignatura perdida?");

        Assert.Contains("EST-103", r.Respuesta);
        Assert.True(r.UsoDatosEstudiante);
        using var h = m.Llm.Herramienta();
        Assert.Contains("EST-103", h.RootElement.GetRawText());
    }

    [Fact]
    public async Task Codigo_de_asignatura_mas_un_estudiante_de_otro_programa_sigue_dando_403_salida()
    {
        var m = new Montaje(Coord2(), new LlmEscrito(
            LlmEscrito.Invoca(Perdidas),
            LlmEscrito.Final("Estadística (EST-103) la perdió también EST-002.")));

        var ex = await Assert.ThrowsAsync<AccesoDenegadoException>(() => m.PreguntarAsync("¿Qué estudiantes de mi programa tienen alguna asignatura perdida?"));

        Assert.Equal("salida", ex.Motivo);
        Assert.Equal(["EST-002"], m.Evento(403).ObjetivosDenegados);
    }

    [Fact]
    public async Task Codigo_de_asignatura_no_devuelto_por_ninguna_herramienta_sigue_marcandose()
    {
        var m = new Montaje(Coord2(), new LlmEscrito(LlmEscrito.Final("Estadística (EST-103).", ["reglamento_academico_2025.md#Artículo 5"])));

        var ex = await Assert.ThrowsAsync<AccesoDenegadoException>(() => m.PreguntarAsync("¿Cómo está el programa?"));

        Assert.Equal("salida", ex.Motivo);
    }

    [Fact]
    public async Task La_pregunta_con_forma_de_id_inexistente_sigue_dando_403_referencia_por_L2a()
    {
        var m = new Montaje(Coord2(), new LlmEscrito(LlmEscrito.Final("x")));

        var ex = await Assert.ThrowsAsync<AccesoDenegadoException>(() => m.PreguntarAsync("¿Cómo va EST-103?"));

        Assert.Equal("referencia", ex.Motivo);
        Assert.Empty(m.Llm.Solicitudes);
    }

    // ---- RES-12: datos y roster alternativos (ids y codigos distintos de los del material)

    [Fact]
    public async Task Con_datos_alternativos_con_codigo_de_asignatura_con_prefijo_de_estudiante_el_codigo_pasa_y_un_alumno_ajeno_no()
    {
        var ok = await PreguntarAltAsync("Matemática (ALU-55) perdida por un alumno de tu programa.");
        Assert.Contains("ALU-55", ok.Respuesta);

        var ex = await Assert.ThrowsAsync<AccesoDenegadoException>(() => PreguntarAltAsync("Matemática (ALU-55) y ALU-11."));
        Assert.Equal("salida", ex.Motivo);
    }

    private static Task<PreguntarResultado> PreguntarAltAsync(string respuestaFinal)
    {
        var llm = new LlmEscrito(LlmEscrito.Invoca(Perdidas), LlmEscrito.Final(respuestaFinal));
        var fuente = new FuenteAlterna();
        var roster = new RosterAlterno();
        var catalogo = CorpusHelper.DefaultCatalog();
        var caso = new PreguntarAsistente(
            new ValidadorFijo(PrincipalAlterno),
            roster,
            () => new FullContextRetriever(catalogo),
            () => new GeneradorRespuesta(llm, new GeneradorOptions(), catalogo),
            new ToolDispatcher(fuente, roster, new AuthorizationPolicy()),
            PreguntarOpciones.PorDefecto,
            new InfoFlujoLlm("full-context", "modelo-de-prueba"));
        return caso.EjecutarAsync(new PreguntarComando("¿Quiénes pierden alguna asignatura?", "x", "x", "req-1"), CancellationToken.None);
    }

    private static readonly PrincipalAcademico PrincipalAlterno = new(RolAcademico.Coordinador, "DIR-9", ["P1"]);

    private sealed class RosterAlterno : IRosterProvider
    {
        public Task<RosterSnapshot> ObtenerAsync(CancellationToken ct) => Task.FromResult(new RosterSnapshot(
            [new RosterEstudiante("ALU-10", "zoe quintero", "P1"), new RosterEstudiante("ALU-11", "ivan prado", "P2")],
            [new RosterCoordinador("DIR-9")],
            [new RosterPrograma("P1", "programa uno"), new RosterPrograma("P2", "programa dos")],
            DateTimeOffset.UnixEpoch));
    }

    private sealed class FuenteAlterna : IAcademicDataSource
    {
        private static readonly EstudianteFuente[] Todos =
            [new("ALU-10", "Zoe Quintero", "P1", 1), new("ALU-11", "Ivan Prado", "P2", 1)];

        public Task<EstudianteFuente?> ObtenerEstudianteAsync(string estudianteId, CancellationToken ct) =>
            Task.FromResult(Todos.FirstOrDefault(e => e.Id == estudianteId));

        public Task<IReadOnlyList<NotaFuente>> ObtenerNotasAsync(string estudianteId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<NotaFuente>>([new NotaFuente(estudianteId, "ALU-55", "Matemática", 3, 1.0m, 1.0m, 1.0m)]);

        public Task<IReadOnlyList<AsistenciaFuente>> ObtenerAsistenciaAsync(string estudianteId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AsistenciaFuente>>([new AsistenciaFuente(estudianteId, "ALU-55", "Matemática", 48, 0)]);

        public Task<CoordinadorFuente?> ObtenerCoordinadorAsync(string coordinadorId, CancellationToken ct) =>
            Task.FromResult<CoordinadorFuente?>(null);

        public Task<IReadOnlyList<ProgramaFuente>> ListarProgramasAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProgramaFuente>>([]);

        public Task<IReadOnlyList<EstudianteFuente>> ListarEstudiantesProgramaAsync(string programaId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EstudianteFuente>>(Todos.Where(e => e.ProgramaId == programaId).ToList());
    }
}
