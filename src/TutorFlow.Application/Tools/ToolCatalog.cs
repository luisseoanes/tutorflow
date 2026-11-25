using System.Text.Json;
using TutorFlow.Application.Ports;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Tools;

internal enum ToolKind
{
    Notas,
    Asistencia,
    Resumen,
    Beca,
    ListarPrograma,
    PerdidasPrograma,
}

/// <summary>Especificacion interna de una herramienta: roles (L3) y argumentos permitidos (esquema estricto, L4).</summary>
internal sealed record ToolSpec(
    string Nombre,
    ToolKind Tipo,
    string Descripcion,
    bool Individual,
    bool AdmiteAsignatura,
    bool SoloCoordinador);

internal static class ToolCatalog
{
    public static readonly IReadOnlyList<ToolSpec> Todas =
    [
        new(ToolNames.ObtenerNotas, ToolKind.Notas,
            "Notas del periodo por asignatura: cortes, definitiva (o aporte acumulado si falta algun corte), estado y si es habilitable. Valores ya calculados por el sistema.",
            Individual: true, AdmiteAsignatura: false, SoloCoordinador: false),
        new(ToolNames.ObtenerAsistencia, ToolKind.Asistencia,
            "Asistencia por asignatura: clases programadas, faltas, porcentaje y si pierde por inasistencia. Filtro opcional por asignatura.",
            Individual: true, AdmiteAsignatura: true, SoloCoordinador: false),
        new(ToolNames.ObtenerResumenAcademico, ToolKind.Resumen,
            "Resumen del periodo: promedio ponderado, creditos matriculados, asignaturas perdidas, asignaturas sin definitiva y situacion academica.",
            Individual: true, AdmiteAsignatura: false, SoloCoordinador: false),
        new(ToolNames.EvaluarBecaRendimiento, ToolKind.Beca,
            "Evalua la beca por rendimiento academico (cumple, no cumple o no determinable) con el detalle por requisito. Solo informa; no otorga becas.",
            Individual: true, AdmiteAsignatura: false, SoloCoordinador: false),
        new(ToolNames.ListarEstudiantesPrograma, ToolKind.ListarPrograma,
            "Lista id y nombre de los estudiantes de un programa del coordinador. Sin programaId, usa todos sus programas.",
            Individual: false, AdmiteAsignatura: false, SoloCoordinador: true),
        new(ToolNames.AsignaturasPerdidasPrograma, ToolKind.PerdidasPrograma,
            "Estudiantes de un programa del coordinador con asignaturas perdidas, con nota y motivo. Sin programaId, usa todos sus programas.",
            Individual: false, AdmiteAsignatura: false, SoloCoordinador: true),
    ];

    public static ToolSpec? Buscar(string? nombre) =>
        nombre is null ? null : Todas.FirstOrDefault(t => string.Equals(t.Nombre, nombre, StringComparison.Ordinal));

    public static bool Expuesta(ToolSpec spec, RolAcademico rol) => !spec.SoloCoordinador || rol == RolAcademico.Coordinador;

    public static IReadOnlyList<ToolDefinition> Definiciones(RolAcademico rol) =>
        Todas.Where(t => Expuesta(t, rol)).Select(t => new ToolDefinition(t.Nombre, t.Descripcion, Esquema(t, rol))).ToList();

    private static JsonElement Esquema(ToolSpec spec, RolAcademico rol)
    {
        var props = new List<string>();
        var required = new List<string>();
        if (spec.Individual)
        {
            props.Add("\"estudianteId\":{\"type\":\"string\",\"pattern\":\"^[A-Za-z0-9-]{1,32}$\",\"description\":\"Id del estudiante. Estudiante: opcional (por defecto el propio). Coordinador: obligatorio.\"}");
            if (rol == RolAcademico.Coordinador)
            {
                required.Add("\"estudianteId\"");
            }
        }
        else
        {
            props.Add("\"programaId\":{\"type\":\"string\",\"pattern\":\"^[A-Za-z0-9-]{1,32}$\",\"description\":\"Id del programa. Opcional: por defecto todos los programas del coordinador.\"}");
        }

        if (spec.AdmiteAsignatura)
        {
            props.Add("\"asignatura\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":100,\"description\":\"Nombre o codigo de la asignatura (opcional).\"}");
        }

        var json = "{\"type\":\"object\",\"properties\":{" + string.Join(",", props) + "},\"required\":[" + string.Join(",", required) + "],\"additionalProperties\":false}";
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
