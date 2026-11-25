namespace TutorFlow.Domain.Authorization;

/// <summary>Rol declarado y validado en L1 (SPEC 6.2).</summary>
public enum RolAcademico
{
    Estudiante,
    Coordinador,
}

/// <summary>
/// Principal inmutable construido por L1 (consistencia de identidad, no autenticacion; SPEC 6.5).
/// Su alcance es el id propio o los programas del coordinador. Nunca se deriva de la pregunta ni del LLM.
/// Refinado en T-02.
/// </summary>
public sealed record PrincipalAcademico
{
    public PrincipalAcademico(RolAcademico rol, string id, IEnumerable<string>? programasCoordinados = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Rol = rol;
        Id = id;
        ProgramasCoordinados = (programasCoordinados ?? Array.Empty<string>()).ToArray();
    }

    public RolAcademico Rol { get; }

    public string Id { get; }

    /// <summary>Programas coordinados (vacio para un estudiante).</summary>
    public IReadOnlyList<string> ProgramasCoordinados { get; }
}
