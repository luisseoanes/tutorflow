using TutorFlow.Application.Errores;
using TutorFlow.Application.Ports;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Security;

/// <summary>
/// L5 (SPEC 6.2): antes de responder, verifica que la respuesta no contenga nombres ni ids de estudiantes fuera del
/// alcance del principal. Determinista; el LLM no interviene. Un hallazgo aborta toda la peticion (403-acceso, "salida").
/// </summary>
public static class GuardiaSalida
{
    public const string Motivo = "salida";

    public static ResultadoGuardia Evaluar(string respuesta, PrincipalAcademico principal, RosterSnapshot roster) =>
        GuardiaReferenciasExplicitas.Evaluar(respuesta, principal, roster, incluirCoordinadores: false, regla: Motivo);

    /// <exception cref="AccesoDenegadoException">La respuesta menciona estudiantes fuera del alcance.</exception>
    public static void Verificar(string respuesta, PrincipalAcademico principal, RosterSnapshot roster, int tokens = 0, decimal costoUsd = 0m)
    {
        if (Evaluar(respuesta, principal, roster).Marcada)
        {
            throw new AccesoDenegadoException(principal.Rol, Motivo, tokens, costoUsd);
        }
    }
}
