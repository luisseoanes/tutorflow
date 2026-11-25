namespace TutorFlow.Application.Errores;

/// <summary>
/// El flujo posterior al roster (L2, recuperacion, LLM, herramientas, L5) aun no esta integrado en esta fase.
/// Se responde 503 explicito: nunca una respuesta 200 inventada.
/// </summary>
public sealed class PendienteDeIntegracionException : Exception
{
    public PendienteDeIntegracionException(string message)
        : base(message)
    {
    }
}
