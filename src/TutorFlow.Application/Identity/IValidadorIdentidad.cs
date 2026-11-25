using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Identity;

/// <summary>
/// L1: valida la CONSISTENCIA de la identidad declarada (SPEC 6.2, 6.5). No es autenticacion: no prueba que quien
/// declara la identidad sea esa persona.
/// </summary>
public interface IValidadorIdentidad
{
    /// <param name="rol">Valor de <c>usuario.rol</c>, o null si falta, es null o no es un string.</param>
    /// <param name="id">Valor de <c>usuario.id</c>, o null si falta, es null o no es un string.</param>
    /// <param name="ct">Token de cancelacion.</param>
    /// <exception cref="Errores.IdentidadInvalidaException">La identidad declarada es inconsistente.</exception>
    /// <exception cref="Errores.DependenciaNoDisponibleException">La fuente de datos no responde.</exception>
    Task<PrincipalAcademico> ValidarAsync(string? rol, string? id, CancellationToken ct);
}
