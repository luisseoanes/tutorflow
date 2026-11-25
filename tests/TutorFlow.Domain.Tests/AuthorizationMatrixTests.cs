using TutorFlow.Domain.Authorization;

namespace TutorFlow.Domain.Tests;

public class AuthorizationMatrixTests
{
    // Programa de cada estudiante (datos de prueba; en produccion sale del roster).
    private static readonly Dictionary<string, string> ProgramaDe = new()
    {
        ["EST-001"] = "ING-SIS", ["EST-002"] = "ING-SIS", ["EST-003"] = "ING-SIS",
        ["EST-004"] = "ADM", ["EST-005"] = "ADM", ["EST-006"] = "ADM",
    };

    private static readonly string[] Recursos =
    [
        "EST-001", "EST-002", "EST-003", "EST-004", "EST-005", "EST-006",
        "PROG:ING-SIS", "PROG:ADM", "COORD-01", "COORD-02",
    ];

    private static readonly string[] Principales =
    [
        "EST-001", "EST-002", "EST-003", "EST-004", "EST-005", "EST-006", "COORD-01", "COORD-02",
    ];

    // Tabla explicita SPEC 6.1 (columnas en el orden de Recursos). 1 = permite. 8 x 10 = 80 celdas.
    // Los datos de coordinadores no los ve nadie (ni el propio coordinador).
    private static readonly Dictionary<string, int[]> Tabla = new()
    {
        //            E1 E2 E3 E4 E5 E6 ING ADM C1 C2
        ["EST-001"] = [1, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        ["EST-002"] = [0, 1, 0, 0, 0, 0, 0, 0, 0, 0],
        ["EST-003"] = [0, 0, 1, 0, 0, 0, 0, 0, 0, 0],
        ["EST-004"] = [0, 0, 0, 1, 0, 0, 0, 0, 0, 0],
        ["EST-005"] = [0, 0, 0, 0, 1, 0, 0, 0, 0, 0],
        ["EST-006"] = [0, 0, 0, 0, 0, 1, 0, 0, 0, 0],
        ["COORD-01"] = [1, 1, 1, 0, 0, 0, 1, 0, 0, 0],
        ["COORD-02"] = [0, 0, 0, 1, 1, 1, 0, 1, 0, 0],
    };

    private static PrincipalAcademico Principal(string id) => id switch
    {
        "COORD-01" => new PrincipalAcademico(RolAcademico.Coordinador, id, ["ING-SIS"]),
        "COORD-02" => new PrincipalAcademico(RolAcademico.Coordinador, id, ["ADM"]),
        _ => new PrincipalAcademico(RolAcademico.Estudiante, id),
    };

    private static RecursoAcademico Recurso(string r)
    {
        if (r.StartsWith("PROG:", StringComparison.Ordinal))
        {
            return new RecursoAcademico(TipoRecurso.ProgramaEstudiantes, ProgramaId: r["PROG:".Length..]);
        }

        if (r.StartsWith("COORD-", StringComparison.Ordinal))
        {
            return new RecursoAcademico(TipoRecurso.DatosCoordinador, r);
        }

        return new RecursoAcademico(TipoRecurso.DatosEstudiante, r, ProgramaDe[r]);
    }

    public static IEnumerable<object[]> Celdas()
    {
        foreach (var p in Principales)
        {
            for (var i = 0; i < Recursos.Length; i++)
            {
                yield return new object[] { p, Recursos[i], Tabla[p][i] == 1 };
            }
        }
    }

    [Fact]
    [Trait("Criterio", "AUTH-22.a")]
    public void La_tabla_tiene_80_celdas_y_14_permitidas()
    {
        Assert.Equal(80, Celdas().Count());
        Assert.Equal(14, Celdas().Count(c => (bool)c[2]));
    }

    [Theory]
    [MemberData(nameof(Celdas))]
    [Trait("Criterio", "AUTH-22.a")]
    public void Matriz_SPEC_6_1(string principal, string recurso, bool permitido)
    {
        var decision = new AuthorizationPolicy().Authorize(Principal(principal), Recurso(recurso));

        Assert.Equal(permitido, decision.Permitido);
    }

    [Theory]
    [InlineData("EST-001")]
    [InlineData("COORD-01")]
    [InlineData("COORD-02")]
    [Trait("Criterio", "AUTH-22.a")]
    public void Reglamento_lo_ven_ambos_roles(string principal) =>
        Assert.True(new AuthorizationPolicy().Authorize(Principal(principal), new RecursoAcademico(TipoRecurso.Reglamento)).Permitido);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("DESCONOCIDO")]
    [Trait("Criterio", "AUTH-22.a")]
    public void Programa_nulo_vacio_o_desconocido_se_deniega_para_el_coordinador(string? programa)
    {
        var politica = new AuthorizationPolicy();
        var coord = Principal("COORD-01");

        Assert.False(politica.Authorize(coord, new RecursoAcademico(TipoRecurso.DatosEstudiante, "EST-001", programa)).Permitido);
        Assert.False(politica.Authorize(coord, new RecursoAcademico(TipoRecurso.ProgramaEstudiantes, ProgramaId: programa)).Permitido);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("est-001")]
    [Trait("Criterio", "AUTH-22.a")]
    public void Estudiante_con_id_nulo_vacio_o_distinto_se_deniega(string? id) =>
        Assert.False(new AuthorizationPolicy().Authorize(Principal("EST-001"), new RecursoAcademico(TipoRecurso.DatosEstudiante, id, "ING-SIS")).Permitido);
}
