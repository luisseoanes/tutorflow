using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TutorFlow.Application.Ports;

namespace TutorFlow.Infrastructure.Audit;

/// <summary>
/// Auditoria minima (SPEC 9.3, destino principal): cada evento es una linea JSON en el logger <c>TutorFlow.Audit</c>
/// (stdout). La emision de <c>pregunta</c> por peticion, el destino de archivo y la minimizacion completa son de T-05e.
/// </summary>
public sealed class JsonLineAuditLog : IAuditLog
{
    public const string CategoriaLogger = "TutorFlow.Audit";
    private const int MaxPregunta = 1000;

    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly ILogger _logger;

    public JsonLineAuditLog(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger(CategoriaLogger);
    }

    public Task RegistrarPreguntaAsync(EventoPregunta evento, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evento);
        var pregunta = evento.Pregunta.Length > MaxPregunta ? evento.Pregunta[..MaxPregunta] : evento.Pregunta;
        Escribir(new
        {
            evento = "pregunta",
            ts = evento.Ts,
            requestId = evento.RequestId,
            usuario = evento.Usuario,
            identidadValida = evento.IdentidadValida,
            pregunta,
            estudiantesConsultados = evento.EstudiantesConsultados,
            programasConsultados = evento.ProgramasConsultados,
            herramientas = evento.Herramientas,
            resultado = evento.Resultado,
            motivoDenegacion = evento.MotivoDenegacion,
            citas = evento.Citas,
            estrategia = evento.Estrategia,
            modelo = evento.Modelo,
            tokens = evento.Tokens,
            costoUsd = evento.CostoUsd,
            latenciaMs = evento.LatenciaMs,
        });
        return Task.CompletedTask;
    }

    public Task RegistrarRosterCargadoAsync(EventoRosterCargado evento, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evento);
        Escribir(new
        {
            evento = "roster.cargado",
            ts = evento.Ts,
            estudiantes = evento.Estudiantes,
            programas = evento.Programas,
            resultado = evento.Resultado,
        });
        return Task.CompletedTask;
    }

    private void Escribir(object evento)
    {
        _logger.LogInformation("{Evento}", JsonSerializer.Serialize(evento, Opciones));
    }
}
