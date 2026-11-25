namespace TutorFlow.Application.Ports;

public sealed record EstudianteFuente(string Id, string Nombre, string ProgramaId, int? Semestre);

public sealed record CoordinadorFuente(string Id, string Nombre, IReadOnlyList<string> Programas);

public sealed record ProgramaFuente(string Id, string Nombre, string? CoordinadorId);

/// <summary>Nota cruda tal como la entrega la fuente: los cortes pueden ser nulos (aun no registrados).</summary>
public sealed record NotaFuente(
    string EstudianteId,
    string Codigo,
    string Asignatura,
    int Creditos,
    decimal? Corte1,
    decimal? Corte2,
    decimal? Corte3);

public sealed record AsistenciaFuente(
    string EstudianteId,
    string Codigo,
    string Asignatura,
    int? ClasesProgramadas,
    int? Inasistencias);

/// <summary>
/// Puerto de la API simulada. Solo se llama con ids validados (URL-encoding en el adaptador).
/// Devuelve <c>null</c> cuando el recurso no existe; falla con DependenciaNoDisponibleException si la fuente no responde.
/// </summary>
public interface IAcademicDataSource
{
    Task<EstudianteFuente?> ObtenerEstudianteAsync(string estudianteId, CancellationToken ct);

    Task<IReadOnlyList<NotaFuente>> ObtenerNotasAsync(string estudianteId, CancellationToken ct);

    Task<IReadOnlyList<AsistenciaFuente>> ObtenerAsistenciaAsync(string estudianteId, CancellationToken ct);

    Task<CoordinadorFuente?> ObtenerCoordinadorAsync(string coordinadorId, CancellationToken ct);

    Task<IReadOnlyList<ProgramaFuente>> ListarProgramasAsync(CancellationToken ct);

    Task<IReadOnlyList<EstudianteFuente>> ListarEstudiantesProgramaAsync(string programaId, CancellationToken ct);
}
