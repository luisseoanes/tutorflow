using System.Text.Json;
using TutorFlow.Application.Ports;
using TutorFlow.Application.Tools;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Tests.Tools;

/// <summary>Conjunto de datos del material (escenarios/academic-data.md): SOLO vive en tests/.</summary>
internal static class MaterialDatos
{
    private static readonly (string Cod, string Nombre, int Creditos)[] Ing =
    [
        ("CAL-201", "Cálculo Integral", 4), ("POO-202", "Programación Orientada a Objetos", 3),
        ("BD-203", "Bases de Datos", 3), ("ING-204", "Inglés III", 2),
    ];

    private static readonly (string Cod, string Nombre, int Creditos)[] Adm =
    [
        ("CON-101", "Contabilidad", 3), ("MIC-102", "Microeconomía", 3),
        ("EST-103", "Estadística", 4), ("COM-104", "Comunicación", 2),
    ];

    // est, c1, c2, c3, clases, faltas (por asignatura, en el orden del catalogo del programa)
    private static readonly Dictionary<string, (decimal?, decimal?, decimal?, int, int?)[]> Filas = new()
    {
        ["EST-001"] = [(4.2m, 4.5m, 4.6m, 64, 1), (4.8m, 4.6m, 4.9m, 48, 0), (4.0m, 3.8m, 4.4m, 48, 2), (3.9m, 4.1m, 4.0m, 32, 1)],
        ["EST-002"] = [(2.5m, 2.8m, 2.6m, 64, 3), (3.5m, 3.2m, 3.6m, 48, 2), (3.8m, 3.6m, 3.9m, 48, 9), (3.0m, 3.4m, 3.1m, 32, 1)],
        ["EST-003"] = [(4.0m, 3.9m, 4.2m, 64, 2), (3.7m, 4.0m, 3.8m, 48, 1), (4.1m, 4.3m, 4.0m, 48, 0), (4.5m, 4.4m, 4.7m, 32, null)],
        ["EST-004"] = [(3.6m, 3.9m, 4.1m, 48, 0), (3.4m, 3.0m, 3.5m, 48, 2), (4.0m, 4.2m, 3.9m, 64, 3), (4.5m, 4.6m, 4.4m, 32, 1)],
        ["EST-005"] = [(3.2m, 3.0m, 3.3m, 48, 4), (1.9m, 1.6m, 1.9m, 48, 5), (2.4m, 2.2m, 2.3m, 64, 6), (3.8m, 3.5m, 3.9m, 32, 2)],
        ["EST-006"] = [(4.1m, 4.0m, 4.3m, 48, 1), (3.6m, 3.8m, 3.9m, 48, 0), (3.9m, 4.1m, null, 64, 2), (4.2m, 4.0m, 4.4m, 32, 0)],
    };

    public static readonly EstudianteFuente[] Estudiantes =
    [
        new("EST-001", "Laura Gómez", "ING-SIS", 3), new("EST-002", "Andrés Martínez", "ING-SIS", 3),
        new("EST-003", "Sofía Herrera", "ING-SIS", 3), new("EST-004", "Camilo Rojas", "ADM", 2),
        new("EST-005", "Valentina Castro", "ADM", 2), new("EST-006", "Mateo Vargas", "ADM", 2),
    ];

    public static IReadOnlyList<NotaFuente> Notas(string est) => Catalogo(est).Select((c, i) =>
    {
        var f = Filas[est][i];
        return new NotaFuente(est, c.Cod, c.Nombre, c.Creditos, f.Item1, f.Item2, f.Item3);
    }).ToList();

    public static IReadOnlyList<AsistenciaFuente> Asistencia(string est) => Catalogo(est).Select((c, i) =>
    {
        var f = Filas[est][i];
        return new AsistenciaFuente(est, c.Cod, c.Nombre, f.Item4, f.Item5);
    }).ToList();

    private static (string Cod, string Nombre, int Creditos)[] Catalogo(string est) =>
        Estudiantes.Single(e => e.Id == est).ProgramaId == "ING-SIS" ? Ing : Adm;

