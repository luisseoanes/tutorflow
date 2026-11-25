namespace TutorFlow.Domain.Authorization;

public enum TipoRecurso
{
    Reglamento,
    DatosEstudiante,
    ProgramaEstudiantes,
    DatosCoordinador,
}

/// <summary>
/// Recurso a autorizar. <see cref="ProgramaId"/> es el programa del estudiante objetivo (resuelto desde el roster)
/// o el programa listado; nunca se toma de la pregunta ni del LLM. Un objetivo ausente del roster se deniega.
/// </summary>
public sealed record RecursoAcademico(TipoRecurso Tipo, string? Id = null, string? ProgramaId = null);

public sealed record DecisionAutorizacion(bool Permitido, string Motivo)
{
    public static DecisionAutorizacion Permitir() => new(true, "permitido");

    public static DecisionAutorizacion Denegar(string motivo) => new(false, motivo);
}

/// <summary>
/// Politica pura y determinista (SPEC 6.1). Solo depende del principal y del recurso; el LLM ni concede ni deniega.
/// </summary>
public interface IAuthorizationPolicy
{
    DecisionAutorizacion Authorize(PrincipalAcademico principal, RecursoAcademico recurso);
}
