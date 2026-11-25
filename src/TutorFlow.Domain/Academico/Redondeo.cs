namespace TutorFlow.Domain.Academico;

/// <summary>Redondeo academico: siempre <see cref="MidpointRounding.AwayFromZero"/> sobre decimal (SPEC 5.1, 5.4).</summary>
public static class Redondeo
{
    /// <summary>Definitiva: 1 decimal.</summary>
    public static decimal Definitiva(decimal bruta) => Math.Round(bruta, 1, MidpointRounding.AwayFromZero);

    /// <summary>Promedio ponderado: 2 decimales.</summary>
    public static decimal Promedio(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    /// <summary>Aporte acumulado: 2 decimales.</summary>
    public static decimal Aporte(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
