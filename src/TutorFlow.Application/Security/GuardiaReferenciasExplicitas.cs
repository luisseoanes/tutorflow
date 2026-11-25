using TutorFlow.Application.Ports;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Security;

/// <summary>
/// L2a (SPEC 6.2): referencias explicitas a estudiantes del roster (id en variantes de mayusculas y separadores,
/// nombre completo, apellido o nombre de pila UNICOS) y a ids de coordinadores distintos del principal.
/// Funcion pura sobre el texto normalizado; nunca consulta datos academicos ni usa el LLM. Los nombres de
/// coordinadores no se detectan (m8). Los patrones de id se derivan de los ids del roster, sin prefijos fijos.
/// </summary>
public static class GuardiaReferenciasExplicitas
{
    private static readonly HashSet<string> Particulas = new(StringComparer.Ordinal)
    {
        "de", "del", "la", "las", "los", "el", "y", "e", "da", "di", "van", "von", "san",
    };

    /// <summary>Evalua una pregunta del usuario (incluye ids de coordinadores).</summary>
    public static ResultadoGuardia Evaluar(string pregunta, PrincipalAcademico principal, RosterSnapshot roster) =>
        Evaluar(pregunta, principal, roster, incluirCoordinadores: true, regla: "referencia");

    /// <summary>Nucleo compartido con L5: <paramref name="incluirCoordinadores"/> false solo mira estudiantes.</summary>
    internal static ResultadoGuardia Evaluar(
        string texto,
        PrincipalAcademico principal,
        RosterSnapshot roster,
        bool incluirCoordinadores,
        string regla)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(roster);

        var normalizado = NormalizadorTexto.Normalizar(texto);
        if (normalizado.Length == 0)
        {
            return ResultadoGuardia.Ninguna;
        }

        var palabras = NormalizadorTexto.Palabras(normalizado);
        var conjunto = new HashSet<string>(palabras, StringComparer.Ordinal);
        var relleno = " " + normalizado + " ";
        var fuera = new List<string>();

        // --- ids (estudiantes y, si aplica, coordinadores) ---
        var estudiantesPorId = new Dictionary<string, RosterEstudiante>(StringComparer.Ordinal);
        foreach (var e in roster.Estudiantes)
        {
            estudiantesPorId[Compacto(e.Id)] = e;
        }

        var coordinadoresPorId = new Dictionary<string, RosterCoordinador>(StringComparer.Ordinal);
        foreach (var c in roster.Coordinadores)
        {
            coordinadoresPorId[Compacto(c.Id)] = c;
        }

        var propio = Compacto(principal.Id);
        var prefijosEstudiante = Prefijos(roster.Estudiantes.Select(e => e.Id));
        var prefijosCoordinador = Prefijos(roster.Coordinadores.Select(c => c.Id));
        AnadirPrefijo(principal.Rol == RolAcademico.Estudiante ? prefijosEstudiante : prefijosCoordinador, propio);

        foreach (var candidato in CandidatosDeId(palabras))
        {
            if (candidato == propio)
            {
                continue;
            }

            if (CoincidePrefijo(candidato, prefijosEstudiante))
            {
                if (estudiantesPorId.TryGetValue(candidato, out var est))
                {
                    if (!EnAlcance(principal, est))
                    {
                        fuera.Add(est.Id);
                    }
                }
                else
                {
                    // Id con forma de estudiante ausente del roster: denegado (SPEC 6.1).
                    fuera.Add(candidato.ToUpperInvariant());
                }
            }
            else if (incluirCoordinadores && CoincidePrefijo(candidato, prefijosCoordinador))
            {
                fuera.Add(coordinadoresPorId.TryGetValue(candidato, out var coord) ? coord.Id : candidato.ToUpperInvariant());
            }
        }

        // --- nombres: completo, o parte unica en el roster ---
        var nombres = roster.Estudiantes
            .Select(e =>
            {
                var nombre = NormalizadorTexto.Normalizar(e.NombreNormalizado);
                return (Est: e, Nombre: nombre, Partes: PartesDeNombre(nombre));
            })
            .ToList();
        var conteo = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var x in nombres)
        {
            foreach (var parte in x.Partes)
            {
                conteo[parte] = conteo.GetValueOrDefault(parte) + 1;
            }
        }

        foreach (var (est, nombre, partes) in nombres)
        {
            // Una parte del nombre solo se resuelve si es unica en el roster (un nombre ambiguo no se resuelve).
            var mencionado = (nombre.Length > 0 && relleno.Contains(" " + nombre + " ", StringComparison.Ordinal))
                || partes.Any(p => conteo[p] == 1 && conjunto.Contains(p));

            if (mencionado && !EnAlcance(principal, est))
            {
                fuera.Add(est.Id);
            }
        }

        var objetivos = fuera.Distinct(StringComparer.Ordinal).ToList();
        return objetivos.Count == 0 ? ResultadoGuardia.Ninguna : ResultadoGuardia.Marca(regla, objetivos);
    }

    private static bool EnAlcance(PrincipalAcademico principal, RosterEstudiante est) =>
        principal.Rol == RolAcademico.Estudiante
            ? string.Equals(principal.Id, est.Id, StringComparison.Ordinal)
            : principal.ProgramasCoordinados.Contains(est.ProgramaId, StringComparer.Ordinal);

    private static string[] PartesDeNombre(string nombreNormalizado) =>
        NormalizadorTexto.Palabras(nombreNormalizado)
            .Where(p => p.Length >= 3 && !Particulas.Contains(p) && !p.All(char.IsDigit))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string Compacto(string id) =>
        NormalizadorTexto.Normalizar(id).Replace(" ", string.Empty, StringComparison.Ordinal);

    /// <summary>Prefijo alfabetico de ids con forma letras+digitos (derivado del roster, no fijo).</summary>
    private static HashSet<string> Prefijos(IEnumerable<string> ids)
    {
        var prefijos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            AnadirPrefijo(prefijos, Compacto(id));
        }

        return prefijos;
    }

    private static void AnadirPrefijo(HashSet<string> prefijos, string compacto)
    {
        var i = 0;
        while (i < compacto.Length && char.IsLetter(compacto[i]))
        {
            i++;
        }

        if (i > 0 && i < compacto.Length && compacto[i..].All(char.IsDigit))
        {
            prefijos.Add(compacto[..i]);
        }
    }

    private static bool CoincidePrefijo(string candidato, HashSet<string> prefijos)
    {
        foreach (var p in prefijos)
        {
            if (candidato.Length > p.Length
                && candidato.StartsWith(p, StringComparison.Ordinal)
                && candidato[p.Length..].All(char.IsDigit))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Cada palabra y cada par de palabras contiguas, sin separador ("est 002" y "est002" dan "est002").</summary>
    private static IEnumerable<string> CandidatosDeId(string[] palabras)
    {
        for (var i = 0; i < palabras.Length; i++)
        {
            yield return palabras[i];
            if (i + 1 < palabras.Length)
            {
                yield return palabras[i] + palabras[i + 1];
            }
        }
    }
}
