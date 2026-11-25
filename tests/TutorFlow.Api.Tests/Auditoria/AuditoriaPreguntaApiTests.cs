using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TutorFlow.Api.Tests.Infra;
using TutorFlow.Application.Errores;
using TutorFlow.Application.Ports;
using TutorFlow.Infrastructure.Audit;
using TutorFlow.Infrastructure.Roster;
using static TutorFlow.Api.Tests.Infra.Asercion;

namespace TutorFlow.Api.Tests.Auditoria;

/// <summary>
/// Evento <c>pregunta</c> (SPEC 9.2) por HTTP: exactamente uno por peticion en cada camino, con el motivo, el objetivo
/// intentado y SIN consultas atribuibles al roster ni a L1. Roster y datos sinteticos de prueba.
/// </summary>
public class AuditoriaPreguntaApiTests
{
    private const string PreguntaAjena = "¿Cuál es la nota de Bases de Datos de Andrés Martínez?";

    private static RosterSnapshot RosterDePrueba() => new(
        [
            new("EST-001", "laura gomez", "ING-SIS"),
            new("EST-002", "andres martinez", "ING-SIS"),
            new("EST-003", "sofia herrera", "ING-SIS"),
            new("EST-004", "camilo rojas", "ADM"),
        ],
        [new("COORD-01"), new("COORD-02")],
        [new("ING-SIS", "ingenieria de sistemas"), new("ADM", "administracion de empresas")],
        DateTimeOffset.UnixEpoch);

    private static FabricaApi Crear()
    {
        var f = new FabricaApi();
        f.Roster.Snapshot = RosterDePrueba();
        return f;
    }

    private static EventoPregunta Unico(FabricaApi f) => Assert.Single(f.Auditoria.Preguntas);

    [Fact]
    [Trait("Criterio", "AUTH-03.d")]
    public async Task Pedir_la_nota_de_otro_por_nombre_audita_referencia_con_el_objetivo_EST_002_y_sin_consultas()
    {
        using var f = Crear();

        var r = await PostAsync(f.CreateClient(), ConUsuario(Usuario("estudiante", "EST-001"), PreguntaAjena));

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        var e = Unico(f);
        Assert.Equal(403, e.Resultado);
        Assert.Equal("referencia", e.MotivoDenegacion);
        Assert.Equal(["EST-002"], e.ObjetivosDenegados);
        Assert.Empty(e.EstudiantesConsultados);
        Assert.Empty(e.ProgramasConsultados);
        Assert.Empty(e.Herramientas);
        Assert.Equal(PreguntaAjena, e.Pregunta);
        Assert.Equal(new UsuarioAuditado("estudiante", "EST-001"), e.Usuario);
        Assert.True(e.IdentidadValida);
        Assert.Equal(0, e.Tokens);
    }

    [Fact]
    [Trait("Criterio", "AUTH-08.b")]
    public async Task Coordinador_con_el_id_de_un_estudiante_audita_identidad_sin_herramientas_ni_llm()
    {
        var llm = new LlmEspia();
        using var f = new FabricaApi(s => s.AddSingleton<ILlmClient>(llm));
        f.Roster.Snapshot = RosterDePrueba();

        var r = await PostAsync(
            f.CreateClient(),
            ConUsuario(Usuario("coordinador", "EST-001"), "¿Qué estudiantes de mi programa tienen materias perdidas?"));

        await Afirmar403IdentidadAsync(r);
        Assert.Equal(0, llm.Invocaciones);
        var e = Unico(f);
        Assert.Equal(403, e.Resultado);
        Assert.Equal("identidad", e.MotivoDenegacion);
        Assert.False(e.IdentidadValida);
        Assert.Empty(e.Herramientas);
        Assert.Empty(e.EstudiantesConsultados);
        Assert.Empty(e.ProgramasConsultados);
    }

    [Fact]
    [Trait("Criterio", "RES-04.b")]
    public async Task Fuente_de_datos_caida_responde_503_y_la_auditoria_registra_resultado_503()
    {
        using var f = Crear();
        f.Fuente.Fallo = _ => new DependenciaNoDisponibleException("timeout simulado");

        var r = await PostAsync(f.CreateClient(), ConUsuario(Usuario("estudiante", "EST-001")));

        await AfirmarProblema(r, HttpStatusCode.ServiceUnavailable);
        var e = Unico(f);
        Assert.Equal(503, e.Resultado);
        Assert.Null(e.MotivoDenegacion);
        Assert.Empty(e.EstudiantesConsultados);
    }

