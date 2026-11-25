namespace TutorFlow.Domain.Academico;

/// <summary>Perdida por inasistencia (Reglamento 2025, Art. 11; SPEC 5.3). Aritmetica entera.</summary>
public static class Inasistencia
{
    /// <summary>
    /// true si inasistencias x 100 &gt; 15 x clases; false si no; null si no hay registro de inasistencias.
    /// Clases nulas o &lt;= 0, o inasistencias negativas = dato corrupto.
    /// </summary>
    public static bool? Pierde(int? clasesProgramadas, int? inasistencias)
    {
        if (clasesProgramadas is null || clasesProgramadas.Value <= 0)
        {
            throw new DatoAcademicoInvalidoException("clasesProgramadas debe ser un entero positivo.");
        }

        if (inasistencias is null)
        {
            return null;
        }

        if (inasistencias.Value < 0)
        {
            throw new DatoAcademicoInvalidoException("inasistencias no puede ser negativo.");
        }

        return (long)inasistencias.Value * 100 > 15L * clasesProgramadas.Value;
    }

    /// <summary>
    /// Porcentaje de inasistencia con 2 decimales (AwayFromZero); null si no hay registro de inasistencias.
    /// Misma validacion de dato corrupto que <see cref="Pierde"/>. Informativo: la decision de perdida usa <see cref="Pierde"/>.
    /// </summary>
    public static decimal? Porcentaje(int? clasesProgramadas, int? inasistencias)
    {
        if (Pierde(clasesProgramadas, inasistencias) is null)
        {
            return null;
        }

        return Math.Round(inasistencias!.Value * 100m / clasesProgramadas!.Value, 2, MidpointRounding.AwayFromZero);
    }
}
