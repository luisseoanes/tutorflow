using System.Text.Json;

namespace TutorFlow.Application.Tools;

/// <summary>Argumentos ya validados (todos opcionales; null = omitido).</summary>
internal sealed record ArgumentosHerramienta(string? EstudianteId, string? ProgramaId, string? Asignatura);

/// <summary>
/// Validacion estricta de argumentos (L4, SPEC 6.2): objeto JSON, solo propiedades del esquema (sin extras ni duplicadas),
/// tipo string, formato de id <c>^[A-Za-z0-9-]{1,32}$</c>. Devuelve null si no es valido; nunca lanza por datos del LLM.
/// </summary>
internal static class ArgumentValidator
{
    private const int MaxAsignatura = 100;

    public static ArgumentosHerramienta? Validar(ToolSpec spec, JsonElement argumentos)
    {
        string? estudiante = null;
        string? programa = null;
        string? asignatura = null;

        // Sin argumentos (default o null) equivale a {}.
        if (argumentos.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return new ArgumentosHerramienta(null, null, null);
        }

        if (argumentos.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in argumentos.EnumerateObject())
        {
            if (!vistos.Add(p.Name) || p.Value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var valor = p.Value.GetString();
            switch (p.Name)
            {
                case "estudianteId" when spec.Individual:
                    if (!IdValido(valor))
                    {
                        return null;
                    }

                    estudiante = valor;
                    break;
                case "programaId" when !spec.Individual:
                    if (!IdValido(valor))
                    {
                        return null;
                    }

                    programa = valor;
                    break;
                case "asignatura" when spec.AdmiteAsignatura:
                    if (!AsignaturaValida(valor))
                    {
                        return null;
                    }

                    asignatura = valor;
                    break;
                default:
                    return null;
            }
        }

        return new ArgumentosHerramienta(estudiante, programa, asignatura);
    }

    public static bool IdValido(string? valor)
    {
        if (string.IsNullOrEmpty(valor) || valor.Length > 32)
        {
            return false;
        }

        foreach (var c in valor)
        {
            var ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AsignaturaValida(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor) || valor.Length > MaxAsignatura)
        {
            return false;
        }

        return !valor.Any(char.IsControl);
    }
}
