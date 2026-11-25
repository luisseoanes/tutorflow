using TutorFlow.Application.Ports;

namespace TutorFlow.Application.Audit;

/// <summary>
/// Contexto de auditoria de UNA peticion (scoped, SPEC 9.2). L1, L2, el despachador y el middleware de errores
/// acumulan aqui; el middleware de auditoria emite el evento una sola vez al terminar. Solo guarda ids, nombres de
/// herramienta y decisiones: nunca notas, respuestas ni datos de la API.
/// </summary>
public interface IContextoAuditoria
{
    /// <summary>Pregunta literal y usuario declarado (si es legible). La identidad aun no esta validada.</summary>
    void Iniciar(string pregunta, string? rol, string? id);

    /// <summary>L1 supero la validacion de consistencia.</summary>
    void IdentidadValidada();

    /// <summary>Objetivos intentados y denegados por una guardia (L2): no son consultas ejecutadas.</summary>
    void ObjetivosDenegados(IEnumerable<string> objetivos);

    /// <summary>Motivo de denegacion: identidad, referencia, herramienta, politica o salida (el primero gana).</summary>
    void Denegar(string motivo);

    /// <summary>Invocacion de herramienta denegada (objetivo intentado, sin datos).</summary>
    void HerramientaDenegada(string nombre, string? objetivo);

    /// <summary>Invocacion con argumentos de forma invalida devuelta al LLM como error de herramienta (D-15): sin objetivo ni datos.</summary>
    void HerramientaRechazadaForma(string nombre);

    /// <summary>
    /// Consulta academica ejecutada con exito: el objetivo entra a estudiantesConsultados o, si es de programa
    /// (ids separados por coma), a programasConsultados.
    /// </summary>
    void ConsultaExitosa(string nombre, string objetivo, bool esPrograma);

    /// <summary>Datos del flujo LLM (se completan en T-08).</summary>
    void FlujoLlm(IEnumerable<string>? citas = null, string? estrategia = null, string? modelo = null, int? tokens = null, decimal? costoUsd = null);

    /// <summary>Construye el evento y marca la emision: devuelve null si ya se emitio.</summary>
    EventoPregunta? ConstruirUnaVez(string requestId, DateTimeOffset ts, int resultado, long latenciaMs);
}
