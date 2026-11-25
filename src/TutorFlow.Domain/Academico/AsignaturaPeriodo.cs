namespace TutorFlow.Domain.Academico;

/// <summary>
/// Asignatura de un estudiante en el periodo. Valida los datos al construirse (dato corrupto -> excepcion tipada)
/// y expone las reglas de SPEC 5.1-5.6. Es pura: no hace E/S.
/// </summary>
public sealed class AsignaturaPeriodo
{
    public const decimal NotaAprobatoria = 3.0m;
    public const decimal NotaMinimaHabilitacion = 2.0m;

    public AsignaturaPeriodo(
        string codigo,
        int creditos,
        decimal? corte1,
        decimal? corte2,
        decimal? corte3,
        int? clasesProgramadas,
        int? inasistencias)
    {
        ArgumentException.ThrowIfNullOrEmpty(codigo);
        if (creditos <= 0)
        {
            throw new DatoAcademicoInvalidoException("creditos debe ser un entero positivo.");
        }

        Codigo = codigo;
        Creditos = creditos;
        Corte1 = corte1;
        Corte2 = corte2;
        Corte3 = corte3;
        ClasesProgramadas = clasesProgramadas;
        Inasistencias = inasistencias;

        // Valida cortes y asistencia en construccion (fail-closed ante datos corruptos).
        Definitiva = Cortes.Definitiva(corte1, corte2, corte3);
        PierdePorInasistencia = Inasistencia.Pierde(clasesProgramadas, inasistencias);
    }

    public string Codigo { get; }

    public int Creditos { get; }

    public decimal? Corte1 { get; }

    public decimal? Corte2 { get; }

    public decimal? Corte3 { get; }

    public int? ClasesProgramadas { get; }

    public int? Inasistencias { get; }

    /// <summary>Definitiva redondeada; null si algun corte esta pendiente.</summary>
    public decimal? Definitiva { get; }

    public decimal AporteAcumulado => Cortes.AporteAcumulado(Corte1, Corte2, Corte3);

    public int PorcentajeEvaluado => Cortes.PorcentajeEvaluado(Corte1, Corte2, Corte3);

    /// <summary>false si no hay registro de inasistencias (SPEC 5.3, D-06).</summary>
    public bool DatoAsistenciaDisponible => Inasistencias is not null;

    /// <summary>null = desconocido (sin registro).</summary>
    public bool? PierdePorInasistencia { get; }

    /// <summary>0.0 si pierde por inasistencia (sin importar los cortes); si no, la definitiva (null si en curso).</summary>
    public decimal? DefinitivaEfectiva => PierdePorInasistencia == true ? 0.0m : Definitiva;

    /// <summary>Perdida: definitiva efectiva &lt; 3.0 o perdida por inasistencia (Art. 5).</summary>
    public bool Perdida => PierdePorInasistencia == true || (DefinitivaEfectiva is { } d && d < NotaAprobatoria);

    /// <summary>Aprobada: definitiva efectiva &gt;= 3.0 (Art. 5).</summary>
    public bool Aprobada => DefinitivaEfectiva is { } d && d >= NotaAprobatoria;

    /// <summary>Habilitable = perdida y definitiva &gt;= 2.0 y no perdida por inasistencia (Art. 6-7).</summary>
    public bool Habilitable => Perdida
        && PierdePorInasistencia != true
        && Definitiva is { } d
        && d >= NotaMinimaHabilitacion;
}
