using System.Text.RegularExpressions;

namespace TutorFlow.Application.Retrieval;

/// <summary>
/// Detector heuristico de instrucciones incrustadas. SOLO para pruebas (SPEC 6.4, RAG-14.c):
/// no se usa para filtrar en ejecucion; la cuarentena es por lista.
/// </summary>
public static partial class InstructionDetector
{
    [GeneratedRegex(@"\b(ignora|ignore|olvida|omite)\w*\s+(todas?\s+)?(tus|las|sus|all|previous|prior)\b.{0,30}\b(instruccion\w*|instructions)", RegexOptions.CultureInvariant)]
    private static partial Regex IgnorePattern();

    [GeneratedRegex(@"\binstrucciones\s+anteriores\b|\b(previous|prior)\s+instructions\b|\bsystem\s+prompt\b", RegexOptions.CultureInvariant)]
    private static partial Regex PreviousPattern();

    [GeneratedRegex(@"\b(recordatorio|mensaje|nota)\s+(interno|interna)\s+para\s+(el|la)\s+(asistente|modelo|ia)\b", RegexOptions.CultureInvariant)]
    private static partial Regex AddressedToAssistantPattern();

    public static bool Detecta(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var t = SpanishTextNormalizer.RemoveDiacritics(texto).ToLowerInvariant();
        return IgnorePattern().IsMatch(t) || PreviousPattern().IsMatch(t) || AddressedToAssistantPattern().IsMatch(t);
    }
}
