namespace TutorFlow.Application.Errores;

/// <summary>
/// La API simulada o el roster no estan disponibles (timeout, 5xx, red, reintentos agotados, roster ausente o vencido).
/// Fail-closed: quien la captura responde 503 (SPEC 3.4, 6.6).
/// </summary>
public sealed class DependenciaNoDisponibleException : Exception
{
    public DependenciaNoDisponibleException(string message)
        : base(message)
    {
    }

    public DependenciaNoDisponibleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
