using System.Reflection;
using TutorFlow.Application.Errores;
using TutorFlow.Application.Ports;
using TutorFlow.Infrastructure.Roster;

namespace TutorFlow.Application.Tests.Datos;

public class RosterTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(300);

    private sealed class FuenteFalsa : IAcademicDataSource
    {
        public bool Falla { get; set; }

        public Task<EstudianteFuente?> ObtenerEstudianteAsync(string estudianteId, CancellationToken ct) => throw new InvalidOperationException("roster no lee estudiantes sueltos");

        public Task<IReadOnlyList<NotaFuente>> ObtenerNotasAsync(string estudianteId, CancellationToken ct) => throw new InvalidOperationException("roster no lee notas");

        public Task<IReadOnlyList<AsistenciaFuente>> ObtenerAsistenciaAsync(string estudianteId, CancellationToken ct) => throw new InvalidOperationException("roster no lee asistencia");

        public Task<CoordinadorFuente?> ObtenerCoordinadorAsync(string coordinadorId, CancellationToken ct) => throw new InvalidOperationException("roster no lee coordinadores");

        public Task<IReadOnlyList<ProgramaFuente>> ListarProgramasAsync(CancellationToken ct)
        {
            if (Falla)
            {
                throw new DependenciaNoDisponibleException("caida");
            }

            return Task.FromResult<IReadOnlyList<ProgramaFuente>>(
            [
                new ProgramaFuente("PX", "Ingeniería de Sistemas", "CX"),
                new ProgramaFuente("PY", "Administración", "CX"),
                new ProgramaFuente("PZ", "Sin Coord", null),
            ]);
        }

        public Task<IReadOnlyList<EstudianteFuente>> ListarEstudiantesProgramaAsync(string programaId, CancellationToken ct)
        {
            IReadOnlyList<EstudianteFuente> r = programaId == "PX"
                ? [new EstudianteFuente("E1", "  Andrés   ÑÁNDÚ Gómez ", "PX", 5), new EstudianteFuente("E2", "Laura Pérez", "PX", 1)]
                : programaId == "PY" ? [new EstudianteFuente("E3", "Camilo Rojas", "PY", null)] : [];
            return Task.FromResult(r);
        }
    }

    private static (RosterProvider P, RelojFalso Reloj, AuditoriaFalsa Audit, FuenteFalsa Fuente) Crear()
    {
        var reloj = new RelojFalso();
        var audit = new AuditoriaFalsa();
        var fuente = new FuenteFalsa();
        return (new RosterProvider(fuente, audit, reloj, Ttl), reloj, audit, fuente);
    }

    [Fact]
    [Trait("Criterio", "AUTH-24.a")]
    public void El_modelo_del_roster_expone_solo_los_campos_minimos()
    {
        Assert.Equal(["Id", "NombreNormalizado", "ProgramaId"], Propiedades(typeof(RosterEstudiante)));
        Assert.Equal(["Id"], Propiedades(typeof(RosterCoordinador)));
        Assert.Equal(["Id", "NombreNormalizado"], Propiedades(typeof(RosterPrograma)));
        Assert.Equal(["CargadoEn", "Coordinadores", "Estudiantes", "Programas"], Propiedades(typeof(RosterSnapshot)));
        Assert.Equal(typeof(IReadOnlyList<RosterEstudiante>), typeof(RosterSnapshot).GetProperty("Estudiantes")!.PropertyType);
        Assert.Equal(typeof(IReadOnlyList<RosterCoordinador>), typeof(RosterSnapshot).GetProperty("Coordinadores")!.PropertyType);
        Assert.Equal(typeof(IReadOnlyList<RosterPrograma>), typeof(RosterSnapshot).GetProperty("Programas")!.PropertyType);
    }

    [Fact]
    [Trait("Criterio", "AUTH-24.a")]
    public async Task El_snapshot_cargado_solo_contiene_esos_campos_normalizados()
    {
        var (p, _, _, _) = Crear();
        Assert.True(await p.CargarAsync(CancellationToken.None));
        var s = await p.ObtenerAsync(CancellationToken.None);
        Assert.Equal(new RosterEstudiante("E1", "andres nandu gomez", "PX"), s.Estudiantes[0]);
        Assert.Equal(3, s.Estudiantes.Count);
        Assert.Equal(["CX"], s.Coordinadores.Select(c => c.Id).ToArray());
        Assert.Equal("ingenieria de sistemas", s.Programas.Single(x => x.Id == "PX").NombreNormalizado);
        Assert.Equal(3, s.Programas.Count);
    }

    [Fact]
    [Trait("Criterio", "AUTH-23.b")]
    public async Task Roster_nunca_cargado_lanza_DependenciaNoDisponible()
    {
        var (p, _, _, _) = Crear();
        await Assert.ThrowsAsync<DependenciaNoDisponibleException>(() => p.ObtenerAsync(CancellationToken.None));
    }

    [Fact]
    [Trait("Criterio", "AUTH-23.b")]
    public async Task Carga_fallida_sin_snapshot_previo_sigue_no_disponible()
    {
        var (p, _, _, fuente) = Crear();
        fuente.Falla = true;
        Assert.False(await p.CargarAsync(CancellationToken.None));
        await Assert.ThrowsAsync<DependenciaNoDisponibleException>(() => p.ObtenerAsync(CancellationToken.None));
    }

    [Fact]
    [Trait("Criterio", "AUTH-23.b")]
    public async Task Ultima_carga_correcta_mayor_a_2xTTL_lanza_y_recarga_restablece()
    {
        var (p, reloj, _, fuente) = Crear();
        await p.CargarAsync(CancellationToken.None);

        reloj.UtcNow += Ttl * 2; // justo en el limite: aun vigente
        await p.ObtenerAsync(CancellationToken.None);

        reloj.UtcNow += TimeSpan.FromSeconds(1); // > 2 x TTL
        await Assert.ThrowsAsync<DependenciaNoDisponibleException>(() => p.ObtenerAsync(CancellationToken.None));

        fuente.Falla = true; // fallos no extienden la vigencia
        Assert.False(await p.CargarAsync(CancellationToken.None));
        await Assert.ThrowsAsync<DependenciaNoDisponibleException>(() => p.ObtenerAsync(CancellationToken.None));

        fuente.Falla = false;
        Assert.True(await p.CargarAsync(CancellationToken.None));
        Assert.NotEmpty((await p.ObtenerAsync(CancellationToken.None)).Estudiantes);
    }

    [Fact]
    public async Task Carga_fallida_conserva_el_snapshot_vigente()
    {
        var (p, reloj, _, fuente) = Crear();
        await p.CargarAsync(CancellationToken.None);
        reloj.UtcNow += Ttl;
        fuente.Falla = true;
        Assert.False(await p.CargarAsync(CancellationToken.None));
        Assert.Equal(3, (await p.ObtenerAsync(CancellationToken.None)).Estudiantes.Count);
    }

    [Fact]
    public async Task Evento_roster_cargado_trae_solo_cantidades_y_resultado()
    {
        var (p, _, audit, fuente) = Crear();
        await p.CargarAsync(CancellationToken.None);
        fuente.Falla = true;
        await p.CargarAsync(CancellationToken.None);

        Assert.Equal(2, audit.Roster.Count);
        Assert.Equal(new EventoRosterCargado(audit.Roster[0].Ts, 3, 3, "ok"), audit.Roster[0]);
        Assert.Equal("error", audit.Roster[1].Resultado);
        Assert.Equal(["Estudiantes", "Programas", "Resultado", "Ts"], Propiedades(typeof(EventoRosterCargado)));
        var texto = string.Join('|', audit.Roster.Select(e => e.ToString()));
        foreach (var prohibido in new[] { "E1", "E2", "PX", "CX", "andres", "Andr" })
        {
            Assert.DoesNotContain(prohibido, texto, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Fallo_de_auditoria_no_rompe_la_carga()
    {
        var reloj = new RelojFalso();
        var p = new RosterProvider(new FuenteFalsa(), new AuditoriaQueFalla(), reloj, Ttl);
        Assert.True(await p.CargarAsync(CancellationToken.None));
    }

    private sealed class AuditoriaQueFalla : IAuditLog
    {
        public Task RegistrarPreguntaAsync(EventoPregunta evento, CancellationToken ct) => Task.CompletedTask;

        public Task RegistrarRosterCargadoAsync(EventoRosterCargado evento, CancellationToken ct) => throw new IOException("disco");
    }

    private static string[] Propiedades(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name).Order(StringComparer.Ordinal).ToArray();
}
