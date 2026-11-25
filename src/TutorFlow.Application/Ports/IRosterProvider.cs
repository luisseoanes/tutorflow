namespace TutorFlow.Application.Ports;

/// <summary>Estudiante del roster: SOLO los datos minimos de SPEC 6.6 (sin notas, asistencia ni semestre).</summary>
public sealed record RosterEstudiante(string Id, string NombreNormalizado, string ProgramaId);

public sealed record RosterCoordinador(string Id);

public sealed record RosterPrograma(string Id, string NombreNormalizado);

public sealed record RosterSnapshot(
    IReadOnlyList<RosterEstudiante> Estudiantes,
    IReadOnlyList<RosterCoordinador> Coordinadores,
    IReadOnlyList<RosterPrograma> Programas,
    DateTimeOffset CargadoEn);

/// <summary>
/// Roster de referencias para L2a/L2b y para resolver programa(X) en L4. Nunca se envia al LLM ni se atribuye a una
/// peticion. Fail-closed: lanza DependenciaNoDisponibleException si nunca cargo o supera 2 x TTL.
/// </summary>
public interface IRosterProvider
{
    Task<RosterSnapshot> ObtenerAsync(CancellationToken ct);
}
