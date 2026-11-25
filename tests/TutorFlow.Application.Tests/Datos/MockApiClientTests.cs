using System.Globalization;
using System.Net;
using TutorFlow.Application.Errores;
using TutorFlow.Domain.Academico;
using TutorFlow.Infrastructure.MockApi;

namespace TutorFlow.Application.Tests.Datos;

public class MockApiClientTests
{
    private static readonly MockApiOptions Rapido = new() { Timeout = TimeSpan.FromMilliseconds(50), BackoffBase = TimeSpan.FromMilliseconds(1) };

    private static (MockApiAcademicDataSource Fuente, List<TimeSpan> Esperas) Crear(ManejadorFalso m, MockApiOptions? opt = null)
    {
        var esperas = new List<TimeSpan>();
        var http = new HttpClient(m) { BaseAddress = new Uri("http://mock/"), Timeout = Timeout.InfiniteTimeSpan };
        var fuente = new MockApiAcademicDataSource(http, opt ?? Rapido, (t, _) =>
        {
            esperas.Add(t);
            return Task.CompletedTask;
        });
        return (fuente, esperas);
    }

    private const string EstJson = """{"id":"X-1","nombre":"Ana","programaId":"P","semestre":3}""";

    [Fact]
    public async Task Timeout_hace_3_intentos_y_lanza_DependenciaNoDisponible()
    {
        var m = new ManejadorFalso(async (_, _, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage();
        });
        var (f, esperas) = Crear(m);
        await Assert.ThrowsAsync<DependenciaNoDisponibleException>(() => f.ObtenerEstudianteAsync("X-1", CancellationToken.None));
        Assert.Equal(3, m.Llamadas);
        Assert.Equal(2, esperas.Count);
        Assert.True(esperas[1] > esperas[0]); // backoff creciente
    }

    [Fact]
    public async Task Error_5xx_reintenta_y_se_recupera()
    {
        var m = new ManejadorFalso((_, n, _) => n < 3 ? ManejadorFalso.Estado(HttpStatusCode.InternalServerError) : ManejadorFalso.Json(EstJson));
        var (f, _) = Crear(m);
        var e = await f.ObtenerEstudianteAsync("X-1", CancellationToken.None);
        Assert.Equal("X-1", e!.Id);
        Assert.Equal(3, m.Llamadas);
    }

