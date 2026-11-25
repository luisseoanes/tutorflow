namespace TutorFlow.Application.Errores;

/// <summary>
/// La identidad declarada no supera la validacion de consistencia de L1 (SPEC 3.3 regla 5). El motivo concreto es
/// solo para la auditoria: el cuerpo 403-identidad es identico para todos los motivos (SPEC 3.4).
/// </summary>
public sealed class IdentidadInvalidaException : Exception
{
    public IdentidadInvalidaException(string motivo)
        : base(motivo)
    {
        Motivo = motivo;
    }

    /// <summary>Motivo interno (nunca se devuelve al cliente).</summary>
    public string Motivo { get; }
}
