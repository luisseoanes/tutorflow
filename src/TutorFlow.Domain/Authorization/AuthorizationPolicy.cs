namespace TutorFlow.Domain.Authorization;

/// <summary>
/// Implementacion pura y determinista de la matriz de SPEC 6.1. Sin E/S, sin estado, sin LLM.
/// Fail-closed: id o programa nulo/vacio/desconocido, tipo de recurso o rol no reconocido -> denegar.
/// Los datos de coordinadores no se exponen a nadie.
/// </summary>
public sealed class AuthorizationPolicy : IAuthorizationPolicy
{
    public DecisionAutorizacion Authorize(PrincipalAcademico principal, RecursoAcademico recurso)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(recurso);

        return recurso.Tipo switch
        {
            TipoRecurso.Reglamento => DecisionAutorizacion.Permitir(),
            TipoRecurso.DatosEstudiante => AutorizarDatosEstudiante(principal, recurso),
            TipoRecurso.ProgramaEstudiantes => AutorizarPrograma(principal, recurso),
            TipoRecurso.DatosCoordinador => DecisionAutorizacion.Denegar("datos_coordinador"),
            _ => DecisionAutorizacion.Denegar("recurso_desconocido"),
        };
    }

    private static DecisionAutorizacion AutorizarDatosEstudiante(PrincipalAcademico principal, RecursoAcademico recurso)
    {
        switch (principal.Rol)
        {
            case RolAcademico.Estudiante:
                return !string.IsNullOrEmpty(recurso.Id) && string.Equals(recurso.Id, principal.Id, StringComparison.Ordinal)
                    ? DecisionAutorizacion.Permitir()
                    : DecisionAutorizacion.Denegar("estudiante_ajeno");
            case RolAcademico.Coordinador:
                return ProgramaCoordinado(principal, recurso.ProgramaId)
                    ? DecisionAutorizacion.Permitir()
                    : DecisionAutorizacion.Denegar("fuera_de_programa");
            default:
                return DecisionAutorizacion.Denegar("rol_desconocido");
        }
    }

    private static DecisionAutorizacion AutorizarPrograma(PrincipalAcademico principal, RecursoAcademico recurso)
    {
        if (principal.Rol != RolAcademico.Coordinador)
        {
            return DecisionAutorizacion.Denegar("listado_solo_coordinador");
        }

        return ProgramaCoordinado(principal, recurso.ProgramaId)
            ? DecisionAutorizacion.Permitir()
            : DecisionAutorizacion.Denegar("fuera_de_programa");
    }

    private static bool ProgramaCoordinado(PrincipalAcademico principal, string? programaId)
    {
        if (string.IsNullOrEmpty(programaId))
        {
            return false;
        }

        foreach (var p in principal.ProgramasCoordinados)
        {
            if (string.Equals(p, programaId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
