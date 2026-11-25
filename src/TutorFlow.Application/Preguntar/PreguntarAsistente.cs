using TutorFlow.Application.Errores;
using TutorFlow.Application.Identity;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Security;
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
/// Caso de uso de SPEC 12.3. Implementado hasta L2: L1 (403-identidad / 503), roster (503) y L2a/L2b (403-acceso antes de cualquier LLM).
/// Los pasos siguientes (recuperacion, LLM + herramientas, validacion de citas, L5) se integran en T-05c..T-08.
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
    /// L2a + L2b: 403-acceso (motivo "referencia") antes de cualquier recuperacion o llamada al LLM; tokens 0 y ninguna
    /// consulta academica. Despues, PUNTO DE EXTENSION (T-05d..T-08): recuperacion, bucle LLM con IToolDispatcher,
    /// validacion de citas y L5. Mientras tanto responde 503 explicito; nunca un 200 inventado.
    /// </summary>
    private static Task<PreguntarResultado> ContinuarTrasRosterAsync(
        PreguntarComando comando,
        PrincipalAcademico principal,
        RosterSnapshot roster,
        CancellationToken ct)
    {
        DenegarSiMarcada(GuardiaReferenciasExplicitas.Evaluar(comando.Pregunta, principal, roster), principal);
        DenegarSiMarcada(GuardiaSolicitudesTerceros.Evaluar(comando.Pregunta, principal, roster), principal);

        _ = ct;
        throw new PendienteDeIntegracionException("El flujo posterior a L2 (recuperacion, LLM, herramientas, L5) aun no esta integrado.");
    }

    private static void DenegarSiMarcada(ResultadoGuardia resultado, PrincipalAcademico principal)
    {
        if (resultado.Marcada)
        {
            throw new AccesoDenegadoException(principal.Rol, "referencia");
        }
    }
}
