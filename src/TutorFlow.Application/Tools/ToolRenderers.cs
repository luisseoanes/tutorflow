using System.Globalization;
using System.Text;
using TutorFlow.Domain.Academico;

namespace TutorFlow.Application.Tools;

/// <summary>Resultados JSON de las herramientas individuales (SPEC 8): datos ya calculados por el dominio.</summary>
internal static class ToolRenderers
{
    public static string Notas(string estudianteId, List<AsignaturaConNombre> periodo, CitasHerramienta citas)
    {
        citas.Definitiva().DatosEstudiante("Notas");
        PeriodoAcademico.CitarAsistencia(citas, periodo);
        if (periodo.Any(p => p.Dominio.Aprobada || p.Dominio.Perdida))
        {
            citas.AprobadaOPerdida();
        }

        if (periodo.Any(p => p.Dominio.Perdida))
        {
            citas.Habilitacion();
        }

        return PeriodoAcademico.Json(w =>
        {
            w.WriteStartObject();
            w.WriteString("estudianteId", estudianteId);
            w.WriteStartArray("asignaturas");
            foreach (var (a, nombre) in periodo)
            {
                w.WriteStartObject();
                w.WriteString("codigo", a.Codigo);
                w.WriteString("asignatura", nombre);
                w.WriteNumber("creditos", a.Creditos);
                PeriodoAcademico.Num(w, "corte1", a.Corte1);
                PeriodoAcademico.Num(w, "corte2", a.Corte2);
                PeriodoAcademico.Num(w, "corte3", a.Corte3);
                PeriodoAcademico.Num(w, "definitiva", a.Definitiva);
                if (a.Definitiva is null)
                {
                    PeriodoAcademico.Num(w, "aporteAcumulado", a.AporteAcumulado);
                    w.WriteNumber("porcentajeEvaluado", a.PorcentajeEvaluado);
                }

                PeriodoAcademico.Num(w, "definitivaEfectiva", a.DefinitivaEfectiva);
                w.WriteString("estado", PeriodoAcademico.Estado(a));
                w.WriteBoolean("habilitable", a.Habilitable);
                PeriodoAcademico.Bool(w, "pierdePorInasistencia", a.PierdePorInasistencia);
                w.WriteBoolean("datoAsistenciaDisponible", a.DatoAsistenciaDisponible);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        });
    }

    public static string Asistencia(string estudianteId, List<AsignaturaConNombre> periodo, string? filtro, CitasHerramienta citas)
    {
        var seleccion = Filtrar(periodo, filtro);
        citas.DatosEstudiante("Asistencia");
        if (seleccion.Any(p => p.Dominio.DatoAsistenciaDisponible))
        {
            citas.PerdidaPorInasistencia();
        }

        if (seleccion.Any(p => !p.Dominio.DatoAsistenciaDisponible))
        {
            citas.AsistenciaNoRegistrada();
        }

        return PeriodoAcademico.Json(w =>
        {
            w.WriteStartObject();
            w.WriteString("estudianteId", estudianteId);
            w.WriteStartArray("asignaturas");
            foreach (var (a, nombre) in seleccion)
            {
                w.WriteStartObject();
                w.WriteString("codigo", a.Codigo);
                w.WriteString("asignatura", nombre);
                w.WriteNumber("clasesProgramadas", a.ClasesProgramadas!.Value);
                if (a.Inasistencias is { } faltas)
                {
                    w.WriteNumber("inasistencias", faltas);
                    PeriodoAcademico.Num(w, "porcentajeInasistencia", Inasistencia.Porcentaje(a.ClasesProgramadas, a.Inasistencias));
                }
                else
                {
                    w.WriteNull("inasistencias");
                    w.WriteNull("porcentajeInasistencia");
                }

                PeriodoAcademico.Bool(w, "pierdePorInasistencia", a.PierdePorInasistencia);
                w.WriteBoolean("datoDisponible", a.DatoAsistenciaDisponible);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        });
    }

    public static string Resumen(string estudianteId, List<AsignaturaConNombre> periodo, CitasHerramienta citas)
    {
        var dominios = periodo.Select(p => p.Dominio).ToList();
        var promedio = ResumenAcademico.Promedio(dominios);
        var creditos = ResumenAcademico.CreditosMatriculados(dominios);
        var situacion = ResumenAcademico.Situacion(promedio);

        citas.Definitiva().Promedio().AprobadaOPerdida().SituacionAcademica().DatosEstudiante("Resumen académico");
        PeriodoAcademico.CitarAsistencia(citas, periodo);
        if (periodo.Any(p => p.Dominio.Perdida))
        {
            citas.Habilitacion();
        }

        return PeriodoAcademico.Json(w =>
        {
            w.WriteStartObject();
            w.WriteString("estudianteId", estudianteId);
            PeriodoAcademico.Num(w, "promedio", promedio);
            w.WriteBoolean("promedioDisponible", promedio is not null);
            w.WriteNumber("creditosMatriculados", creditos);
            w.WriteStartArray("asignaturasPerdidas");
            foreach (var (a, nombre) in periodo.Where(p => p.Dominio.Perdida))
            {
                EscribirPerdida(w, a, nombre);
            }

            w.WriteEndArray();
            w.WriteStartArray("asignaturasSinDefinitiva");
            foreach (var (a, nombre) in periodo.Where(p => p.Dominio.DefinitivaEfectiva is null))
            {
                w.WriteStartObject();
                w.WriteString("codigo", a.Codigo);
                w.WriteString("asignatura", nombre);
                PeriodoAcademico.Num(w, "aporteAcumulado", a.AporteAcumulado);
                w.WriteNumber("porcentajeEvaluado", a.PorcentajeEvaluado);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteString("situacionAcademica", situacion switch
            {
                SituacionAcademica.Normal => "normal",
                SituacionAcademica.PruebaAcademica => "prueba-academica",
                _ => "no-disponible",
            });
            w.WriteEndObject();
        });
    }

    public static string Beca(string estudianteId, List<AsignaturaConNombre> periodo, CitasHerramienta citas)
    {
        var evaluacion = TutorFlow.Domain.Academico.Beca.Evaluar(periodo.Select(p => p.Dominio));
        citas.Beca().Promedio().AprobadaOPerdida().DatosEstudiante("Resumen académico");
        PeriodoAcademico.CitarAsistencia(citas, periodo);

        return PeriodoAcademico.Json(w =>
        {
            w.WriteStartObject();
            w.WriteString("estudianteId", estudianteId);
            w.WriteString("resultado", evaluacion.Resultado switch
            {
                ResultadoBeca.Cumple => "cumple",
                ResultadoBeca.NoCumple => "no-cumple",
                _ => "no-determinable",
            });
            w.WriteStartArray("requisitos");
            foreach (var r in evaluacion.Requisitos)
            {
                w.WriteStartObject();
                w.WriteString("requisito", r.Requisito switch
                {
                    RequisitoBeca.Promedio => "promedio",
                    RequisitoBeca.SinAsignaturasPerdidas => "sin-asignaturas-perdidas",
                    _ => "creditos-matriculados",
                });
                w.WriteString("estado", r.Estado switch
                {
                    EstadoRequisito.Cumple => "cumple",
                    EstadoRequisito.NoCumple => "no-cumple",
                    _ => "no-determinable",
                });
                PeriodoAcademico.Num(w, "observado", r.Observado);
                PeriodoAcademico.Num(w, "exigido", r.Exigido);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        });
    }

    /// <summary>Asignatura perdida con nota y motivo ("nota" o "inasistencia").</summary>
    public static void EscribirPerdida(System.Text.Json.Utf8JsonWriter w, AsignaturaPeriodo a, string nombre)
    {
        w.WriteStartObject();
        w.WriteString("codigo", a.Codigo);
        w.WriteString("asignatura", nombre);
        PeriodoAcademico.Num(w, "definitivaEfectiva", a.DefinitivaEfectiva);
        w.WriteString("motivo", a.PierdePorInasistencia == true ? "inasistencia" : "nota");
        w.WriteBoolean("habilitable", a.Habilitable);
        w.WriteEndObject();
    }

    private static List<AsignaturaConNombre> Filtrar(List<AsignaturaConNombre> periodo, string? filtro)
    {
        if (string.IsNullOrWhiteSpace(filtro))
        {
            return periodo;
        }

        var f = Normalizar(filtro);
        var exactas = periodo.Where(p => Normalizar(p.Nombre) == f || Normalizar(p.Dominio.Codigo) == f).ToList();
        return exactas.Count > 0
            ? exactas
            : periodo.Where(p => Normalizar(p.Nombre).Contains(f, StringComparison.Ordinal)).ToList();
    }

    private static string Normalizar(string texto)
    {
        var sb = new StringBuilder();
        foreach (var c in texto.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }
}
