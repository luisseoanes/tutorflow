using System.Text.Json;
using Microsoft.Extensions.Logging;
using TutorFlow.Application.Ports;
using TutorFlow.Infrastructure.Audit;

namespace TutorFlow.Api.Tests.Auditoria;

/// <summary>Destinos y forma del evento (SPEC 9.2 y 9.3) sobre el escritor real, con stdout inyectado.</summary>
public class JsonLineAuditLogTests
{
    internal sealed class CapturaLogs : ILoggerProvider
    {
        public List<(LogLevel Nivel, string Categoria, string Mensaje)> Lineas { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Registro(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class Registro(CapturaLogs padre, string categoria) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                padre.Lineas.Add((logLevel, categoria, formatter(state, exception)));
        }
    }

    internal sealed class ArchivoQueFalla : IAuditoriaArchivo
    {
        public int Intentos { get; private set; }

        public Task AgregarLineaAsync(string linea, CancellationToken ct)
        {
            Intentos++;
            throw new IOException("disco lleno");
        }
    }

    private static readonly DateTimeOffset Ts = new(2026, 1, 2, 3, 4, 5, 678, TimeSpan.Zero);

    private static EventoPregunta Evento(string pregunta = "hola") => new(
        Ts,
        "req-1",
        new UsuarioAuditado("estudiante", "EST-001"),
        true,
        pregunta,
        ["EST-001"],
        [],
        [new HerramientaAuditada("obtener_notas", "EST-001", "permitido")],
        200,
        null,
        ["evaluacion_y_calificaciones.md#Sección 3"],
        "bm25",
        "modelo-x",
        1234,
        0.0012m,
        1830,
        []);

    internal static JsonLineAuditLog Crear(StringWriter stdout, IAuditoriaArchivo? archivo = null, CapturaLogs? logs = null)
    {
        var fabrica = LoggerFactory.Create(b =>
        {
            if (logs is not null)
            {
                b.AddProvider(logs);
            }
        });
        return new JsonLineAuditLog(new AuditoriaStdout(stdout), archivo, fabrica);
    }