    [Fact]
    [Trait("Criterio", "AUTH-24.b")]
    public async Task Los_ids_leidos_solo_para_el_roster_no_entran_a_estudiantesConsultados_y_roster_cargado_no_lleva_ids_ni_nombres()
    {
        var stdout = new StringWriter();
        RosterProvider? roster = null;
        using var f = new FabricaApi(s =>
        {
            s.RemoveAll<IAuditLog>();
            s.AddSingleton<IAuditLog>(sp => new JsonLineAuditLog(
                new AuditoriaStdout(stdout), null, sp.GetRequiredService<ILoggerFactory>()));
            s.RemoveAll<IRosterProvider>();
            s.AddSingleton<IRosterProvider>(sp => roster!);
        });
        var fuente = f.Fuente;
        fuente.Programas.Add(new ProgramaFuente("ING-SIS", "Ingeniería de Sistemas", "COORD-01"));
        fuente.Estudiantes["EST-001"] = new("EST-001", "Laura Gómez", "ING-SIS", 3);
        fuente.Estudiantes["EST-002"] = new("EST-002", "Andrés Martínez", "ING-SIS", 3);
        fuente.Estudiantes["EST-003"] = new("EST-003", "Sofía Herrera", "ING-SIS", 3);
        var audit = (IAuditLog)f.Services.GetRequiredService<IAuditLog>();
        roster = new RosterProvider(fuente, audit, new RelojFijo(), TimeSpan.FromMinutes(5));
        Assert.True(await roster.CargarAsync(CancellationToken.None));

        var r = await PostAsync(f.CreateClient(), ConUsuario(Usuario("estudiante", "EST-001"), PreguntaAjena));

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        var lineas = stdout.ToString().TrimEnd('\n').Split('\n');
        Assert.Equal(2, lineas.Length);
        using var carga = JsonDocument.Parse(lineas[0]);
        Assert.Equal("roster.cargado", carga.RootElement.GetProperty("evento").GetString());
        foreach (var prohibido in new[] { "EST-00", "Laura", "laura", "Sofía", "sofia", "Andrés", "andres", "Martínez" })
        {
            Assert.DoesNotContain(prohibido, lineas[0]);
        }

        using var pregunta = JsonDocument.Parse(lineas[1]);
        Assert.Equal("pregunta", pregunta.RootElement.GetProperty("evento").GetString());
        Assert.Equal(0, pregunta.RootElement.GetProperty("estudiantesConsultados").GetArrayLength());
        Assert.Equal(0, pregunta.RootElement.GetProperty("programasConsultados").GetArrayLength());
        Assert.True(fuente.Estudiantes.Count >= 3);
    }

    [Fact]
    [Trait("Criterio", "RES-08.b")]
    public async Task Archivo_de_auditoria_que_falla_no_altera_la_respuesta_y_registra_un_error_de_operacion()
    {
        var stdout = new StringWriter();
        var logs = new JsonLineAuditLogTests.CapturaLogs();
        var archivo = new JsonLineAuditLogTests.ArchivoQueFalla();
        using var f = new FabricaApi(s =>
        {
            s.RemoveAll<IAuditLog>();
            s.AddSingleton<IAuditLog>(new JsonLineAuditLog(
                new AuditoriaStdout(stdout),
                archivo,
                LoggerFactory.Create(b => b.AddProvider(logs))));
        });

        // Un 200 requiere el flujo LLM de T-08: se usa el camino 403 disponible (misma auditoria, mismo destino).
        var r = await PostAsync(f.CreateClient(), ConUsuario(Usuario("estudiante", "EST-999")));

        await Afirmar403IdentidadAsync(r);
        Assert.Equal(1, archivo.Intentos);
        var linea = Assert.Single(stdout.ToString().TrimEnd('\n').Split('\n'));
        using var doc = JsonDocument.Parse(linea);
        Assert.Equal("pregunta", doc.RootElement.GetProperty("evento").GetString());
        Assert.Equal(403, doc.RootElement.GetProperty("resultado").GetInt32());
        var error = Assert.Single(logs.Lineas, l => l.Nivel == LogLevel.Error);
        Assert.Equal(JsonLineAuditLog.CategoriaLogger, error.Categoria);
    }

    // ---- Un evento por camino (SPEC 9.1), sin criterio nuevo ----

    [Fact]
    public async Task Cuerpo_invalido_400_emite_un_evento_con_usuario_null_y_resultado_400()
    {
        using var f = Crear();

        var r = await PostAsync(f.CreateClient(), "{no es json");

        await AfirmarProblema(r, HttpStatusCode.BadRequest);
        var e = Unico(f);
        Assert.Equal(400, e.Resultado);
        Assert.Null(e.Usuario);
        Assert.False(e.IdentidadValida);
        Assert.Null(e.MotivoDenegacion);
        Assert.NotEmpty(e.RequestId);
    }

