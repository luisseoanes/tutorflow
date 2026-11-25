using TutorFlow.Application.Errores;
using TutorFlow.Application.Ports;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Identity;

/// <summary>
/// L1 (SPEC 6.2): rol en lista blanca exacta, id con formato <c>^[A-Za-z0-9-]{1,32}$</c> (comparacion Ordinal, sin
/// normalizar) y existencia en la coleccion que corresponde al rol. Verifica consistencia; NO autentica (SPEC 6.5).
/// Un id con formato invalido nunca llega a la fuente de datos.
/// </summary>
public sealed class ValidadorIdentidad : IValidadorIdentidad
{
    public const string RolEstudiante = "estudiante";
    public const string RolCoordinador = "coordinador";
    public const int MaxLongitudId = 32;

    private readonly IAcademicDataSource _fuente;

    public ValidadorIdentidad(IAcademicDataSource fuente)
    {
        ArgumentNullException.ThrowIfNull(fuente);
        _fuente = fuente;
    }

    public async Task<PrincipalAcademico> ValidarAsync(string? rol, string? id, CancellationToken ct)
    {
        if (rol is null)
        {
            throw new IdentidadInvalidaException("rol ausente o de tipo invalido");
        }

        if (id is null)
        {
            throw new IdentidadInvalidaException("id ausente o de tipo invalido");
        }

        var esEstudiante = string.Equals(rol, RolEstudiante, StringComparison.Ordinal);
        var esCoordinador = string.Equals(rol, RolCoordinador, StringComparison.Ordinal);
        if (!esEstudiante && !esCoordinador)
        {
            throw new IdentidadInvalidaException("rol fuera de la lista blanca");
        }

        if (!FormatoIdValido(id))
        {
            throw new IdentidadInvalidaException("formato de id invalido");
        }

        if (esEstudiante)
        {
            var estudiante = await _fuente.ObtenerEstudianteAsync(id, ct);
            if (estudiante is null || !string.Equals(estudiante.Id, id, StringComparison.Ordinal))
            {
                throw new IdentidadInvalidaException("id inexistente o que no corresponde al rol");
            }

            return new PrincipalAcademico(RolAcademico.Estudiante, id);
        }

        var coordinador = await _fuente.ObtenerCoordinadorAsync(id, ct);
        if (coordinador is null || !string.Equals(coordinador.Id, id, StringComparison.Ordinal))
        {
            throw new IdentidadInvalidaException("id inexistente o que no corresponde al rol");
        }

        return new PrincipalAcademico(RolAcademico.Coordinador, id, coordinador.Programas);
    }

    /// <summary>Equivale a <c>^[A-Za-z0-9-]{1,32}$</c> sobre ASCII, sin la tolerancia de <c>$</c> a un salto de linea final.</summary>
    public static bool FormatoIdValido(string id)
    {
        if (id.Length is 0 or > MaxLongitudId)
        {
            return false;
        }

        foreach (var c in id)
        {
            var ok = c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }
}
