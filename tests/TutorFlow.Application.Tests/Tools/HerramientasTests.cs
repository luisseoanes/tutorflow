using System.Globalization;
using System.Text.Json;
using TutorFlow.Application.Errores;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Tools;
using TutorFlow.Domain.Academico;
using TutorFlow.Domain.Authorization;
using static TutorFlow.Application.Tests.Tools.Armado;
using static TutorFlow.Application.Tests.Tools.MaterialDatos;

namespace TutorFlow.Application.Tests.Tools;

public class HerramientasTests
{
    private const string Evaluacion = "evaluacion_y_calificaciones.md";
    private const string Reglamento = "reglamento_academico_2025.md";

    // ---------------------------------------------------------------- L3 y L4

    [Fact]
    [Trait("Criterio", "AUTH-06.c")]
    public async Task El_estudiante_no_ve_ni_puede_invocar_las_herramientas_de_programa()
    {
        var (d, f) = Crear();

        var nombres = d.HerramientasExpuestas(Est("EST-001")).Select(t => t.Nombre).ToList();
        Assert.Equal(
            ["obtener_notas", "obtener_asistencia", "obtener_resumen_academico", "evaluar_beca_rendimiento"],
            nombres);
        Assert.DoesNotContain("listar_estudiantes_programa", nombres);
        Assert.DoesNotContain("asignaturas_perdidas_programa", nombres);

        foreach (var herramienta in new[] { "listar_estudiantes_programa", "asignaturas_perdidas_programa" })
        {
            var ex = await Assert.ThrowsAsync<AccesoDenegadoException>(
                () => d.DespacharAsync(Est("EST-001"), Inv(herramienta), default));
            Assert.Equal("herramienta", ex.Motivo);
            Assert.Equal(RolAcademico.Estudiante, ex.Rol);
        }

        Assert.Empty(f.Lecturas);

        // El coordinador ve las seis, con nombres exactos de SPEC 8.
        Assert.Equal(
            [
                "obtener_notas", "obtener_asistencia", "obtener_resumen_academico", "evaluar_beca_rendimiento",
                "listar_estudiantes_programa", "asignaturas_perdidas_programa",
            ],
            d.HerramientasExpuestas(Coord1()).Select(t => t.Nombre).ToList());
    }

    [Fact]
    [Trait("Criterio", "AUTH-23.c")]
    public async Task Objetivo_ausente_del_roster_se_deniega_sin_leer()
    {
        var (d, f) = Crear();

        var ex = await Assert.ThrowsAsync<AccesoDenegadoException>(
            () => d.DespacharAsync(Coord1(), Inv("obtener_notas", """{"estudianteId":"EST-007"}"""), default));

        Assert.Equal(RolAcademico.Coordinador, ex.Rol);
        Assert.Equal("politica", ex.Motivo);
        Assert.Empty(f.Lecturas);
    }

    [Theory]
    [InlineData("obtener_notas", """{"estudianteId":"EST-005"}""", "politica")]
    [InlineData("herramienta_inexistente", "{}", "herramienta")]
    [Trait("Criterio", "ADV-11.a")]
    public async Task Las_invocaciones_manipuladas_de_L3_y_L4_se_rechazan_sin_llegar_a_la_fuente(string nombre, string args, string motivo)
    {
        var (d, f) = Crear();

        var ex = await Assert.ThrowsAsync<AccesoDenegadoException>(() => d.DespacharAsync(Coord1(), Inv(nombre, args), default));

        Assert.Equal(motivo, ex.Motivo);
        Assert.Equal(RolAcademico.Coordinador, ex.Rol);
        Assert.Empty(f.Lecturas);
    }

