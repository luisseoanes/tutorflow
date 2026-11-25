using TutorFlow.Domain.Academico;

namespace TutorFlow.Domain.Tests;

public class AcademicoTests
{
    public static IEnumerable<object[]> FilasGolden() =>
        DatosGolden.Filas.Select(f => new object[] { f.Est, f.Cod });

    // ---- Tabla golden completa (24 filas) -------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(FilasGolden))]
    [Trait("Criterio", "ACAD-01.a")]
    [Trait("Criterio", "ACAD-08.a")]
    [Trait("Criterio", "ACAD-12.a")]
    [Trait("Criterio", "ACAD-13.a")]
    public void Tabla_golden_fila(string est, string cod)
    {
        var f = DatosGolden.Filas.Single(x => x.Est == est && x.Cod == cod);
        var a = DatosGolden.Asignatura(est, cod);

        Assert.Equal(f.Def, a.Definitiva);
        Assert.Equal(f.Pierde, a.PierdePorInasistencia);
        Assert.Equal(f.Efectiva, a.DefinitivaEfectiva);
    }

    [Fact]
    [Trait("Criterio", "ACAD-01.a")]
    public void Tabla_golden_tiene_24_filas() => Assert.Equal(24, DatosGolden.Filas.Length);

    [Theory]
    [InlineData("EST-001", "4.39")]
    [InlineData("EST-002", "2.28")]
    [InlineData("EST-003", "4.11")]
    [InlineData("EST-004", "3.88")]
    [InlineData("EST-005", "2.65")]
    [InlineData("EST-006", "4.05")]
    [Trait("Criterio", "ACAD-01.a")]
    [Trait("Criterio", "ACAD-09.a")]
    [Trait("Criterio", "ACAD-14.a")]
    [Trait("Criterio", "ACAD-19.a")]
    public void Tabla_golden_promedios(string est, string esperado)
    {
        var periodo = DatosGolden.Periodo(est);

        Assert.Equal(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture), ResumenAcademico.Promedio(periodo));
        Assert.Equal(12, ResumenAcademico.CreditosMatriculados(periodo));
    }

    // ---- ACAD-01 ---------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Criterio", "ACAD-01.a")]
    public void EST001_promedio_4_39_y_definitivas()
    {
        var periodo = DatosGolden.Periodo("EST-001");

        Assert.Equal(4.39m, ResumenAcademico.Promedio(periodo));
        Assert.Equal(new decimal?[] { 4.5m, 4.8m, 4.1m, 4.0m }, periodo.Select(a => a.Definitiva).ToArray());
    }

    // ---- ACAD-02..04: puntos medios -------------------------------------------------------------------------

    [Fact]
    [Trait("Criterio", "ACAD-02.a")]
    public void Definitiva_4_2_4_5_4_6_es_4_5() => Assert.Equal(4.5m, Cortes.Definitiva(4.2m, 4.5m, 4.6m));

    [Fact]
    [Trait("Criterio", "ACAD-03.a")]
    public void Definitiva_4_5_4_4_4_7_es_4_6() => Assert.Equal(4.6m, Cortes.Definitiva(4.5m, 4.4m, 4.7m));

    [Fact]
    [Trait("Criterio", "ACAD-04.a")]
    public void Definitiva_4_1_4_0_4_3_es_4_2() => Assert.Equal(4.2m, Cortes.Definitiva(4.1m, 4.0m, 4.3m));

    // ---- ACAD-05.a: redondeo ----------------------------------------------------------------------------------

    [Theory]
    [InlineData("4.45", "4.5")]
    [InlineData("4.35", "4.4")]
    [InlineData("2.25", "2.3")]
    [InlineData("4.44", "4.4")]
    [InlineData("0.05", "0.1")]
    [Trait("Criterio", "ACAD-05.a")]
    public void Redondeo_de_definitivas(string bruta, string esperado) =>
        Assert.Equal(D(esperado), Redondeo.Definitiva(D(bruta)));

    [Theory]
    [InlineData("2.275", "2.28")]
    [InlineData("2.265", "2.27")]
    [InlineData("4.005", "4.01")]
    [Trait("Criterio", "ACAD-05.a")]
    public void Redondeo_de_promedios(string valor, string esperado) =>
        Assert.Equal(D(esperado), Redondeo.Promedio(D(valor)));

    private static decimal D(string s) => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    // ---- ACAD-06.a ---------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Criterio", "ACAD-06.a")]
    public void EST004_contabilidad_definitiva_3_9_bruta_3_89()
    {
        // Bruta 3.89 copiada del escenario (no calculada por el codigo probado).
        Assert.Equal(3.9m, Redondeo.Definitiva(3.89m));
        Assert.Equal(3.9m, DatosGolden.Asignatura("EST-004", "CON-101").Definitiva);
    }

    // ---- ACAD-08.a / ACAD-09.a -------------------------------------------------------------------------------

    [Fact]
    [Trait("Criterio", "ACAD-08.a")]
    public void EST002_BD_pierde_por_inasistencia_9_de_48()
    {
        var bd = DatosGolden.Asignatura("EST-002", "BD-203");

        Assert.True(bd.PierdePorInasistencia);
        Assert.Equal(0.0m, bd.DefinitivaEfectiva);
        Assert.False(bd.Habilitable);
        Assert.True(bd.Perdida);
        Assert.False(bd.Aprobada);
    }

    [Fact]
    [Trait("Criterio", "ACAD-09.a")]
    public void EST002_promedio_2_28_no_3_23()
    {
        var promedio = ResumenAcademico.Promedio(DatosGolden.Periodo("EST-002"));

        Assert.Equal(2.28m, promedio);
        Assert.NotEqual(3.23m, promedio);
    }

    // ---- ACAD-10.a: limite exacto del 15% ---------------------------------------------------------------------

    [Theory]
    [InlineData(40, 6, false)]
    [InlineData(20, 3, false)]
    [InlineData(48, 7, false)]
    [InlineData(48, 8, true)]
    [InlineData(64, 9, false)]
    [InlineData(64, 10, true)]
    [InlineData(32, 4, false)]
    [InlineData(32, 5, true)]
    [Trait("Criterio", "ACAD-10.a")]
    public void Limite_exacto_del_15_por_ciento(int clases, int faltas, bool pierde) =>
        Assert.Equal(pierde, Inasistencia.Pierde(clases, faltas));

    // ---- ACAD-12.a ---------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Criterio", "ACAD-12.a")]
    public void EST003_ingles_sin_asistencia_es_desconocido_y_definitiva_4_6()
    {
        var ing = DatosGolden.Asignatura("EST-003", "ING-204");

        Assert.False(ing.DatoAsistenciaDisponible);
        Assert.Null(ing.PierdePorInasistencia);
        Assert.Equal(4.6m, ing.Definitiva);
        Assert.Equal(4.6m, ing.DefinitivaEfectiva);
        Assert.Null(Inasistencia.Pierde(32, null));
    }

    // ---- ACAD-13.a ---------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Criterio", "ACAD-13.a")]
    public void EST006_estadistica_sin_definitiva_aporte_2_40_evaluado_60()
    {
        var est = DatosGolden.Asignatura("EST-006", "EST-103");

        Assert.Null(est.Definitiva);
        Assert.Null(est.DefinitivaEfectiva);
        Assert.Equal(2.40m, est.AporteAcumulado);
        Assert.Equal(60, est.PorcentajeEvaluado);
        Assert.False(est.Aprobada);
    }

    // ---- ACAD-14.a ---------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Criterio", "ACAD-14.a")]
    public void EST006_promedio_4_05_excluye_la_asignatura_sin_definitiva()
    {
        var promedio = ResumenAcademico.Promedio(DatosGolden.Periodo("EST-006"));

        Assert.Equal(4.05m, promedio);
        Assert.NotEqual(2.70m, promedio);
    }

    [Fact]
    [Trait("Criterio", "ACAD-14.a")]
    public void Sin_ninguna_definitiva_el_promedio_no_esta_disponible()
    {
        var solo = new[] { new AsignaturaPeriodo("X-1", 3, 4.0m, null, null, 48, 0) };

        Assert.Null(ResumenAcademico.Promedio(solo));
        Assert.Equal(SituacionAcademica.NoDisponible, ResumenAcademico.Situacion(null));
    }

    // ---- ACAD-15.a ---------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Criterio", "ACAD-15.a")]
    public void EST005_estadistica_habilitable_microeconomia_no()
    {
        var est = DatosGolden.Asignatura("EST-005", "EST-103");
        var mic = DatosGolden.Asignatura("EST-005", "MIC-102");

        Assert.Equal(2.3m, est.DefinitivaEfectiva);
        Assert.True(est.Perdida);
        Assert.True(est.Habilitable);
        Assert.Equal(1.8m, mic.DefinitivaEfectiva);
        Assert.True(mic.Perdida);
        Assert.False(mic.Habilitable);
    }

    [Fact]
    [Trait("Criterio", "ACAD-15.a")]
    public void Perdidas_de_EST002_y_aprobacion_Art5()
    {
        var periodo = DatosGolden.Periodo("EST-002");

        Assert.Equal(new[] { "CAL-201", "BD-203" }, periodo.Where(a => a.Perdida).Select(a => a.Codigo).ToArray());
        Assert.True(periodo.Single(a => a.Codigo == "CAL-201").Habilitable);
        Assert.False(periodo.Single(a => a.Codigo == "BD-203").Habilitable);
        Assert.True(periodo.Single(a => a.Codigo == "POO-202").Aprobada);
    }

    // ---- ACAD-16.a / ACAD-17.a: beca --------------------------------------------------------------------------

    [Fact]
    [Trait("Criterio", "ACAD-16.a")]
    public void EST001_beca_cumple()
    {
        var beca = Beca.Evaluar(DatosGolden.Periodo("EST-001"));

        Assert.Equal(ResultadoBeca.Cumple, beca.Resultado);
        Assert.All(beca.Requisitos, r => Assert.Equal(EstadoRequisito.Cumple, r.Estado));
        Assert.Equal(4.39m, beca.Requisitos.Single(r => r.Requisito == RequisitoBeca.Promedio).Observado);
        Assert.Equal(12m, beca.Requisitos.Single(r => r.Requisito == RequisitoBeca.CreditosMatriculados).Observado);
    }

    [Fact]
    [Trait("Criterio", "ACAD-17.a")]
    public void EST004_beca_no_cumple_por_promedio_3_88()
    {
        var beca = Beca.Evaluar(DatosGolden.Periodo("EST-004"));

        Assert.Equal(ResultadoBeca.NoCumple, beca.Resultado);
        var promedio = beca.Requisitos.Single(r => r.Requisito == RequisitoBeca.Promedio);
        Assert.Equal(EstadoRequisito.NoCumple, promedio.Estado);
        Assert.Equal(3.88m, promedio.Observado);
        Assert.Equal(EstadoRequisito.Cumple, beca.Requisitos.Single(r => r.Requisito == RequisitoBeca.SinAsignaturasPerdidas).Estado);
        Assert.Equal(EstadoRequisito.Cumple, beca.Requisitos.Single(r => r.Requisito == RequisitoBeca.CreditosMatriculados).Estado);
    }

    [Theory]
    [InlineData("EST-002")]
    [InlineData("EST-003")]
    [InlineData("EST-005")]
    [InlineData("EST-006")]
    [Trait("Criterio", "ACAD-17.a")]
    public void Tabla_golden_beca_no_cumple_para_el_resto(string est)
    {
        var beca = Beca.Evaluar(DatosGolden.Periodo(est));

        Assert.Equal(ResultadoBeca.NoCumple, beca.Resultado);
    }

    [Fact]
    [Trait("Criterio", "ACAD-16.a")]
    public void Beca_no_determinable_con_corte_pendiente_o_asistencia_desconocida_si_nada_falla()
    {
        var pendiente = new[]
        {
            new AsignaturaPeriodo("A-1", 6, 4.8m, 4.8m, 4.8m, 48, 0),
            new AsignaturaPeriodo("A-2", 6, 4.8m, 4.8m, null, 48, 0),
        };
        var sinAsistencia = new[]
        {
            new AsignaturaPeriodo("A-1", 6, 4.8m, 4.8m, 4.8m, 48, 0),
            new AsignaturaPeriodo("A-2", 6, 4.8m, 4.8m, 4.8m, 48, null),
        };

        Assert.Equal(ResultadoBeca.NoDeterminable, Beca.Evaluar(pendiente).Resultado);
        Assert.Equal(ResultadoBeca.NoDeterminable, Beca.Evaluar(sinAsistencia).Resultado);
    }

    [Fact]
    [Trait("Criterio", "ACAD-17.a")]
    public void Beca_no_cumple_si_los_creditos_son_menos_de_12_aunque_haya_pendientes()
    {
        var pocos = new[]
        {
            new AsignaturaPeriodo("A-1", 6, 4.8m, 4.8m, 4.8m, 48, 0),
            new AsignaturaPeriodo("A-2", 5, 4.8m, null, null, 48, 0),
        };

        Assert.Equal(ResultadoBeca.NoCumple, Beca.Evaluar(pocos).Resultado);
    }

    // ---- ACAD-19.a ---------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Criterio", "ACAD-19.a")]
    public void EST005_promedio_2_65_prueba_academica()
    {
        var promedio = ResumenAcademico.Promedio(DatosGolden.Periodo("EST-005"));

        Assert.Equal(2.65m, promedio);
        Assert.Equal(SituacionAcademica.PruebaAcademica, ResumenAcademico.Situacion(promedio));
        Assert.Equal(SituacionAcademica.Normal, ResumenAcademico.Situacion(3.0m));
        Assert.Equal(SituacionAcademica.Normal, ResumenAcademico.Situacion(
            ResumenAcademico.Promedio(DatosGolden.Periodo("EST-001"))));
    }

    // ---- Datos corruptos (SPEC 5.1, 5.3) -----------------------------------------------------------------------

    [Fact]
    public void Notas_fuera_de_rango_son_dato_corrupto()
    {
        Assert.Throws<DatoAcademicoInvalidoException>(() => Cortes.Definitiva(6.2m, 4.0m, 4.0m));
        Assert.Throws<DatoAcademicoInvalidoException>(() => Cortes.Definitiva(4.0m, -0.1m, 4.0m));
        Assert.Throws<DatoAcademicoInvalidoException>(() => new AsignaturaPeriodo("X-1", 3, 4.0m, 4.0m, 5.1m, 48, 0));
        Assert.Equal(0.0m, Cortes.Definitiva(0.0m, 0.0m, 0.0m));
        Assert.Equal(5.0m, Cortes.Definitiva(5.0m, 5.0m, 5.0m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void Clases_programadas_no_positivas_son_dato_corrupto(int clases) =>
        Assert.Throws<DatoAcademicoInvalidoException>(() => Inasistencia.Pierde(clases, 1));

    [Fact]
    public void Clases_nulas_creditos_no_positivos_e_inasistencias_negativas_son_dato_corrupto()
    {
        Assert.Throws<DatoAcademicoInvalidoException>(() => Inasistencia.Pierde(null, 1));
        Assert.Throws<DatoAcademicoInvalidoException>(() => Inasistencia.Pierde(48, -1));
        Assert.Throws<DatoAcademicoInvalidoException>(() => new AsignaturaPeriodo("X-1", -1, 4.0m, 4.0m, 4.0m, 48, 0));
        Assert.Throws<DatoAcademicoInvalidoException>(() => new AsignaturaPeriodo("X-1", 3, 4.0m, 4.0m, 4.0m, 0, 0));
    }
}
