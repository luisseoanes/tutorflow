using System.Globalization;
using System.Text;

namespace TutorFlow.Application.Retrieval;

/// <summary>Normalizacion para BM25: minusculas, sin tildes, stopwords y stemming ligero.</summary>
public static class SpanishTextNormalizer
{
    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "a", "al", "algo", "ante", "como", "con", "contra", "cual", "cuales", "cuando", "cuanto", "cuantos", "cuantas",
        "de", "del", "desde", "donde", "e", "el", "ella", "ellos", "en", "entre", "era", "es", "esa", "ese", "eso",
        "esta", "estan", "este", "esto", "fue", "ha", "han", "hay", "la", "las", "le", "les", "lo", "los", "mas",
        "me", "mi", "mis", "muy", "ni", "no", "nos", "o", "para", "pero", "por", "porque", "que", "quien", "se",
        "sea", "ser", "si", "sin", "sobre", "son", "su", "sus", "tu", "tus", "un", "una", "uno", "unos", "unas",
        "y", "ya", "yo", "puedo", "puede", "tengo", "tiene",
    };

    public static string RemoveDiacritics(string text)
    {
        var d = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Tokeniza una CONSULTA: ademas de sus terminos, agrega las equivalencias academicas generales.</summary>
    public static IReadOnlyList<string> TokenizeQuery(string query)
    {
        var tokens = new List<string>(Tokenize(query));
        if (string.IsNullOrEmpty(query))
        {
            return tokens;
        }

        var plain = RemoveDiacritics(query).ToLowerInvariant();
        var sb = new StringBuilder();
        void Expand()
        {
            if (sb.Length == 0)
            {
                return;
            }

            foreach (var eq in AcademicEquivalences.Expand(sb.ToString()))
            {
                tokens.AddRange(Tokenize(eq));
            }

            sb.Clear();
        }

        foreach (var ch in plain)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
            else
            {
                Expand();
            }
        }

        Expand();
        return tokens;
    }

    public static IReadOnlyList<string> Tokenize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var plain = RemoveDiacritics(text).ToLowerInvariant();
        var tokens = new List<string>();
        var sb = new StringBuilder();
        foreach (var ch in plain)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
            else if (sb.Length > 0)
            {
                Flush(sb, tokens);
            }
        }

        if (sb.Length > 0)
        {
            Flush(sb, tokens);
        }

        return tokens;
    }

    private static void Flush(StringBuilder sb, List<string> tokens)
    {
        var word = sb.ToString();
        sb.Clear();
        if (Stopwords.Contains(word))
        {
            return;
        }

        tokens.Add(Stem(word));
    }

    /// <summary>Stemming ligero: plurales, "-ciones" y vocal final.</summary>
    internal static string Stem(string w)
    {
        if (w.Length == 0 || char.IsDigit(w[0]))
        {
            return w;
        }

        if (w.EndsWith("ciones", StringComparison.Ordinal))
        {
            w = w[..^6] + "cion";
        }
        else if (w.Length > 5 && w.EndsWith("es", StringComparison.Ordinal))
        {
            w = w[..^2];
        }
        else if (w.Length > 3 && w.EndsWith('s'))
        {
            w = w[..^1];
        }

        if (w.Length > 4 && (w.EndsWith('a') || w.EndsWith('e') || w.EndsWith('o')))
        {
            w = w[..^1];
        }

        return w;
    }
}
