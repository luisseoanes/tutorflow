namespace TutorFlow.Domain.Academico;

/// <summary>Calculo de la definitiva a partir de los tres cortes (SPEC 5.1, 5.2). Pesos 30/30/40.</summary>
public static class Cortes
{
    public const decimal Peso1 = 0.3m;
    public const decimal Peso2 = 0.3m;
    public const decimal Peso3 = 0.4m;

    /// <summary>Definitiva con los tres cortes calificados.</summary>
    public static decimal Definitiva(decimal corte1, decimal corte2, decimal corte3)
    {
        Nota.Validar(corte1, nameof(corte1));
        Nota.Validar(corte2, nameof(corte2));
        Nota.Validar(corte3, nameof(corte3));
        return Redondeo.Definitiva((Peso1 * corte1) + (Peso2 * corte2) + (Peso3 * corte3));
    }

    /// <summary>Definitiva, o null si algun corte es null (asignatura en curso).</summary>
    public static decimal? Definitiva(decimal? corte1, decimal? corte2, decimal? corte3)
    {
        Validar(corte1, nameof(corte1));
        Validar(corte2, nameof(corte2));
        Validar(corte3, nameof(corte3));
        if (corte1 is null || corte2 is null || corte3 is null)
        {
            return null;
        }

        return Definitiva(corte1.Value, corte2.Value, corte3.Value);
    }

    /// <summary>Aporte acumulado = suma(peso x corte calificado), 2 decimales.</summary>
    public static decimal AporteAcumulado(decimal? corte1, decimal? corte2, decimal? corte3)
    {
        Validar(corte1, nameof(corte1));
        Validar(corte2, nameof(corte2));
        Validar(corte3, nameof(corte3));
        var suma = (corte1 is null ? 0m : Peso1 * corte1.Value)
            + (corte2 is null ? 0m : Peso2 * corte2.Value)
            + (corte3 is null ? 0m : Peso3 * corte3.Value);
        return Redondeo.Aporte(suma);
    }

    /// <summary>Porcentaje evaluado (0..100): suma de los pesos de los cortes calificados.</summary>
    public static int PorcentajeEvaluado(decimal? corte1, decimal? corte2, decimal? corte3)
    {
        Validar(corte1, nameof(corte1));
        Validar(corte2, nameof(corte2));
        Validar(corte3, nameof(corte3));
        return (corte1 is null ? 0 : 30) + (corte2 is null ? 0 : 30) + (corte3 is null ? 0 : 40);
    }

    private static void Validar(decimal? corte, string campo)
    {
        if (corte is not null)
        {
            Nota.Validar(corte.Value, campo);
        }
    }
}
