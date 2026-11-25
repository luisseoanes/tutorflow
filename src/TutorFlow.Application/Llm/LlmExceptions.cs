namespace TutorFlow.Application.Llm;

/// <summary>
/// El proveedor LLM no esta disponible o rechazo la credencial (401/403, reintentos agotados, salida ilegible).
/// Core la mapea a 503 (SPEC 10.2). El mensaje nunca contiene la llave ni el cuerpo del proveedor.
/// </summary>
public sealed class LlmNoDisponibleException : Exception
{
    public LlmNoDisponibleException(string message, int? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }

    /// <summary>Ultimo codigo HTTP del proveedor, si lo hubo.</summary>
    public int? StatusCode { get; }
}

/// <summary>Configuracion LLM invalida (p. ej. LLM_PROVIDER desconocido). Debe fallar al arrancar.</summary>
public sealed class LlmConfiguracionException(string message) : InvalidOperationException(message);