    [Fact]
    [Trait("Contrato", "SPEC-9.3")]
    public async Task Roster_cargado_sale_como_linea_JSON_pura_en_stdout_sin_ids_ni_nombres()
    {
        var stdout = new StringWriter();
        var audit = Crear(stdout);

        await audit.RegistrarRosterCargadoAsync(new EventoRosterCargado(Ts, 12, 3, "ok"), CancellationToken.None);

        var linea = Assert.Single(stdout.ToString().TrimEnd('\n').Split('\n'));
        using var doc = JsonDocument.Parse(linea);
        Assert.Equal("roster.cargado", doc.RootElement.GetProperty("evento").GetString());
        Assert.Equal("2026-01-02T03:04:05.678Z", doc.RootElement.GetProperty("ts").GetString());
        Assert.Equal(12, doc.RootElement.GetProperty("estudiantes").GetInt32());
        Assert.Equal(3, doc.RootElement.GetProperty("programas").GetInt32());
        Assert.Equal("ok", doc.RootElement.GetProperty("resultado").GetString());
        Assert.Equal(
            ["evento", "ts", "estudiantes", "programas", "resultado"],
            doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
    }

    [Fact]
    [Trait("Contrato", "SPEC-9.2")]
    public async Task Pregunta_sale_como_una_linea_JSON_propia_con_los_campos_de_SPEC_9_2_y_sin_mas()
    {
        var stdout = new StringWriter();
        var audit = Crear(stdout);

        await audit.RegistrarPreguntaAsync(Evento(), CancellationToken.None);

        var linea = Assert.Single(stdout.ToString().TrimEnd('\n').Split('\n'));
        Assert.StartsWith("{\"evento\":\"pregunta\"", linea);
        using var doc = JsonDocument.Parse(linea);
        var raiz = doc.RootElement;
        Assert.Equal(
            [
                "evento", "ts", "requestId", "usuario", "identidadValida", "pregunta", "estudiantesConsultados",
                "programasConsultados", "objetivosDenegados", "herramientas", "resultado", "motivoDenegacion", "citas",
                "estrategia", "modelo", "tokens", "costoUsd", "latenciaMs",
            ],
            raiz.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("EST-001", raiz.GetProperty("usuario").GetProperty("id").GetString());
        Assert.Equal("obtener_notas", raiz.GetProperty("herramientas")[0].GetProperty("nombre").GetString());
        Assert.Equal(JsonValueKind.Null, raiz.GetProperty("motivoDenegacion").ValueKind);
        Assert.Equal(0.0012m, raiz.GetProperty("costoUsd").GetDecimal());
    }

    [Fact]
    [Trait("Contrato", "SPEC-9.2")]
    public async Task Pregunta_con_saltos_de_linea_y_comillas_sigue_siendo_una_sola_linea_y_se_trunca_a_1000()
    {
        var stdout = new StringWriter();
        var audit = Crear(stdout);
        var pregunta = "linea1\n\"linea2\"\r\n" + new string('a', 1500);

        await audit.RegistrarPreguntaAsync(Evento(pregunta), CancellationToken.None);

        var linea = Assert.Single(stdout.ToString().TrimEnd('\n').Split('\n'));
        using var doc = JsonDocument.Parse(linea);
        var guardada = doc.RootElement.GetProperty("pregunta").GetString()!;
        Assert.Equal(1000, guardada.Length);
        Assert.Equal(pregunta[..1000], guardada);
    }

    [Fact]
    [Trait("Contrato", "SPEC-9.2")]
    public async Task Usuario_no_legible_se_registra_como_null()
    {
        var stdout = new StringWriter();
        var audit = Crear(stdout);

        await audit.RegistrarPreguntaAsync(Evento() with { Usuario = null }, CancellationToken.None);

        using var doc = JsonDocument.Parse(stdout.ToString().Trim());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("usuario").ValueKind);
    }

    [Fact]
    [Trait("Contrato", "SPEC-9.3")]
    public async Task Archivo_secundario_recibe_la_misma_linea_que_stdout()
    {
        var ruta = Path.Combine(Path.GetTempPath(), "tutorflow-audit-" + Guid.NewGuid().ToString("N"), "sub", "audit.jsonl");
        try
        {
            var stdout = new StringWriter();
            var audit = Crear(stdout, new AuditoriaArchivo(ruta));

            await audit.RegistrarPreguntaAsync(Evento(), CancellationToken.None);
            await audit.RegistrarRosterCargadoAsync(new EventoRosterCargado(Ts, 1, 1, "ok"), CancellationToken.None);

            Assert.Equal(stdout.ToString(), await File.ReadAllTextAsync(ruta));
        }
        finally
        {
            var raiz = Path.GetDirectoryName(Path.GetDirectoryName(ruta))!;
            if (Directory.Exists(raiz))
            {
                Directory.Delete(raiz, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Contrato", "SPEC-9.3")]
    public async Task Fallo_del_archivo_no_lanza_escribe_stdout_y_registra_un_error_sin_el_contenido()
    {
        var stdout = new StringWriter();
        var logs = new CapturaLogs();
        var archivo = new ArchivoQueFalla();
        var audit = Crear(stdout, archivo, logs);

        await audit.RegistrarPreguntaAsync(Evento("secreto-en-la-pregunta"), CancellationToken.None);

        Assert.Equal(1, archivo.Intentos);
        Assert.Contains("secreto-en-la-pregunta", stdout.ToString());
        var error = Assert.Single(logs.Lineas);
        Assert.Equal(LogLevel.Error, error.Nivel);
        Assert.DoesNotContain("secreto-en-la-pregunta", error.Mensaje);
    }
}
