namespace TutorFlow.Application.Ports;

public sealed record UsuarioAuditado(string Rol, string Id);

public sealed record HerramientaAuditada(string Nombre, string? Objetivo, string Decision);

/// <summary>
/// Evento <c>pregunta</c> (SPEC 9.2). No contiene notas, respuesta ni datos de la API. <c>ObjetivosDenegados</c> (campo
/// aditivo) lleva el objetivo intentado y rechazado por una guardia (L2), sin datos.
/// </summary>
public sealed record EventoPregunta(
    DateTimeOffset Ts,
    string RequestId,
    UsuarioAuditado? Usuario,
    bool IdentidadValida,
    string Pregunta,
    IReadOnlyList<string> EstudiantesConsultados,
    IReadOnlyList<string> ProgramasConsultados,
    IReadOnlyList<HerramientaAuditada> Herramientas,
    int Resultado,
    string? MotivoDenegacion,
    IReadOnlyList<string> Citas,
    string? Estrategia,
    string? Modelo,
    int Tokens,
    decimal CostoUsd,
    long LatenciaMs,
    IReadOnlyList<string>? ObjetivosDenegados = null);

/// <summary>Evento de sistema <c>roster.cargado</c> (SPEC 9.1): sin ids ni nombres.</summary>
public sealed record EventoRosterCargado(DateTimeOffset Ts, int Estudiantes, int Programas, string Resultado);

public interface IAuditLog
{
    Task RegistrarPreguntaAsync(EventoPregunta evento, CancellationToken ct);

    Task RegistrarRosterCargadoAsync(EventoRosterCargado evento, CancellationToken ct);
}
