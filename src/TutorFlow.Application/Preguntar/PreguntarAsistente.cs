using TutorFlow.Application.Errores;
using TutorFlow.Application.Identity;
using TutorFlow.Application.Ports;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Preguntar;

/// <summary>Entrada ya validada en forma (SPEC 3.3 reglas 1-4). <c>Rol</c> e <c>Id</c> son null si faltan o no son string.</summary>
public sealed record PreguntarComando(string Pregunta, string? Rol, string? Id, string RequestId);

public sealed record PreguntarResultado(
    string Respuesta,
    IReadOnlyList<CitaDocumental> Citas,
    bool UsoDatosEstudiante,
    int Tokens,
    decimal CostoUsd);

public interface IPreguntarAsistente
{
    Task<PreguntarResultado> EjecutarAsync(PreguntarComando comando, CancellationToken ct);
}

/// <summary>
/// Caso de uso de SPEC 12.3. Implementado hasta "roster vigente": L1 (403-identidad / 503) y roster (503).
/// Los pasos siguientes (L2a/L2b, recuperacion, LLM + herramientas, validacion de citas, L5) se integran en T-05c..T-08.
/// </summary>
public sealed class PreguntarAsistente : IPreguntarAsistente
{
    private readonly IValidadorIdentidad _identidad;
    private readonly IRosterProvider _roster;

    public PreguntarAsistente(IValidadorIdentidad identidad, IRosterProvider roster)
    {
        ArgumentNullException.ThrowIfNull(identidad);
        ArgumentNullException.ThrowIfNull(roster);
        _identidad = identidad;
        _roster = roster;
    }

    public async Task<PreguntarResultado> EjecutarAsync(PreguntarComando comando, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var principal = await _identidad.ValidarAsync(comando.Rol, comando.Id, ct);
        var roster = await _roster.ObtenerAsync(ct);
        return await ContinuarTrasRosterAsync(comando, principal, roster, ct);
    }

    /// <summary>
    /// PUNTO DE EXTENSION (T-05c): aqui se inserta L2a + L2b (403-acceso antes de cualquier llamada al LLM) y, despues,
    /// recuperacion, bucle LLM con IToolDispatcher, validacion de citas y L5 (T-05d..T-08).
    /// Mientras tanto responde 503 explicito; nunca un 200 inventado.
    /// </summary>
    private static Task<PreguntarResultado> ContinuarTrasRosterAsync(
        PreguntarComando comando,
        PrincipalAcademico principal,
        RosterSnapshot roster,
        CancellationToken ct)
    {
        _ = (comando, principal, roster, ct);
        throw new PendienteDeIntegracionException("El flujo posterior al roster (L2, recuperacion, LLM, L5) aun no esta integrado.");
    }
}
