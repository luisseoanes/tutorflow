namespace TutorFlow.Application.Security;

/// <summary>
/// Resultado de una guardia determinista (L2a, L2b o L5). <c>Regla</c> identifica la regla que marco ("R1".."R6",
/// "referencia" en L2a, "salida" en L5); <c>Objetivos</c> son los ids fuera del alcance (para auditoria), vacio en L2b.
/// </summary>
public sealed record ResultadoGuardia(bool Marcada, string? Regla, IReadOnlyList<string> Objetivos)
{
    public static ResultadoGuardia Ninguna { get; } = new(false, null, []);

    public static ResultadoGuardia Marca(string regla, IReadOnlyList<string>? objetivos = null) =>
        new(true, regla, objetivos ?? []);
}
