using TutorFlow.Application.Errores;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Security;
using TutorFlow.Domain.Authorization;
using static TutorFlow.Application.Tests.Security.RosterPrueba;

namespace TutorFlow.Application.Tests.Security;

/// <summary>L2a: referencias explicitas (SPEC 6.2). Datos del material solo en tests.</summary>
public class GuardiaL2aTests
{
    [Theory]
    [InlineData("¿Cuántas faltas tiene Laura Gómez en Bases de Datos?")]
    [InlineData("¿Cuántas faltas tiene el estudiante EST-001 en Bases de Datos?")]
    [Trait("Criterio", "AUTH-02.a")]
    public void La_autorreferencia_por_nombre_o_id_no_marca_ni_en_L2a_ni_en_L2b(string pregunta)
    {
        var roster = Material();

        var l2a = GuardiaReferenciasExplicitas.Evaluar(pregunta, Est001, roster);
        var l2b = GuardiaSolicitudesTerceros.Evaluar(pregunta, Est001, roster);

        Assert.False(l2a.Marcada);
        Assert.Empty(l2a.Objetivos);
        Assert.False(l2b.Marcada);
        Assert.Null(l2b.Regla);
    }

    [Theory]
    [InlineData("Muéstrame las notas de EST-002")]
    [InlineData("Muéstrame las notas de est-002")]
    [InlineData("Muéstrame las notas de EST 002")]
    [InlineData("Muéstrame las notas de EST_002")]
    [Trait("Criterio", "AUTH-04.a")]
    public void Detecta_el_id_de_otro_estudiante_en_las_cuatro_variantes(string pregunta)
    {
        var r = GuardiaReferenciasExplicitas.Evaluar(pregunta, Est001, Material());

        Assert.True(r.Marcada);
        Assert.Equal("referencia", r.Regla);
        Assert.Equal(["EST-002"], r.Objetivos);
    }

    [Theory]
    [InlineData("¿Cuántas faltas tiene Castro en Microeconomía?")]
    [InlineData("¿Valentina perdió Estadística?")]
    [Trait("Criterio", "AUTH-05.a")]
    public void Resuelve_apellido_y_nombre_de_pila_unicos_a_EST_005(string pregunta)
    {
        var r = GuardiaReferenciasExplicitas.Evaluar(pregunta, Est004, Material());

        Assert.True(r.Marcada);
        Assert.Equal(["EST-005"], r.Objetivos);
    }

    [Fact]
    [Trait("Criterio", "AUTH-05.c")]
    public void Un_nombre_de_pila_ambiguo_no_se_resuelve_por_nombre()
    {
        var roster = Material() with
        {
            Estudiantes =
            [
                .. Material().Estudiantes,
                new RosterEstudiante("EST-007", "valentina ospina", "ADM"),
            ],
        };

        var l2a = GuardiaReferenciasExplicitas.Evaluar("¿Valentina perdió Estadística?", Est004, roster);
        var l2b = GuardiaSolicitudesTerceros.Evaluar("¿Valentina perdió Estadística?", Est004, roster);

        // La contencion queda en L3/L4: L2a/L2b no marcan.
        Assert.False(l2a.Marcada);
        Assert.False(l2b.Marcada);

        // El nombre completo y el apellido unico si resuelven.
        Assert.Equal(["EST-005"], GuardiaReferenciasExplicitas.Evaluar("notas de Valentina Castro", Est004, roster).Objetivos);
        Assert.Equal(["EST-007"], GuardiaReferenciasExplicitas.Evaluar("notas de Ospina", Est004, roster).Objetivos);
    }

    [Fact]
    [Trait("Criterio", "AUTH-21.a")]
    public void El_id_de_otro_coordinador_se_detecta_y_el_propio_no()
    {
        var roster = Material();

        var ajeno = GuardiaReferenciasExplicitas.Evaluar("¿Qué notas tienen los estudiantes de COORD-02?", Coord01, roster);
        var propio = GuardiaReferenciasExplicitas.Evaluar("¿Qué dice COORD-01 de las notas?", Coord01, roster);

        Assert.True(ajeno.Marcada);
        Assert.Equal("referencia", ajeno.Regla);
        Assert.Equal(["COORD-02"], ajeno.Objetivos);
        Assert.False(propio.Marcada);
    }

