using TutorFlow.Application.Errores;
using TutorFlow.Application.Identity;
using TutorFlow.Application.Ports;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Tests.Identity;

/// <summary>Espia de IAcademicDataSource: registra las lecturas de identidad. Datos sinteticos de prueba.</summary>
internal sealed class FuenteIdentidadEspia : IAcademicDataSource
{
    public List<string> Llamadas { get; } = [];

    public Func<Exception?>? Fallo { get; set; }

    public Task<EstudianteFuente?> ObtenerEstudianteAsync(string estudianteId, CancellationToken ct)
    {
        Llamadas.Add("estudiante " + estudianteId);
        var ex = Fallo?.Invoke();
        if (ex is not null)
        {
            throw ex;
        }

        return Task.FromResult<EstudianteFuente?>(
            estudianteId is "EST-001" or "EST-002" ? new EstudianteFuente(estudianteId, "Nombre", "ING-SIS", 5) : null);
    }

    public Task<CoordinadorFuente?> ObtenerCoordinadorAsync(string coordinadorId, CancellationToken ct)
    {
        Llamadas.Add("coordinador " + coordinadorId);
        var ex = Fallo?.Invoke();
        if (ex is not null)
        {
            throw ex;
        }

        return Task.FromResult<CoordinadorFuente?>(
            coordinadorId == "COORD-01" ? new CoordinadorFuente("COORD-01", "Nombre", ["ING-SIS", "ADM"]) : null);
    }

    public Task<IReadOnlyList<NotaFuente>> ObtenerNotasAsync(string estudianteId, CancellationToken ct) => throw new NotSupportedException();

    public Task<IReadOnlyList<AsistenciaFuente>> ObtenerAsistenciaAsync(string estudianteId, CancellationToken ct) => throw new NotSupportedException();

    public Task<IReadOnlyList<ProgramaFuente>> ListarProgramasAsync(CancellationToken ct) => throw new NotSupportedException();

    public Task<IReadOnlyList<EstudianteFuente>> ListarEstudiantesProgramaAsync(string programaId, CancellationToken ct) => throw new NotSupportedException();
}

public class ValidadorIdentidadTests
{
    [Fact]
    [Trait("Criterio", "AUTH-15.a")]
    public async Task Principal_estudiante_tiene_solo_su_id_y_los_campos_extra_no_intervienen()
    {
        // El lector del cuerpo entrega solo rol e id (los campos extra del escenario se ignoran antes de L1).
        var fuente = new FuenteIdentidadEspia();
        var validador = new ValidadorIdentidad(fuente);

        var principal = await validador.ValidarAsync("estudiante", "EST-001", CancellationToken.None);

        Assert.Equal(RolAcademico.Estudiante, principal.Rol);
        Assert.Equal("EST-001", principal.Id);
        Assert.Empty(principal.ProgramasCoordinados);
        Assert.Equal(["estudiante EST-001"], fuente.Llamadas);
        // Su alcance es solo EST-001 (politica pura de dominio).
        var politica = new AuthorizationPolicy();
        Assert.True(politica.Authorize(principal, new RecursoAcademico(TipoRecurso.DatosEstudiante, "EST-001", "ING-SIS")).Permitido);
        Assert.False(politica.Authorize(principal, new RecursoAcademico(TipoRecurso.DatosEstudiante, "EST-005", "ADM")).Permitido);
    }

    [Fact]
    [Trait("Criterio", "AUTH-15.a")]
    public async Task Principal_coordinador_toma_sus_programas_de_la_fuente()
    {
        var validador = new ValidadorIdentidad(new FuenteIdentidadEspia());

        var principal = await validador.ValidarAsync("coordinador", "COORD-01", CancellationToken.None);

        Assert.Equal(RolAcademico.Coordinador, principal.Rol);
        Assert.Equal("COORD-01", principal.Id);
        Assert.Equal(["ING-SIS", "ADM"], principal.ProgramasCoordinados);
    }