    // D-15: la forma invalida ya no aborta con 403; se devuelve al LLM como error generico, sin llegar a la fuente.
    [Theory]
    [InlineData("obtener_notas", """{"estudianteId":"EST-002","extra":"admin"}""")]
    [InlineData("obtener_notas", """{"estudianteId":["EST-002","EST-005"]}""")]
    [InlineData("obtener_notas", """{"estudianteId":"../programas/ADM/estudiantes"}""")]
    [InlineData("obtener_notas", """{"estudianteId":"Laura Gómez"}""")]
    [InlineData("obtener_notas", """{"estudianteId":"EST-002","estudianteId":"EST-005"}""")]
    [InlineData("listar_estudiantes_programa", """{"programaId":"ADM","x":1}""")]
    [Trait("Criterio", "ADV-11.a")]
    [Trait("Criterio", "ADV-11.b")]
    public async Task Las_invocaciones_de_forma_invalida_se_devuelven_como_error_generico_sin_leer_ni_eco(string nombre, string args)
    {
        var (d, f) = Crear();

        var r = await d.DespacharAsync(Coord1(), Inv(nombre, args), default);

        Assert.Empty(f.Lecturas);
        Assert.False(r.UsoDatosEstudiante);
        Assert.Null(r.Objetivo);
        Assert.Empty(r.Citas);
        using var doc = JsonDocument.Parse(r.Contenido);
        var error = doc.RootElement.GetProperty("error").GetString()!;
        Assert.Equal($"Argumentos no válidos para {nombre}: usa ids con letras, dígitos y guiones, u omite estudianteId para consultar tus propios datos.", error);
        foreach (var eco in new[] { "EST-002", "EST-005", "Laura", "Gómez", "admin", "extra", "programas", "ADM" })
        {
            Assert.DoesNotContain(eco, error.Replace(nombre, string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Criterio", "ADV-11.a")]
    public async Task Programa_ajeno_sigue_denegado_sin_leer()
    {
        var (d, f) = Crear();

        var ajeno = await Assert.ThrowsAsync<AccesoDenegadoException>(
            () => d.DespacharAsync(Coord2(), Inv("asignaturas_perdidas_programa", """{"programaId":"ING-SIS"}"""), default));
        Assert.Equal("politica", ajeno.Motivo);

        Assert.Empty(f.Lecturas);
    }

    // INT-021: el coordinador sin estudianteId es error de forma (argumento ausente), no 403; mensaje fijo y sin lecturas.
    [Theory]
    [InlineData("obtener_notas")]
    [InlineData("obtener_asistencia")]
    [InlineData("obtener_resumen_academico")]
    [InlineData("evaluar_beca_rendimiento")]
    [Trait("Criterio", "ADV-11.b")]
    public async Task El_coordinador_sin_estudianteId_recibe_error_de_forma_sin_leer(string nombre)
    {
        var (d, f) = Crear();

        var r = await d.DespacharAsync(Coord1(), Inv(nombre), default);

        Assert.Empty(f.Lecturas);
        Assert.False(r.UsoDatosEstudiante);
        Assert.Null(r.Objetivo);
        Assert.Empty(r.Citas);
        using var doc = JsonDocument.Parse(r.Contenido);
        Assert.Equal(
            $"Falta el argumento estudianteId para {nombre}: debes proporcionar el id del estudiante a consultar.",
            doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task El_estudiante_usa_su_propio_id_si_lo_omite_y_el_de_otro_se_deniega()
    {
        var (d, f) = Crear();

        var propio = await d.DespacharAsync(Est("EST-001"), Inv("obtener_notas"), default);
        Assert.Equal("EST-001", propio.Objetivo);
        Assert.True(propio.UsoDatosEstudiante);
        Assert.Equal(["notas:EST-001", "asistencia:EST-001"], f.Lecturas);

        f.Lecturas.Clear();
        var ex = await Assert.ThrowsAsync<AccesoDenegadoException>(
            () => d.DespacharAsync(Est("EST-001"), Inv("obtener_notas", """{"estudianteId":"EST-002"}"""), default));
        Assert.Equal("politica", ex.Motivo);
        Assert.Empty(f.Lecturas);
    }

    [Fact]
    public async Task El_estudiante_propio_ausente_del_roster_se_deniega_sin_leer()
    {
        var (d, f) = Crear(ausenteDelRoster: "EST-001");

        var ex = await Assert.ThrowsAsync<AccesoDenegadoException>(
            () => d.DespacharAsync(Est("EST-001"), Inv("obtener_notas"), default));

        Assert.Equal("politica", ex.Motivo);
        Assert.Empty(f.Lecturas);
    }

    [Fact]
    public void Los_esquemas_son_estrictos_y_distinguen_el_rol()
    {
        var (d, _) = Crear();

        var estudiante = d.HerramientasExpuestas(Est("EST-001")).Single(t => t.Nombre == "obtener_notas").EsquemaArgumentos;
        var coordinador = d.HerramientasExpuestas(Coord1()).Single(t => t.Nombre == "obtener_notas").EsquemaArgumentos;

        Assert.False(estudiante.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(0, estudiante.GetProperty("required").GetArrayLength());
        Assert.Equal("estudianteId", coordinador.GetProperty("required")[0].GetString());
        Assert.True(estudiante.GetProperty("properties").TryGetProperty("estudianteId", out _));
    }

    // ---------------------------------------------------------------- Citas y resultados

    [Fact]
    [Trait("Criterio", "AUTH-16.a")]
    public async Task El_coordinador_consulta_un_estudiante_de_su_programa()
    {
        var (d, f) = Crear();

        var notas = await d.DespacharAsync(Coord1(), Inv("obtener_notas", """{"estudianteId":"EST-002"}"""), default);
        using var doc = Contenido(notas);
        var bd = Asignatura(doc, "BD-203");
        Assert.Equal("perdida-inasistencia", bd.GetProperty("estado").GetString());
        Assert.Equal("0.0", bd.GetProperty("definitivaEfectiva").GetRawText());
        Assert.Equal("3.8", bd.GetProperty("definitiva").GetRawText());
        Assert.False(bd.GetProperty("habilitable").GetBoolean());
        Assert.Equal("EST-002", notas.Objetivo);
        Assert.True(notas.UsoDatosEstudiante);
        Assert.True(Cita(notas, Reglamento, "Artículo 11"));

        var asistencia = await d.DespacharAsync(Coord1(), Inv("obtener_asistencia", """{"estudianteId":"EST-002","asignatura":"Bases de Datos"}"""), default);
        using var docA = Contenido(asistencia);
        var fila = Asignatura(docA, "BD-203");
        Assert.Equal(9, fila.GetProperty("inasistencias").GetInt32());
        Assert.Equal(48, fila.GetProperty("clasesProgramadas").GetInt32());
        Assert.True(fila.GetProperty("pierdePorInasistencia").GetBoolean());
        Assert.Equal("18.75", fila.GetProperty("porcentajeInasistencia").GetRawText());
        Assert.Single(docA.RootElement.GetProperty("asignaturas").EnumerateArray());
        Assert.DoesNotContain(f.Lecturas, l => l.EndsWith("EST-005", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Criterio", "AUTH-19.a")]
    public async Task El_coordinador_de_ADM_recibe_exactamente_a_Valentina_Castro()
    {
        var (d, f) = Crear();

        var r = await d.DespacharAsync(Coord2(), Inv("asignaturas_perdidas_programa"), default);

        using var doc = Contenido(r);
        var programas = doc.RootElement.GetProperty("programas").EnumerateArray().ToList();
        var programa = Assert.Single(programas);
        Assert.Equal("ADM", programa.GetProperty("programaId").GetString());
        var estudiante = Assert.Single(programa.GetProperty("estudiantes").EnumerateArray());
        Assert.Equal("EST-005", estudiante.GetProperty("id").GetString());
        Assert.Equal("Valentina Castro", estudiante.GetProperty("nombre").GetString());
        var perdidas = estudiante.GetProperty("asignaturasPerdidas").EnumerateArray()
            .Select(a => (a.GetProperty("asignatura").GetString(), a.GetProperty("definitivaEfectiva").GetRawText(), a.GetProperty("motivo").GetString()))
            .ToList();
        Assert.Equal([("Microeconomía", "1.8", "nota"), ("Estadística", "2.3", "nota")], perdidas);
        Assert.DoesNotContain("Camilo", r.Contenido, StringComparison.Ordinal);
        Assert.DoesNotContain("Mateo", r.Contenido, StringComparison.Ordinal);
        Assert.True(Cita(r, Reglamento, "Artículo 5"));
        Assert.True(Cita(r, "Datos del programa", "ADM"));
        Assert.True(r.UsoDatosEstudiante);
        Assert.Equal("ADM", r.Objetivo);
        Assert.DoesNotContain(f.Lecturas, l => l == "programa:ING-SIS" || ING.Any(id => l.EndsWith(id, StringComparison.Ordinal)));
    }

    [Fact]
    [Trait("Criterio", "AUTH-20.a")]
    public async Task El_coordinador_de_ING_SIS_recibe_exactamente_a_Andres_Martinez_con_la_perdida_por_inasistencia()
    {
        var (d, _) = Crear();

        var r = await d.DespacharAsync(Coord1(), Inv("asignaturas_perdidas_programa"), default);

        using var doc = Contenido(r);
        var estudiante = Assert.Single(doc.RootElement.GetProperty("programas")[0].GetProperty("estudiantes").EnumerateArray());
        Assert.Equal("Andrés Martínez", estudiante.GetProperty("nombre").GetString());
        var perdidas = estudiante.GetProperty("asignaturasPerdidas").EnumerateArray()
            .Select(a => (a.GetProperty("asignatura").GetString(), a.GetProperty("definitivaEfectiva").GetRawText(), a.GetProperty("motivo").GetString()))
            .ToList();
        Assert.Equal([("Cálculo Integral", "2.6", "nota"), ("Bases de Datos", "0.0", "inasistencia")], perdidas);
        Assert.DoesNotContain("Laura", r.Contenido, StringComparison.Ordinal);
        Assert.DoesNotContain("Sofía", r.Contenido, StringComparison.Ordinal);
        Assert.True(Cita(r, Reglamento, "Artículo 5"));
        Assert.True(Cita(r, Reglamento, "Artículo 11"));
    }

    [Fact]
    [Trait("Criterio", "ACAD-01.b")]
    public async Task El_resumen_de_EST_001_devuelve_4_39_con_sus_citas()
    {
        var (d, _) = Crear();

        var r = await d.DespacharAsync(Est("EST-001"), Inv("obtener_resumen_academico"), default);

        using var doc = Contenido(r);
        Assert.Equal("4.39", doc.RootElement.GetProperty("promedio").GetRawText());
        Assert.Equal(12, doc.RootElement.GetProperty("creditosMatriculados").GetInt32());
        Assert.Equal("normal", doc.RootElement.GetProperty("situacionAcademica").GetString());
        Assert.True(Cita(r, Evaluacion, "Sección 3"));
        Assert.True(Cita(r, Evaluacion, "Sección 4"));
        Assert.True(Cita(r, "Datos del estudiante", "Resumen académico"));
        Assert.True(r.UsoDatosEstudiante);
    }

    [Fact]
    [Trait("Criterio", "ACAD-06.b")]
    public async Task La_definitiva_de_Contabilidad_de_EST_004_cita_notas_y_seccion_3()
    {
        var (d, _) = Crear();

        var r = await d.DespacharAsync(Est("EST-004"), Inv("obtener_notas"), default);

        using var doc = Contenido(r);
        Assert.Equal("3.9", Asignatura(doc, "CON-101").GetProperty("definitiva").GetRawText());
        Assert.True(Cita(r, "Datos del estudiante", "Notas"));
        Assert.True(Cita(r, Evaluacion, "Sección 3"));
    }

    [Fact]
    [Trait("Criterio", "ACAD-08.b")]
    public async Task La_perdida_por_inasistencia_cita_el_articulo_11_de_2025_y_nunca_el_2023()
    {
        var (d, _) = Crear();

        var r = await d.DespacharAsync(Est("EST-002"), Inv("obtener_notas"), default);

        Assert.True(Cita(r, Reglamento, "Artículo 11"));
        Assert.DoesNotContain(r.Citas, c => c.Documento.Contains("2023", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Criterio", "ACAD-12.b")]
    public async Task La_asistencia_no_registrada_cita_el_articulo_12()
    {
        var (d, _) = Crear();

        var r = await d.DespacharAsync(Est("EST-003"), Inv("obtener_asistencia", """{"asignatura":"Inglés III"}"""), default);

        Assert.True(Cita(r, Reglamento, "Artículo 12"));
        Assert.True(Cita(r, "Datos del estudiante", "Asistencia"));
        using var doc = Contenido(r);
        var ing = Assert.Single(doc.RootElement.GetProperty("asignaturas").EnumerateArray());
        Assert.False(ing.GetProperty("datoDisponible").GetBoolean());
        Assert.Equal(JsonValueKind.Null, ing.GetProperty("pierdePorInasistencia").ValueKind);
        Assert.Equal(JsonValueKind.Null, ing.GetProperty("inasistencias").ValueKind);
    }

    [Fact]
    [Trait("Criterio", "ACAD-13.b")]
    public async Task El_corte_pendiente_no_se_presenta_como_definitiva_y_cita_la_seccion_3()
    {
        var (d, _) = Crear();

        var r = await d.DespacharAsync(Est("EST-006"), Inv("obtener_notas"), default);

        Assert.True(Cita(r, Evaluacion, "Sección 3"));
        using var doc = Contenido(r);
        var est = Asignatura(doc, "EST-103");
        Assert.Equal(JsonValueKind.Null, est.GetProperty("definitiva").ValueKind);
        Assert.Equal("2.40", est.GetProperty("aporteAcumulado").GetRawText());
        Assert.Equal(60, est.GetProperty("porcentajeEvaluado").GetInt32());
        Assert.Equal("en-curso", est.GetProperty("estado").GetString());
    }

    [Fact]
    [Trait("Criterio", "ACAD-15.b")]
    public async Task La_habilitacion_cita_el_articulo_6_y_aplica_el_umbral_2025()
    {
        var (d, _) = Crear();

        var r = await d.DespacharAsync(Est("EST-005"), Inv("obtener_notas"), default);

        Assert.True(Cita(r, Reglamento, "Artículo 6"));
        using var doc = Contenido(r);
        Assert.True(Asignatura(doc, "EST-103").GetProperty("habilitable").GetBoolean());
        Assert.False(Asignatura(doc, "MIC-102").GetProperty("habilitable").GetBoolean());
    }

    [Fact]
    [Trait("Criterio", "ACAD-16.b")]
    public async Task La_beca_cita_la_seccion_3_1_y_EST_001_cumple()
    {
        var (d, _) = Crear();

        var r = await d.DespacharAsync(Est("EST-001"), Inv("evaluar_beca_rendimiento"), default);

        Assert.True(Cita(r, "bienestar_y_becas.md", "Sección 3.1"));
        using var doc = Contenido(r);
        Assert.Equal("cumple", doc.RootElement.GetProperty("resultado").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("requisitos").GetArrayLength());
    }

    [Fact]
    [Trait("Criterio", "ACAD-19.b")]
    public async Task La_situacion_academica_cita_el_articulo_20()
    {
        var (d, _) = Crear();

        var r = await d.DespacharAsync(Est("EST-005"), Inv("obtener_resumen_academico"), default);

        Assert.True(Cita(r, Reglamento, "Artículo 20"));
        using var doc = Contenido(r);
        Assert.Equal("2.65", doc.RootElement.GetProperty("promedio").GetRawText());
        Assert.Equal("prueba-academica", doc.RootElement.GetProperty("situacionAcademica").GetString());
    }

    [Fact]
    public async Task Las_citas_van_deduplicadas_normativas_primero_y_datos_despues()
    {
        var (d, _) = Crear();

        var r = await d.DespacharAsync(Est("EST-002"), Inv("obtener_resumen_academico"), default);

        Assert.Equal(r.Citas.Count, r.Citas.Distinct().Count());
        var ultimaNormativa = r.Citas.ToList().FindLastIndex(c => c.Documento.EndsWith(".md", StringComparison.Ordinal));
        var primeraDato = r.Citas.ToList().FindIndex(c => c.Documento.StartsWith("Datos del", StringComparison.Ordinal));
        Assert.True(ultimaNormativa < primeraDato);
    }

    // ---------------------------------------------------------------- Dato corrupto y cultura

    [Theory]
    [InlineData("corte")]
    [InlineData("clases")]
    [InlineData("creditos")]
    [Trait("Criterio", "ACAD-20.a")]
    public async Task El_dato_corrupto_se_propaga_como_error_tipado_sin_cifras(string tipo)
    {
        foreach (var herramienta in new[] { "obtener_notas", "obtener_asistencia", "obtener_resumen_academico", "evaluar_beca_rendimiento" })
        {
            var (d, f) = Crear();
            switch (tipo)
            {
                case "corte":
                    f.CorromperNotas = (_, n) => n.Select((x, i) => i == 0 ? x with { Corte2 = 6.2m } : x).ToList();
                    break;
                case "clases":
                    f.CorromperAsistencia = (_, a) => a.Select((x, i) => i == 0 ? x with { ClasesProgramadas = 0 } : x).ToList();
                    break;
                default:
                    f.CorromperNotas = (_, n) => n.Select((x, i) => i == 0 ? x with { Creditos = -1 } : x).ToList();
                    break;
            }

            if (herramienta == "obtener_asistencia" && tipo is "corte" or "creditos")
            {
                continue; // la asistencia no lee cortes ni creditos
            }

            var ex = await Record.ExceptionAsync(() => d.DespacharAsync(Est("EST-001"), Inv(herramienta), default));
            Assert.IsType<DatoAcademicoInvalidoException>(ex);
            Assert.DoesNotContain("6.2", ex.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Criterio", "ACAD-20.a")]
    public async Task El_dato_corrupto_en_un_listado_de_programa_tambien_es_un_error_tipado()
    {
        var (d, f) = Crear();
        f.CorromperNotas = (e, n) => e == "EST-005" ? n.Select((x, i) => i == 1 ? x with { Corte1 = -0.1m } : x).ToList() : n;

        var ex = await Record.ExceptionAsync(() => d.DespacharAsync(Coord2(), Inv("asignaturas_perdidas_programa"), default));

        Assert.IsType<DatoAcademicoInvalidoException>(ex);
    }

    [Fact]
    [Trait("Criterio", "RES-10.a")]
    public async Task Con_cultura_es_CO_las_herramientas_serializan_con_punto_decimal()
    {
        var anterior = CultureInfo.CurrentCulture;
        var anteriorUi = CultureInfo.CurrentUICulture;
        try
        {
            var es = new CultureInfo("es-CO");
            CultureInfo.CurrentCulture = es;
            CultureInfo.CurrentUICulture = es;
            Assert.Equal("4,39", 4.39m.ToString(CultureInfo.CurrentCulture)); // la cultura realmente usa coma

            var (d, _) = Crear();
            var resumen = await d.DespacharAsync(Est("EST-001"), Inv("obtener_resumen_academico"), default);
            var notas = await d.DespacharAsync(Est("EST-001"), Inv("obtener_notas"), default);
            var asistencia = await d.DespacharAsync(Coord1(), Inv("obtener_asistencia", """{"estudianteId":"EST-002"}"""), default);

            Assert.Contains("\"promedio\":4.39", resumen.Contenido, StringComparison.Ordinal);
            Assert.DoesNotContain("4,39", resumen.Contenido, StringComparison.Ordinal);
            Assert.Contains("\"corte1\":4.2", notas.Contenido, StringComparison.Ordinal);
            Assert.DoesNotContain(',', ComoTexto(notas.Contenido, "corte1"));
            Assert.Contains("\"porcentajeInasistencia\":18.75", asistencia.Contenido, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
            CultureInfo.CurrentUICulture = anteriorUi;
        }
    }

    private static string ComoTexto(string json, string propiedad)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("asignaturas")[0].GetProperty(propiedad).GetRawText();
    }

    private static readonly string[] ING = ["EST-001", "EST-002", "EST-003"];
}