    [Fact]
    [Trait("Criterio", "AUTH-21.b")]
    public void El_nombre_de_un_coordinador_no_se_marca_ni_en_L2a_ni_en_L2b()
    {
        const string pregunta = "¿Marcela Ríos es la coordinadora de Sistemas?";
        var roster = Material();

        Assert.False(GuardiaReferenciasExplicitas.Evaluar(pregunta, Est001, roster).Marcada);
        Assert.False(GuardiaSolicitudesTerceros.Evaluar(pregunta, Est001, roster).Marcada);
    }

    [Fact]
    [Trait("Criterio", "AUTH-17.a")]
    public void El_coordinador_no_alcanza_a_un_estudiante_de_otro_programa_por_nombre_ni_id()
    {
        var roster = Material();

        var porNombre = GuardiaReferenciasExplicitas.Evaluar("¿Cuál es el promedio de Valentina Castro?", Coord01, roster);
        var porId = GuardiaReferenciasExplicitas.Evaluar("Notas de EST-005", Coord01, roster);

        Assert.True(porNombre.Marcada);
        Assert.Equal(["EST-005"], porNombre.Objetivos);
        Assert.True(porId.Marcada);
        Assert.Equal(["EST-005"], porId.Objetivos);
    }

    [Fact]
    [Trait("Criterio", "AUTH-17.a")]
    public void El_coordinador_si_puede_referirse_a_estudiantes_de_sus_programas()
    {
        var roster = Material();

        Assert.False(GuardiaReferenciasExplicitas.Evaluar("¿Qué nota tiene Andrés Martínez en Bases de Datos?", Coord01, roster).Marcada);
        Assert.False(GuardiaReferenciasExplicitas.Evaluar("Notas de EST-002", Coord01, roster).Marcada);
    }

    [Fact]
    [Trait("Criterio", "AUTH-17.a")]
    public void Un_id_inexistente_con_forma_de_id_se_trata_como_fuera_de_alcance()
    {
        var r = GuardiaReferenciasExplicitas.Evaluar("Notas de EST-999", Est001, Material());

        Assert.True(r.Marcada);
    }

    [Fact]
    [Trait("Criterio", "ADV-13.a")]
    public void L5_deniega_una_respuesta_con_el_nombre_de_otro_estudiante()
    {
        var roster = Material();
        const string salida = "...y Andrés Martínez tiene 2.28";

        var ex = Assert.Throws<AccesoDenegadoException>(() => GuardiaSalida.Verificar(salida, Est001, roster));

        Assert.Equal("salida", ex.Motivo);
        Assert.Equal(RolAcademico.Estudiante, ex.Rol);
        Assert.Equal(0, ex.Tokens);
    }

    [Fact]
    [Trait("Criterio", "ADV-13.a")]
    public void L5_deniega_ids_ajenos_conserva_los_tokens_y_acepta_lo_propio_y_lo_del_alcance()
    {
        var roster = Material();

        var ex = Assert.Throws<AccesoDenegadoException>(() => GuardiaSalida.Verificar("Consulta EST-003.", Est001, roster, 550, 0.01m));
        Assert.Equal(550, ex.Tokens);
        Assert.Equal(0.01m, ex.CostoUsd);

        GuardiaSalida.Verificar("Laura Gómez (EST-001) tiene 4.39 de promedio.", Est001, roster);
        GuardiaSalida.Verificar("Andrés Martínez perdió Bases de Datos.", Coord01, roster);
        Assert.Throws<AccesoDenegadoException>(() => GuardiaSalida.Verificar("Valentina Castro perdió Estadística.", Coord01, roster));
    }
}