    [Theory]
    [InlineData(null, "EST-001")]
    [InlineData("estudiante", null)]
    [InlineData("Estudiante", "EST-001")]
    [InlineData("ESTUDIANTE", "EST-001")]
    [InlineData("admin", "EST-001")]
    [InlineData("estudiante", "")]
    [InlineData("estudiante", "est-001")]
    [InlineData("estudiante", " EST-001")]
    [InlineData("estudiante", "EST-001\n")]
    [InlineData("estudiante", "EST-001/../EST-002")]
    [InlineData("estudiante", "EST_001")]
    [InlineData("estudiante", "EST-0000000000000000000000000000001")]
    [InlineData("estudiante", "EST‑001")]
    [InlineData("estudiante", "EST-999")]
    [InlineData("estudiante", "COORD-01")]
    [InlineData("coordinador", "EST-001")]
    [InlineData("coordinador", "COORD-99")]
    [Trait("Criterio", "AUTH-10.a")]
    [Trait("Criterio", "AUTH-12.a")]
    public async Task Identidad_inconsistente_lanza_IdentidadInvalida(string? rol, string? id)
    {
        var validador = new ValidadorIdentidad(new FuenteIdentidadEspia());

        await Assert.ThrowsAsync<IdentidadInvalidaException>(() => validador.ValidarAsync(rol, id, CancellationToken.None));
    }

    [Fact]
    [Trait("Criterio", "AUTH-10.a")]
    public async Task Rol_con_otra_capitalizacion_no_se_normaliza_y_no_llega_a_la_fuente()
    {
        var fuente = new FuenteIdentidadEspia();
        var validador = new ValidadorIdentidad(fuente);

        foreach (var rol in new[] { "Coordinador", "COORDINADOR", "Estudiante" })
        {
            await Assert.ThrowsAsync<IdentidadInvalidaException>(() => validador.ValidarAsync(rol, "COORD-01", CancellationToken.None));
        }

        Assert.Empty(fuente.Llamadas);
    }

    [Theory]
    [InlineData("EST-001/../EST-002")]
    [InlineData("EST-002%2F")]
    [InlineData("EST-001?x=1")]
    [InlineData("../coordinadores/COORD-01")]
    [Trait("Criterio", "AUTH-13.b")]
    public async Task Formato_invalido_no_provoca_ninguna_llamada_a_la_fuente(string id)
    {
        var fuente = new FuenteIdentidadEspia();
        var validador = new ValidadorIdentidad(fuente);

        await Assert.ThrowsAsync<IdentidadInvalidaException>(() => validador.ValidarAsync("estudiante", id, CancellationToken.None));
        await Assert.ThrowsAsync<IdentidadInvalidaException>(() => validador.ValidarAsync("coordinador", id, CancellationToken.None));

        Assert.Empty(fuente.Llamadas);
    }

    [Fact]
    [Trait("Criterio", "AUTH-23.a")]
    public async Task Fallo_de_la_fuente_se_propaga_como_dependencia_no_disponible_y_no_como_identidad_invalida()
    {
        var fuente = new FuenteIdentidadEspia { Fallo = () => new DependenciaNoDisponibleException("500") };
        var validador = new ValidadorIdentidad(fuente);

        await Assert.ThrowsAsync<DependenciaNoDisponibleException>(() => validador.ValidarAsync("coordinador", "COORD-01", CancellationToken.None));
        await Assert.ThrowsAsync<DependenciaNoDisponibleException>(() => validador.ValidarAsync("estudiante", "EST-001", CancellationToken.None));
    }

    [Theory]
    [InlineData("A", true)]
    [InlineData("EST-001", true)]
    [InlineData("a-B-9", true)]
    [InlineData("12345678901234567890123456789012", true)]
    [InlineData("123456789012345678901234567890123", false)]
    [InlineData("", false)]
    [InlineData("EST 001", false)]
    [InlineData("EST-001\n", false)]
    [InlineData("ЕST-001", false)]
    [Trait("Criterio", "AUTH-14.a")]
    public void Formato_de_id_es_ASCII_alfanumerico_con_guion_de_1_a_32(string id, bool esperado)
    {
        Assert.Equal(esperado, ValidadorIdentidad.FormatoIdValido(id));
    }
}
