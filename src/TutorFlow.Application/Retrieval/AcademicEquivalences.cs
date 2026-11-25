namespace TutorFlow.Application.Retrieval;

/// <summary>
/// Equivalencias academicas GENERALES del dominio (expansion solo de la CONSULTA, nunca de los documentos).
/// Cada regla recoge vocabulario coloquial o sinonimos del reglamento y NO esta derivada de las preguntas del dataset:
///  - materia = asignatura (el reglamento usa siempre "asignatura").
///  - pasar/pase = aprobar; reprobar/rajar = perder (el reglamento dice "aprobar", "perder").
///  - faltar/falta/faltas = inasistencia (el reglamento dice "inasistencia").
///  - calcular una nota/promedio: se calcula con los cortes y sus pesos/ponderacion (regla del dominio: la
///    definicion de como se calcula una nota vive en la seccion de cortes y pesos).
/// Las claves son formas superficiales sin tildes en minuscula; los valores, terminos que luego se normalizan/stemmean.
/// </summary>
public static class AcademicEquivalences
{
    private static readonly Dictionary<string, string[]> Map = Build();

    public static IReadOnlyList<string> Expand(string normalizedSurfaceForm) =>
        Map.TryGetValue(normalizedSurfaceForm, out var v) ? v : [];

    private static Dictionary<string, string[]> Build()
    {
        var d = new Dictionary<string, string[]>(StringComparer.Ordinal);
        void Add(string[] keys, params string[] values)
        {
            foreach (var k in keys)
            {
                d[k] = values;
            }
        }

        Add(["materia", "materias", "curso", "cursos"], "asignatura");
        Add(["paso", "pasa", "pasar", "pase", "pasan", "pases", "pasarla", "pasarlo"], "aprobar", "aprueba", "aprobada");
        Add(["reprobar", "reprueba", "repruebo", "rajar", "rajo"], "perder", "pierde", "reprobar");
        Add(["falto", "falta", "faltas", "faltar", "faltan", "faltamos", "faltado"], "inasistencia", "inasistencias", "asistencia");
        Add(["calcula", "calcular", "calculan", "calculo", "calculamos"], "peso", "corte", "ponderacion");
        return d;
    }
}
