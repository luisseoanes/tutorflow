using System.Text.RegularExpressions;
using TutorFlow.Application.Ports;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Security;

/// <summary>
/// L2b (SPEC 6.7, D-03): deteccion lexica y determinista de solicitudes de datos de terceros. Vocabularios TD/ST/SD
/// y reglas R1-R6 exactamente como en SPEC 6.7 (versionados aqui; ampliarlos exige mantener verdes AUTH-06 y AUTH-25).
/// Sin LLM ni puntajes. Se aplica sobre texto normalizado y por palabra completa.
/// </summary>
public static class GuardiaSolicitudesTerceros
{
    public const string VersionVocabularios = "6.7-v1";

    private const string Ini = "(?<![a-z0-9])";
    private const string Fin = "(?![a-z0-9])";

    // TD: dato academico personal.
    private static readonly Regex Td = Construir(
        "nota|notas|promedio|promedios|definitiva|definitivas|calificacion|calificaciones|corte|cortes|falta|faltas|" +
        "inasistencia|inasistencias|asistencia|perdio|perdieron|perdida|perdidas|aprobo|aprobaron|reprobo|reprobaron|" +
        "habilitar|beca|prueba academica|situacion academica|materias perdidas");

    // ST: sujeto de terceros (fuerte).
    private static readonly Regex St = Construir(
        "companero|companeros|companera|companeras|" +
        "otro estudiante|otros estudiantes|otra estudiante|otras estudiantes|" +
        "otra persona|alguien mas|mi amigo|mi amiga|" +
        "todos los estudiantes|todas las estudiantes|" +
        "estudiantes de mi (?:programa|carrera|curso|grupo|semestre|clase|salon)|" +
        "lista(?: completa)? de(?: los)? estudiantes|listado(?: completo)? de(?: los)? estudiantes|" +
        "nombres de los estudiantes|ranking|" +
        "quienes? (?:[a-z0-9]+ ){0,3}(?:perdio|perdieron|reprobo|reprobaron|tiene|tienen|saco|sacaron|falto|faltaron)|" +
        "el estudiante con (?:mas|menos|mejor|peor)|la estudiante con (?:mas|menos|mejor|peor)");

    // SD: sujeto generico (debil).
    private static readonly Regex Sd = Construir("estudiante|estudiantes");

    // R4: otros programas (coordinador).
    private static readonly Regex OtrosProgramas = Construir("otros programas|todos los programas|otra carrera|otras carreras");

    public static ResultadoGuardia Evaluar(string pregunta, PrincipalAcademico principal, RosterSnapshot roster)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(roster);

        var normalizado = NormalizadorTexto.Normalizar(pregunta);
        if (normalizado.Length == 0 || !Td.IsMatch(normalizado))
        {
            return ResultadoGuardia.Ninguna;
        }

        // Desde aqui hay TD.
        var td = true;
        var st = St.IsMatch(normalizado);
        var sd = Sd.IsMatch(normalizado);
        var programas = ProgramasReferenciados(normalizado, roster);

        if (principal.Rol == RolAcademico.Estudiante)
        {
            // R1: ST y TD. R2: SD y TD sin ST no marca (pregunta normativa).
            if (st && td)
            {
                return ResultadoGuardia.Marca("R1");
            }

            // R5: cualquier programa (ni siquiera el suyo) con SD o ST y TD.
            if (programas.Count > 0 && (sd || st))
            {
                return ResultadoGuardia.Marca("R5");
            }

            return ResultadoGuardia.Ninguna;
        }

        // Coordinador. R3: ST y TD no marca por si solo (el alcance lo garantizan L2a y L4).
        if (OtrosProgramas.IsMatch(normalizado))
        {
            return ResultadoGuardia.Marca("R4");
        }

        if ((sd || st) && programas.Any(p => !principal.ProgramasCoordinados.Contains(p, StringComparer.Ordinal)))
        {
            return ResultadoGuardia.Marca("R6");
        }

        return ResultadoGuardia.Ninguna;
    }

    private static List<string> ProgramasReferenciados(string normalizado, RosterSnapshot roster)
    {
        var relleno = " " + normalizado + " ";
        var resultado = new List<string>();
        foreach (var p in roster.Programas)
        {
            var porId = NormalizadorTexto.Normalizar(p.Id);
            var porNombre = NormalizadorTexto.Normalizar(p.NombreNormalizado);
            if ((porId.Length > 0 && relleno.Contains(" " + porId + " ", StringComparison.Ordinal))
                || (porNombre.Length > 0 && relleno.Contains(" " + porNombre + " ", StringComparison.Ordinal)))
            {
                resultado.Add(p.Id);
            }
        }

        return resultado;
    }

    private static Regex Construir(string alternativas) =>
        new(Ini + "(?:" + alternativas + ")" + Fin, RegexOptions.CultureInvariant | RegexOptions.Compiled);
}