    [Fact]
    public async Task Error_de_red_reintenta_y_agota()
    {
        var m = new ManejadorFalso((_, _, _) => throw new HttpRequestException("red"));
        var (f, _) = Crear(m);
        await Assert.ThrowsAsync<DependenciaNoDisponibleException>(() => f.ListarProgramasAsync(CancellationToken.None));
        Assert.Equal(3, m.Llamadas);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task No_reintenta_4xx(HttpStatusCode status)
    {
        var m = new ManejadorFalso((_, _, _) => ManejadorFalso.Estado(status));
        var (f, _) = Crear(m);
        await Assert.ThrowsAsync<DependenciaNoDisponibleException>(() => f.ObtenerEstudianteAsync("X-1", CancellationToken.None));
        Assert.Equal(1, m.Llamadas);
    }

    [Fact]
    public async Task Respuesta_404_devuelve_null_sin_reintentar()
    {
        var m = new ManejadorFalso((_, _, _) => ManejadorFalso.Json("""{"error":"No encontrado"}""", HttpStatusCode.NotFound));
        var (f, _) = Crear(m);
        Assert.Null(await f.ObtenerEstudianteAsync("X-9", CancellationToken.None));
        Assert.Null(await f.ObtenerCoordinadorAsync("C-9", CancellationToken.None));
        Assert.Equal(2, m.Llamadas);
    }

    [Fact]
    public async Task Solo_usa_GET()
    {
        var metodos = new List<HttpMethod>();
        var m = new ManejadorFalso((r, _, _) =>
        {
            metodos.Add(r.Method);
            return ManejadorFalso.Json("[]");
        });
        var (f, _) = Crear(m);
        await f.ListarProgramasAsync(CancellationToken.None);
        await f.ObtenerNotasAsync("A", CancellationToken.None);
        Assert.All(metodos, x => Assert.Equal(HttpMethod.Get, x));
    }

    [Theory]
    [InlineData("A/B", "estudiantes/A%2FB")]
    [InlineData("A?x=1", "estudiantes/A%3Fx%3D1")]
    [InlineData("../coordinadores/C", "estudiantes/..%2Fcoordinadores%2FC")]
    public async Task Ids_se_codifican_y_nunca_cambian_la_ruta(string id, string esperado)
    {
        var m = new ManejadorFalso((_, _, _) => ManejadorFalso.Json("{}", HttpStatusCode.NotFound));
        var (f, _) = Crear(m);
        await f.ObtenerEstudianteAsync(id, CancellationToken.None);
        var ruta = m.Rutas.Single();
        Assert.StartsWith("http://mock/" + esperado, ruta, StringComparison.Ordinal);
        Assert.DoesNotContain("?", ruta, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("")]
    public async Task Ids_de_segmento_especial_se_tratan_como_inexistentes_sin_llamar_a_la_red(string id)
    {
        var m = new ManejadorFalso((_, _, _) => ManejadorFalso.Json("[]"));
        var (f, _) = Crear(m);
        Assert.Null(await f.ObtenerEstudianteAsync(id, CancellationToken.None));
        Assert.Null(await f.ObtenerCoordinadorAsync(id, CancellationToken.None));
        Assert.Empty(await f.ObtenerNotasAsync(id, CancellationToken.None));
        Assert.Empty(await f.ListarEstudiantesProgramaAsync(id, CancellationToken.None));
        Assert.Equal(0, m.Llamadas);
    }

    [Fact]
    public async Task Id_con_barra_en_notas_no_cambia_el_recurso()
    {
        var m = new ManejadorFalso((_, _, _) => ManejadorFalso.Json("[]"));
        var (f, _) = Crear(m);
        await f.ObtenerNotasAsync("A/../B", CancellationToken.None);
        Assert.Equal("http://mock/estudiantes/A%2F..%2FB/notas", m.Rutas.Single());
    }

    [Theory]
    [InlineData("""[{"estudianteId":"E","codigo":"C","asignatura":"A","creditos":3,"corte1":"4.0","corte2":null,"corte3":null}]""")]
    [InlineData("""[{"estudianteId":"E","codigo":"C","asignatura":"A","creditos":"3","corte1":4.0}]""")]
    [InlineData("""[{"estudianteId":"E","codigo":"C","asignatura":"A","creditos":3.5,"corte1":4.0}]""")]
    [InlineData("""[{"estudianteId":"E","codigo":"C","asignatura":"A","corte1":4.0}]""")]
    [InlineData("""[{"estudianteId":7,"codigo":"C","asignatura":"A","creditos":3}]""")]
    [InlineData("""{"no":"arreglo"}""")]
    [InlineData("""no es json""")]
    public async Task Notas_con_tipos_inesperados_lanzan_DatoAcademicoInvalido(string json)
    {
        var m = new ManejadorFalso((_, _, _) => ManejadorFalso.Json(json));
        var (f, _) = Crear(m);
        await Assert.ThrowsAsync<DatoAcademicoInvalidoException>(() => f.ObtenerNotasAsync("E", CancellationToken.None));
        Assert.Equal(1, m.Llamadas);
    }

    [Fact]
    public async Task Asistencia_con_clases_cero_negativas_o_nulas_no_se_corrige()
    {
        var m = new ManejadorFalso((_, _, _) => ManejadorFalso.Json(
            """[{"estudianteId":"E","codigo":"A","asignatura":"x","clasesProgramadas":0,"inasistencias":1},{"estudianteId":"E","codigo":"B","asignatura":"y","clasesProgramadas":-4,"inasistencias":0},{"estudianteId":"E","codigo":"C","asignatura":"z","clasesProgramadas":null,"inasistencias":0}]"""));
        var (f, _) = Crear(m);
        var r = await f.ObtenerAsistenciaAsync("E", CancellationToken.None);
        Assert.Equal([0, -4, null], r.Select(a => a.ClasesProgramadas).ToArray());
    }

    [Fact]
    public async Task Asistencia_con_clases_texto_lanza()
    {
        var m = new ManejadorFalso((_, _, _) => ManejadorFalso.Json("""[{"estudianteId":"E","codigo":"A","asignatura":"x","clasesProgramadas":"64","inasistencias":1}]"""));
        var (f, _) = Crear(m);
        await Assert.ThrowsAsync<DatoAcademicoInvalidoException>(() => f.ObtenerAsistenciaAsync("E", CancellationToken.None));
    }

    [Fact]
    public async Task Decimales_se_leen_con_InvariantCulture_aunque_la_cultura_use_coma()
    {
        var previa = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("es-CO");
        try
        {
            var m = new ManejadorFalso((_, _, _) => ManejadorFalso.Json(
                """[{"estudianteId":"E","codigo":"C","asignatura":"A","creditos":3,"corte1":4.25,"corte2":3.5,"corte3":null}]"""));
            var (f, _) = Crear(m);
            var n = (await f.ObtenerNotasAsync("E", CancellationToken.None)).Single();
            Assert.Equal(4.25m, n.Corte1);
            Assert.Equal(3.5m, n.Corte2);
            Assert.Null(n.Corte3);
        }
        finally
        {
            CultureInfo.CurrentCulture = previa;
        }
    }

    [Fact]
    public async Task Cancelacion_del_llamador_no_se_convierte_en_reintento()
    {
        using var cts = new CancellationTokenSource();
        var m = new ManejadorFalso(async (_, _, ct) =>
        {
            await cts.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage();
        });
        var (f, _) = Crear(m, new MockApiOptions { Timeout = TimeSpan.FromSeconds(30) });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.ObtenerEstudianteAsync("X", cts.Token));
        Assert.Equal(1, m.Llamadas);
    }

    [Fact]
    public async Task Coordinador_y_programas_se_leen()
    {
        var m = new ManejadorFalso((r, _, _) => r.RequestUri!.AbsolutePath.StartsWith("/coordinadores", StringComparison.Ordinal)
            ? ManejadorFalso.Json("""{"id":"C","nombre":"N","programas":["P1","P2"]}""")
            : ManejadorFalso.Json("""[{"id":"P1","nombre":"Uno","coordinadorId":"C"},{"id":"P2","nombre":"Dos"}]"""));
        var (f, _) = Crear(m);
        Assert.Equal(["P1", "P2"], (await f.ObtenerCoordinadorAsync("C", CancellationToken.None))!.Programas);
        var p = await f.ListarProgramasAsync(CancellationToken.None);
        Assert.Equal("C", p[0].CoordinadorId);
        Assert.Null(p[1].CoordinadorId);
    }
}
