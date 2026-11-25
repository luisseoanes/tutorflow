using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TutorFlow.Application.Ports;

namespace TutorFlow.Infrastructure.Audit;

/// <summary>Destino principal (SPEC 9.3): el <see cref="TextWriter"/> de stdout. Inyectable para las pruebas.</summary>
public sealed record AuditoriaStdout(TextWriter Escritor);

/// <summary>Destino secundario opcional (SPEC 9.3, <c>AUDIT_LOG_PATH</c>): agrega una linea JSON a un archivo.</summary>
public interface IAuditoriaArchivo
{
    Task AgregarLineaAsync(string linea, CancellationToken ct);
}

/// <summary>Archivo JSONL en <c>AUDIT_LOG_PATH</c>; crea el directorio si no existe.</summary>
public sealed class AuditoriaArchivo : IAuditoriaArchivo
{
    private readonly string _ruta;
    private readonly SemaphoreSlim _candado = new(1, 1);

    public AuditoriaArchivo(string ruta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        _ruta = ruta;
    }

    public async Task AgregarLineaAsync(string linea, CancellationToken ct)
    {
        await _candado.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(_ruta));
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.AppendAllTextAsync(_ruta, linea + "\n", ct).ConfigureAwait(false);
        }
        finally
        {
            _candado.Release();
        }
    }
}

/// <summary>
/// Auditoria (SPEC 9.3): cada evento es UNA linea JSON pura en stdout (no pasa por el formateador del logger, que la
/// escaparia dentro de "Message") y, si hay <c>AUDIT_LOG_PATH</c>, tambien en un archivo JSONL. Un fallo del archivo
/// no falla la peticion: se registra un error de operacion sin contenido del evento.
/// </summary>
public sealed class JsonLineAuditLog : IAuditLog
{
    public const string CategoriaLogger = "TutorFlow.Audit";
    private const int MaxPregunta = 1000;

    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly TextWriter _stdout;
    private readonly IAuditoriaArchivo? _archivo;
    private readonly ILogger _logger;
    private readonly object _lock = new();

    public JsonLineAuditLog(AuditoriaStdout stdout, IAuditoriaArchivo? archivo, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _stdout = stdout.Escritor;
        _archivo = archivo;
        _logger = loggerFactory.CreateLogger(CategoriaLogger);
    }

    public Task RegistrarPreguntaAsync(EventoPregunta evento, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evento);
        var pregunta = evento.Pregunta.Length > MaxPregunta ? evento.Pregunta[..MaxPregunta] : evento.Pregunta;
        return EscribirAsync(
            new
            {
                evento = "pregunta",
                ts = Formato(evento.Ts),
                requestId = evento.RequestId,
                usuario = evento.Usuario is null ? null : new { rol = evento.Usuario.Rol, id = evento.Usuario.Id },
                identidadValida = evento.IdentidadValida,
                pregunta,
                estudiantesConsultados = evento.EstudiantesConsultados,
                programasConsultados = evento.ProgramasConsultados,
                objetivosDenegados = evento.ObjetivosDenegados ?? [],
                herramientas = evento.Herramientas.Select(h => new { nombre = h.Nombre, objetivo = h.Objetivo, decision = h.Decision }),
                resultado = evento.Resultado,
                motivoDenegacion = evento.MotivoDenegacion,
                citas = evento.Citas,
                estrategia = evento.Estrategia,
                modelo = evento.Modelo,
                tokens = evento.Tokens,
                costoUsd = evento.CostoUsd,
                latenciaMs = evento.LatenciaMs,
            },
            ct);
    }

    public Task RegistrarRosterCargadoAsync(EventoRosterCargado evento, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return EscribirAsync(
            new
            {
                evento = "roster.cargado",
                ts = Formato(evento.Ts),
                estudiantes = evento.Estudiantes,
                programas = evento.Programas,
                resultado = evento.Resultado,
            },
            ct);
    }

    private static string Formato(DateTimeOffset ts) =>
        ts.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private async Task EscribirAsync(object evento, CancellationToken ct)
    {
        var linea = JsonSerializer.Serialize(evento, Opciones);
        lock (_lock)
        {
            _stdout.WriteLine(linea);
            _stdout.Flush();
        }

        if (_archivo is null)
        {
            return;
        }

        try
        {
            await _archivo.AgregarLineaAsync(linea, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // SPEC 9.3: error de operacion; sin el contenido del evento.
            _logger.LogError("No se pudo escribir el archivo de auditoria secundario ({TipoError}).", ex.GetType().Name);
        }
    }
}
