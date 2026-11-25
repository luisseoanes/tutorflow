using TutorFlow.Domain.Academico;

namespace TutorFlow.Domain.Tests;

/// <summary>
/// Datos de prueba copiados de escenarios/academic-data.md (tabla golden). Solo viven en tests/.
/// </summary>
internal static class DatosGolden
{
    // est, codigo, c1, c2, c3, definitiva esperada (null = sin definitiva), clases, faltas (null = sin registro),
    // pierde esperado (null = desconocido), definitiva efectiva esperada (null = sin definitiva).
    public static readonly (string Est, string Cod, decimal? C1, decimal? C2, decimal? C3, decimal? Def, int Clases, int? Faltas, bool? Pierde, decimal? Efectiva)[] Filas =
    [
        ("EST-001", "CAL-201", 4.2m, 4.5m, 4.6m, 4.5m, 64, 1, false, 4.5m),
        ("EST-001", "POO-202", 4.8m, 4.6m, 4.9m, 4.8m, 48, 0, false, 4.8m),
        ("EST-001", "BD-203", 4.0m, 3.8m, 4.4m, 4.1m, 48, 2, false, 4.1m),
        ("EST-001", "ING-204", 3.9m, 4.1m, 4.0m, 4.0m, 32, 1, false, 4.0m),
        ("EST-002", "CAL-201", 2.5m, 2.8m, 2.6m, 2.6m, 64, 3, false, 2.6m),
        ("EST-002", "POO-202", 3.5m, 3.2m, 3.6m, 3.5m, 48, 2, false, 3.5m),
        ("EST-002", "BD-203", 3.8m, 3.6m, 3.9m, 3.8m, 48, 9, true, 0.0m),
        ("EST-002", "ING-204", 3.0m, 3.4m, 3.1m, 3.2m, 32, 1, false, 3.2m),
        ("EST-003", "CAL-201", 4.0m, 3.9m, 4.2m, 4.1m, 64, 2, false, 4.1m),
        ("EST-003", "POO-202", 3.7m, 4.0m, 3.8m, 3.8m, 48, 1, false, 3.8m),
        ("EST-003", "BD-203", 4.1m, 4.3m, 4.0m, 4.1m, 48, 0, false, 4.1m),
        ("EST-003", "ING-204", 4.5m, 4.4m, 4.7m, 4.6m, 32, null, null, 4.6m),
        ("EST-004", "CON-101", 3.6m, 3.9m, 4.1m, 3.9m, 48, 0, false, 3.9m),
        ("EST-004", "MIC-102", 3.4m, 3.0m, 3.5m, 3.3m, 48, 2, false, 3.3m),
        ("EST-004", "EST-103", 4.0m, 4.2m, 3.9m, 4.0m, 64, 3, false, 4.0m),
        ("EST-004", "COM-104", 4.5m, 4.6m, 4.4m, 4.5m, 32, 1, false, 4.5m),
        ("EST-005", "CON-101", 3.2m, 3.0m, 3.3m, 3.2m, 48, 4, false, 3.2m),
        ("EST-005", "MIC-102", 1.9m, 1.6m, 1.9m, 1.8m, 48, 5, false, 1.8m),
        ("EST-005", "EST-103", 2.4m, 2.2m, 2.3m, 2.3m, 64, 6, false, 2.3m),
        ("EST-005", "COM-104", 3.8m, 3.5m, 3.9m, 3.8m, 32, 2, false, 3.8m),
        ("EST-006", "CON-101", 4.1m, 4.0m, 4.3m, 4.2m, 48, 1, false, 4.2m),
        ("EST-006", "MIC-102", 3.6m, 3.8m, 3.9m, 3.8m, 48, 0, false, 3.8m),
        ("EST-006", "EST-103", 3.9m, 4.1m, null, null, 64, 2, false, null),
        ("EST-006", "COM-104", 4.2m, 4.0m, 4.4m, 4.2m, 32, 0, false, 4.2m),
    ];

    /// <summary>Creditos por codigo de asignatura (campo "creditos" de q10/material/mock-api/datos.json; 12 por estudiante).</summary>
    private static readonly Dictionary<string, int> Creditos = new()
    {
        ["CAL-201"] = 4, ["POO-202"] = 3, ["BD-203"] = 3, ["ING-204"] = 2,
        ["CON-101"] = 3, ["MIC-102"] = 3, ["EST-103"] = 4, ["COM-104"] = 2,
    };

    public static AsignaturaPeriodo Asignatura(string est, string cod) =>
        Construir(Filas.Single(f => f.Est == est && f.Cod == cod));

    public static List<AsignaturaPeriodo> Periodo(string est) =>
        Filas.Where(f => f.Est == est).Select(Construir).ToList();

    private static AsignaturaPeriodo Construir(
        (string Est, string Cod, decimal? C1, decimal? C2, decimal? C3, decimal? Def, int Clases, int? Faltas, bool? Pierde, decimal? Efectiva) f) =>
        new(f.Cod, Creditos[f.Cod], f.C1, f.C2, f.C3, f.Clases, f.Faltas);
}
