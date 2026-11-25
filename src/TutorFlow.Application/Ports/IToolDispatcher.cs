using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Ports;

public sealed record CitaDocumental(string Documento, string Seccion);

/// <summary>Resultado ya calculado por el dominio (JSON con formato invariante) y sus citas deterministas (SPEC 7.5).</summary>
public sealed record ResultadoHerramienta(
    string Contenido,
    IReadOnlyList<CitaDocumental> Citas,
    bool UsoDatosEstudiante,
    string? Objetivo);

/// <summary>
/// Unica via de ejecucion de herramientas para el bucle del LLM: L3 (herramienta expuesta) -> esquema -> L4
/// (IAuthorizationPolicy antes de leer) -> ejecucion. El principal lo aporta el servidor, jamas el LLM; una
/// denegacion lanza AccesoDenegadoException y aborta la peticion.
/// </summary>
public interface IToolDispatcher
{
    IReadOnlyList<ToolDefinition> HerramientasExpuestas(PrincipalAcademico principal);

    Task<ResultadoHerramienta> DespacharAsync(
        PrincipalAcademico principal,
        InvocacionPropuesta invocacion,
        CancellationToken ct);
}
