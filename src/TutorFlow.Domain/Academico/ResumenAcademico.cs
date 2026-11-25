namespace TutorFlow.Domain.Academico;

public enum SituacionAcademica
{
    Normal,
    PruebaAcademica,
    NoDisponible,
}

/// <summary>Reglas agregadas del periodo (SPEC 5.4, 5.7).</summary>
public static class ResumenAcademico
{
    public const decimal UmbralPruebaAcademica = 3.0m;

    /// <summary>
    /// Promedio ponderado por creditos de las definitivas efectivas (ya redondeadas), 2 decimales AwayFromZero.
    /// Solo entran las asignaturas con definitiva efectiva (las perdidas por inasistencia entran con 0.0).
    /// null = "no disponible" si ninguna entra.
    /// </summary>
    public static decimal? Promedio(IEnumerable<AsignaturaPeriodo> asignaturas)
    {
        ArgumentNullException.ThrowIfNull(asignaturas);
        decimal suma = 0m;
        decimal creditos = 0m;
        foreach (var a in asignaturas)
        {
            if (a.DefinitivaEfectiva is { } d)
            {
                suma += d * a.Creditos;
                creditos += a.Creditos;
            }
        }

        return creditos == 0m ? null : Redondeo.Promedio(suma / creditos);
    }

    /// <summary>Creditos matriculados: suma de todas las asignaturas, tengan o no definitiva.</summary>
    public static int CreditosMatriculados(IEnumerable<AsignaturaPeriodo> asignaturas)
    {
        ArgumentNullException.ThrowIfNull(asignaturas);
        return asignaturas.Sum(a => a.Creditos);
    }

    /// <summary>Prueba academica si promedio &lt; 3.0 (Art. 20).</summary>
    public static SituacionAcademica Situacion(decimal? promedio) => promedio switch
    {
        null => SituacionAcademica.NoDisponible,
        < UmbralPruebaAcademica => SituacionAcademica.PruebaAcademica,
        _ => SituacionAcademica.Normal,
    };
}