    public static RosterSnapshot Roster() => new(
        Estudiantes.Select(e => new RosterEstudiante(e.Id, e.Nombre.ToLowerInvariant(), e.ProgramaId)).ToList(),
        [new RosterCoordinador("COORD-01"), new RosterCoordinador("COORD-02")],
        [new RosterPrograma("ING-SIS", "ingenieria de sistemas"), new RosterPrograma("ADM", "administracion de empresas")],
        DateTimeOffset.UnixEpoch);

    public static PrincipalAcademico Est(string id) => new(RolAcademico.Estudiante, id);

    public static PrincipalAcademico Coord1() => new(RolAcademico.Coordinador, "COORD-01", ["ING-SIS"]);

    public static PrincipalAcademico Coord2() => new(RolAcademico.Coordinador, "COORD-02", ["ADM"]);
}

/// <summary>Fuente espia: registra cada lectura academica. Se puede corromper por estudiante.</summary>
internal sealed class FuenteEspia : IAcademicDataSource
{
    public List<string> Lecturas { get; } = [];

    public Func<string, IReadOnlyList<NotaFuente>, IReadOnlyList<NotaFuente>>? CorromperNotas { get; set; }

    public Func<string, IReadOnlyList<AsistenciaFuente>, IReadOnlyList<AsistenciaFuente>>? CorromperAsistencia { get; set; }

    public Task<EstudianteFuente?> ObtenerEstudianteAsync(string estudianteId, CancellationToken ct)
    {
        Lecturas.Add("estudiante:" + estudianteId);
        return Task.FromResult(MaterialDatos.Estudiantes.FirstOrDefault(e => e.Id == estudianteId));
    }

    public Task<IReadOnlyList<NotaFuente>> ObtenerNotasAsync(string estudianteId, CancellationToken ct)
    {
        Lecturas.Add("notas:" + estudianteId);
        var n = MaterialDatos.Notas(estudianteId);
        return Task.FromResult(CorromperNotas?.Invoke(estudianteId, n) ?? n);
    }

    public Task<IReadOnlyList<AsistenciaFuente>> ObtenerAsistenciaAsync(string estudianteId, CancellationToken ct)
    {
        Lecturas.Add("asistencia:" + estudianteId);
        var a = MaterialDatos.Asistencia(estudianteId);
        return Task.FromResult(CorromperAsistencia?.Invoke(estudianteId, a) ?? a);
    }

    public Task<CoordinadorFuente?> ObtenerCoordinadorAsync(string coordinadorId, CancellationToken ct)
    {
        Lecturas.Add("coordinador:" + coordinadorId);
        return Task.FromResult<CoordinadorFuente?>(null);
    }

    public Task<IReadOnlyList<ProgramaFuente>> ListarProgramasAsync(CancellationToken ct)
    {
        Lecturas.Add("programas");
        return Task.FromResult<IReadOnlyList<ProgramaFuente>>([]);
    }

    public Task<IReadOnlyList<EstudianteFuente>> ListarEstudiantesProgramaAsync(string programaId, CancellationToken ct)
    {
        Lecturas.Add("programa:" + programaId);
        return Task.FromResult<IReadOnlyList<EstudianteFuente>>(
            MaterialDatos.Estudiantes.Where(e => e.ProgramaId == programaId).ToList());
    }
}

internal sealed class RosterEnMemoria : IRosterProvider
{
    public Task<RosterSnapshot> ObtenerAsync(CancellationToken ct) => Task.FromResult(MaterialDatos.Roster());
}

internal static class Armado
{
    public static (ToolDispatcher D, FuenteEspia F) Crear()
    {
        var f = new FuenteEspia();
        return (new ToolDispatcher(f, new RosterEnMemoria(), new AuthorizationPolicy()), f);
    }

    public static InvocacionPropuesta Inv(string nombre, string argsJson = "{}")
    {
        using var doc = JsonDocument.Parse(argsJson);
        return new InvocacionPropuesta("call-1", nombre, doc.RootElement.Clone());
    }

    public static JsonDocument Contenido(ResultadoHerramienta r) => JsonDocument.Parse(r.Contenido);

    public static JsonElement Asignatura(JsonDocument doc, string codigo) =>
        doc.RootElement.GetProperty("asignaturas").EnumerateArray().Single(a => a.GetProperty("codigo").GetString() == codigo);

    public static bool Cita(ResultadoHerramienta r, string documento, string seccion) =>
        r.Citas.Any(c => c.Documento == documento && c.Seccion == seccion);
}
