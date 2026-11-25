using TutorFlow.Application.Ports;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Llm;

/// <summary>
/// Resultado de la generacion fundamentada, ya validado (SPEC 7.4-7.5). Las citas solo son chunks recuperados en la
/// peticion y las citas deterministas de las herramientas; <see cref="Citas"/> vacia implica texto de abstencion.
/// </summary>
public sealed record RespuestaGenerada(
    string Respuesta,
    IReadOnlyList<CitaDocumental> Citas,
    bool UsoDatosEstudiante,
    int Tokens,
    decimal CostoUsd,
    IReadOnlyList<string> ObjetivosConsultados,
    bool Abstuvo);

/// <summary>
/// Servicio que Core invoca (T-08) despues de L1/L2 y de la recuperacion. Solo propone invocaciones: las ejecuta el
/// <see cref="IToolDispatcher"/>, cuyas excepciones (AccesoDenegadoException, etc.) se propagan sin capturar.
/// Puede lanzar <see cref="LlmNoDisponibleException"/> (Core: 503).
/// </summary>
public interface IGeneradorRespuesta
{
    Task<RespuestaGenerada> GenerarAsync(
        PrincipalAcademico principal,
        string pregunta,
        IReadOnlyList<Fragmento> fragmentos,
        IToolDispatcher herramientas,
        CancellationToken ct);
}