    [Fact]
    public async Task Content_type_no_json_400_emite_un_evento()
    {
        using var f = Crear();

        var r = await PostAsync(f.CreateClient(), "x", "text/plain");

        await AfirmarProblema(r, HttpStatusCode.BadRequest);
        Assert.Equal(400, Unico(f).Resultado);
    }

    [Fact]
    public async Task Identidad_403_emite_un_evento_con_el_usuario_declarado_y_la_pregunta_literal()
    {
        using var f = Crear();

        var r = await PostAsync(f.CreateClient(), ConUsuario(Usuario("estudiante", "EST-999"), "¿Mi promedio?"));

        await Afirmar403IdentidadAsync(r);
        var e = Unico(f);
        Assert.Equal(new UsuarioAuditado("estudiante", "EST-999"), e.Usuario);
        Assert.Equal("¿Mi promedio?", e.Pregunta);
        Assert.Equal("identidad", e.MotivoDenegacion);
    }

    [Fact]
    public async Task Identidad_con_rol_ilegible_403_emite_un_evento_con_usuario_null()
    {
        using var f = Crear();

        var r = await PostAsync(f.CreateClient(), ConUsuario("{\"rol\":42,\"id\":\"EST-001\"}"));

        await Afirmar403IdentidadAsync(r);
        var e = Unico(f);
        Assert.Null(e.Usuario);
        Assert.Equal("identidad", e.MotivoDenegacion);
    }

    [Fact]
    public async Task Acceso_403_por_L2b_emite_un_evento_con_motivo_referencia()
    {
        using var f = Crear();

        var r = await PostAsync(
            f.CreateClient(),
            ConUsuario(Usuario("estudiante", "EST-001"), "Muéstrame las notas de otros estudiantes"));

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        var e = Unico(f);
        Assert.Equal("referencia", e.MotivoDenegacion);
        Assert.Empty(e.ObjetivosDenegados ?? []);
        Assert.Empty(e.EstudiantesConsultados);
    }

    [Fact]
    public async Task Roster_caido_503_emite_un_evento_con_identidad_valida()
    {
        using var f = Crear();
        f.Roster.Caido = true;

        var r = await PostAsync(f.CreateClient(), ConUsuario(Usuario("estudiante", "EST-001")));

        await AfirmarProblema(r, HttpStatusCode.ServiceUnavailable);
        var e = Unico(f);
        Assert.Equal(503, e.Resultado);
        Assert.True(e.IdentidadValida);
    }

    [Fact]
    public async Task Flujo_pendiente_503_emite_un_evento()
    {
        using var f = Crear();

        var r = await PostAsync(f.CreateClient(), ConUsuario(Usuario("estudiante", "EST-001"), "¿Cuál es mi promedio?"));

        await AfirmarProblema(r, HttpStatusCode.ServiceUnavailable);
        var e = Unico(f);
        Assert.Equal(503, e.Resultado);
        Assert.True(e.IdentidadValida);
        Assert.Empty(e.EstudiantesConsultados);
    }

    [Fact]
    public async Task Error_no_controlado_500_emite_un_evento()
    {
        using var f = Crear();
        f.Fuente.Fallo = _ => new InvalidOperationException("fallo interno");

        var r = await PostAsync(f.CreateClient(), ConUsuario(Usuario("estudiante", "EST-001")));

        await AfirmarProblema(r, HttpStatusCode.InternalServerError);
        Assert.Equal(500, Unico(f).Resultado);
    }

    [Fact]
    public async Task Una_peticion_a_otra_ruta_no_emite_evento_de_pregunta()
    {
        using var f = Crear();

        var r = await f.CreateClient().GetAsync("/api/salud");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Empty(f.Auditoria.Preguntas);
    }

    [Fact]
    public async Task Un_fallo_del_log_de_auditoria_no_cambia_la_respuesta()
    {
        using var f = new FabricaApi(s =>
        {
            s.RemoveAll<IAuditLog>();
            s.AddSingleton<IAuditLog, AuditoriaQueLanza>();
        });

        var r = await PostAsync(f.CreateClient(), ConUsuario(Usuario("estudiante", "EST-999")));

        await Afirmar403IdentidadAsync(r);
    }

    private sealed class AuditoriaQueLanza : IAuditLog
    {
        public Task RegistrarPreguntaAsync(EventoPregunta evento, CancellationToken ct) => throw new IOException("auditoria caida");

        public Task RegistrarRosterCargadoAsync(EventoRosterCargado evento, CancellationToken ct) => throw new IOException("auditoria caida");
    }

    private sealed class RelojFijo : IClock
    {
        public DateTimeOffset UtcNow { get; } = DateTimeOffset.UtcNow;
    }
}
