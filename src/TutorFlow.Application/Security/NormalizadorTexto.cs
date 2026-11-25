using System.Globalization;
using System.Text;

namespace TutorFlow.Application.Security;

/// <summary>
/// Normalizacion de las guardias L2a/L2b/L5 (SPEC 6.7): NFD sin diacriticos, minusculas, todo lo que no sea letra
/// o digito pasa a ser un espacio y los espacios se colapsan.
/// </summary>
public static class NormalizadorTexto
{
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(texto.Length);
        var espacioPendiente = false;
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                if (espacioPendiente && sb.Length > 0)
                {
                    sb.Append(' ');
                }

                espacioPendiente = false;
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                espacioPendiente = true;
            }
        }

        return sb.ToString();
    }

    public static string[] Palabras(string normalizado) =>
        normalizado.Length == 0 ? [] : normalizado.Split(' ');
}
