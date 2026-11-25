namespace TutorFlow.Domain.Academico;

public enum ResultadoBeca
{
    Cumple,
    NoCumple,
    NoDeterminable,
}

public enum RequisitoBeca
{
    Promedio,
    SinAsignaturasPerdidas,
    CreditosMatriculados,
}

public enum EstadoRequisito
{
    Cumple,
    NoCumple,
    NoDeterminable,
}

/// <summary>Detalle de un requisito: <see cref="Observado"/> es el valor medido (null si no disponible); <see cref="Exigido"/> el umbral.</summary>
public sealed record RequisitoBecaEvaluado(RequisitoBeca Requisito, EstadoRequisito Estado, decimal? Observado, decimal Exigido);

public sealed record EvaluacionBeca(ResultadoBeca Resultado, IReadOnlyList<RequisitoBecaEvaluado> Requisitos);

/// <summary>Beca por rendimiento academico (bienestar_y_becas.md 3.1; SPEC 5.8). TutorFlow informa, no otorga.</summary>
public static class Beca
{
    public const decimal PromedioMinimo = 4.30m;
    public const int CreditosMinimos = 12;

    public static EvaluacionBeca Evaluar(IEnumerable<AsignaturaPeriodo> asignaturas)
    {
        ArgumentNullException.ThrowIfNull(asignaturas);
        var lista = asignaturas.ToList();

        var promedio = ResumenAcademico.Promedio(lista);
        var creditos = ResumenAcademico.CreditosMatriculados(lista);
        var hayCortesPendientes = lista.Any(a => a.Definitiva is null);
        var hayAsistenciaDesconocida = lista.Any(a => a.PierdePorInasistencia is null);

        // Promedio >= 4.30 con cortes pendientes o asistencia desconocida aun puede cambiar: no determinable.
        EstadoRequisito estadoPromedio;
        if (promedio is null)
        {
            estadoPromedio = EstadoRequisito.NoDeterminable;
        }
        else if (promedio.Value >= PromedioMinimo)
        {
            estadoPromedio = hayCortesPendientes || hayAsistenciaDesconocida
                ? EstadoRequisito.NoDeterminable
                : EstadoRequisito.Cumple;
        }
        else
        {
            // Un promedio < 4.30 ya incumple, aunque haya una asignatura en curso (tabla golden: "No (promedio)").
            estadoPromedio = EstadoRequisito.NoCumple;
        }

        // Perdidas: una perdida conocida ya falla; si no, lo pendiente o desconocido deja indeterminado.
        var perdidas = lista.Count(a => a.Perdida);
        EstadoRequisito estadoPerdidas;
        if (perdidas > 0)
        {
            estadoPerdidas = EstadoRequisito.NoCumple;
        }
        else if (hayCortesPendientes || hayAsistenciaDesconocida)
        {
            estadoPerdidas = EstadoRequisito.NoDeterminable;
        }
        else
        {
            estadoPerdidas = EstadoRequisito.Cumple;
        }

        var estadoCreditos = creditos >= CreditosMinimos ? EstadoRequisito.Cumple : EstadoRequisito.NoCumple;

        var requisitos = new List<RequisitoBecaEvaluado>
        {
            new(RequisitoBeca.Promedio, estadoPromedio, promedio, PromedioMinimo),
            new(RequisitoBeca.SinAsignaturasPerdidas, estadoPerdidas, perdidas, 0),
            new(RequisitoBeca.CreditosMatriculados, estadoCreditos, creditos, CreditosMinimos),
        };

        ResultadoBeca resultado;
        if (requisitos.Any(r => r.Estado == EstadoRequisito.NoCumple))
        {
            resultado = ResultadoBeca.NoCumple;
        }
        else if (requisitos.Any(r => r.Estado == EstadoRequisito.NoDeterminable))
        {
            resultado = ResultadoBeca.NoDeterminable;
        }
        else
        {
            resultado = ResultadoBeca.Cumple;
        }

        return new EvaluacionBeca(resultado, requisitos);
    }
}
