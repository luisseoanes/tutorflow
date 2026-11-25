using TutorFlow.Application.Audit;
using TutorFlow.Application.Errores;
using TutorFlow.Application.Tools;
using TutorFlow.Domain.Authorization;
using static TutorFlow.Application.Tests.Tools.Armado;

namespace TutorFlow.Application.Tests.Audit;

/// <summary>Gancho de auditoria del despachador y acumulacion del contexto por peticion (SPEC 9.2).</summary>
public class ContextoAuditoriaTests
{
    private static readonly DateTimeOffset Ts = DateTimeOffset.UnixEpoch;

    private static (ToolDispatcher D, ContextoAuditoria C) ConContexto()
    {
        var f = new Tools.FuenteEspia();
        var c = new ContextoAuditoria();
        return (new ToolDispatcher(f, new Tools.RosterEnMemoria(), new AuthorizationPolicy(), c), c);
    }

    [Fact]
    public async Task Consulta_individual_exitosa_registra_solo_el_objetivo_consultado_y_no_el_roster()
    {
        var (d, c) = ConContexto();

        await d.DespacharAsync(new PrincipalAcademico(RolAcademico.Estudiante, "EST-001"), Inv("obtener_notas"), default);

        var e = c.ConstruirUnaVez("r", Ts, 200, 1)!;
        Assert.Equal(["EST-001"], e.EstudiantesConsultados);
        Assert.Empty(e.ProgramasConsultados);
        var h = Assert.Single(e.Herramientas);
        Assert.Equal(("obtener_notas", "EST-001", "permitido"), (h.Nombre, h.Objetivo, h.Decision));
    }

    [Fact]
    public async Task Consulta_de_programas_exitosa_va_a_programasConsultados_separando_por_coma()
    {
        var (d, c) = ConContexto();
        var coord = new PrincipalAcademico(RolAcademico.Coordinador, "COORD-01", ["ING-SIS", "ADM"]);

        await d.DespacharAsync(coord, Inv("asignaturas_perdidas_programa"), default);

        var e = c.ConstruirUnaVez("r", Ts, 200, 1)!;
        Assert.Equal(["ING-SIS", "ADM"], e.ProgramasConsultados);
        Assert.Empty(e.EstudiantesConsultados);
        Assert.Equal("ING-SIS,ADM", Assert.Single(e.Herramientas).Objetivo);
    }

    [Fact]
    public async Task Denegacion_por_politica_registra_el_objetivo_intentado_sin_consulta_y_el_motivo()
    {
        var (d, c) = ConContexto();

        await Assert.ThrowsAsync<AccesoDenegadoException>(() => d.DespacharAsync(
            new PrincipalAcademico(RolAcademico.Estudiante, "EST-001"),
            Inv("obtener_notas", """{"estudianteId":"EST-002"}"""),
            default));

        var e = c.ConstruirUnaVez("r", Ts, 403, 1)!;
        Assert.Empty(e.EstudiantesConsultados);
        Assert.Equal("politica", e.MotivoDenegacion);
        var h = Assert.Single(e.Herramientas);
        Assert.Equal(("obtener_notas", "EST-002", "denegado"), (h.Nombre, h.Objetivo, h.Decision));
    }

    [Fact]
    public async Task Denegacion_por_herramienta_no_expuesta_registra_motivo_herramienta()
    {
        var (d, c) = ConContexto();

        await Assert.ThrowsAsync<AccesoDenegadoException>(() => d.DespacharAsync(
            new PrincipalAcademico(RolAcademico.Estudiante, "EST-001"), Inv("listar_estudiantes_programa"), default));

        var e = c.ConstruirUnaVez("r", Ts, 403, 1)!;
        Assert.Equal("herramienta", e.MotivoDenegacion);
        Assert.Equal("denegado", Assert.Single(e.Herramientas).Decision);
        Assert.Empty(e.ProgramasConsultados);
    }

    [Fact]
    public void El_evento_se_construye_una_sola_vez_y_trunca_la_pregunta_a_1000()
    {
        var c = new ContextoAuditoria();
        c.Iniciar(new string('x', 1500), "estudiante", "EST-001");

        var e = c.ConstruirUnaVez("r", Ts, 200, 5)!;

        Assert.Equal(1000, e.Pregunta.Length);
        Assert.Null(c.ConstruirUnaVez("r", Ts, 200, 5));
    }

    [Fact]
    public void El_primer_motivo_de_denegacion_prevalece_y_los_objetivos_no_se_repiten()
    {
        var c = new ContextoAuditoria();
        c.Denegar("referencia");
        c.Denegar("politica");
        c.ObjetivosDenegados(["EST-002", "EST-002"]);

        var e = c.ConstruirUnaVez("r", Ts, 403, 1)!;

        Assert.Equal("referencia", e.MotivoDenegacion);
        Assert.Equal(["EST-002"], e.ObjetivosDenegados);
    }
}
