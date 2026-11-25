using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TutorFlow.Application.Retrieval;

/// <summary>Segmentacion estructural determinista (SPEC 7.2).</summary>
public static partial class ChunkSegmenter
{
    [GeneratedRegex(@"^\*\*Artículo\s+(\d+)\.\s*(.*?)\.?\*\*", RegexOptions.CultureInvariant)]
    private static partial Regex ArticleRegex();

    [GeneratedRegex(@"^(#{2,3})\s+(.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(\d+(?:\.\d+)*)\.?\s+(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedRegex();

    [GeneratedRegex(@"^Anexo\b\.?\s*(.*)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex AnexoRegex();

    private sealed record Draft(string Seccion, string Titulo, List<string> Lineas);

    public static IReadOnlyList<Chunk> Segment(string documento, string contenido)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documento);
        ArgumentNullException.ThrowIfNull(contenido);

        var lineas = contenido.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var modoArticulos = lineas.Any(l => ArticleRegex().IsMatch(l));
        var vigente = !documento.Contains("2023", StringComparison.Ordinal);

        var drafts = new List<Draft>();
        Draft? actual = null;
        foreach (var linea in lineas)
        {
            var art = ArticleRegex().Match(linea);
            if (art.Success)
            {
                actual = new Draft($"Artículo {art.Groups[1].Value}", art.Groups[2].Value.Trim(), [linea]);
                drafts.Add(actual);
                continue;
            }

            var head = HeadingRegex().Match(linea);
            if (head.Success)
            {
                if (modoArticulos)
                {
                    actual = null; // capitulos: cierran el articulo previo, no generan chunk
                    continue;
                }

                var (seccion, titulo) = CanonicalizeHeading(head.Groups[2].Value);
                actual = new Draft(seccion, titulo, [linea]);
                drafts.Add(actual);
                continue;
            }

            actual?.Lineas.Add(linea);
        }

        var chunks = new List<Chunk>();
        foreach (var d in drafts)
        {
            var texto = string.Join("\n", d.Lineas).Trim();
            if (!modoArticulos && !TieneTextoPropio(d.Lineas))
            {
                continue; // seccion padre sin texto propio
            }

            chunks.Add(new Chunk(
                ChunkId: $"{documento}#{d.Seccion}",
                Documento: documento,
                Seccion: d.Seccion,
                Titulo: d.Titulo,
                Texto: texto,
                Vigente: vigente,
                Untrusted: false,
                Hash: Hash(texto)));
        }

        return chunks;
    }

    private static bool TieneTextoPropio(List<string> lineas) =>
        lineas.Skip(1).Any(l => !string.IsNullOrWhiteSpace(l));

    private static (string Seccion, string Titulo) CanonicalizeHeading(string heading)
    {
        var anexo = AnexoRegex().Match(heading);
        if (anexo.Success)
        {
            return ("Anexo", anexo.Groups[1].Value.Trim());
        }

        var num = NumberedRegex().Match(heading);
        if (num.Success)
        {
            return ($"Sección {num.Groups[1].Value}", num.Groups[2].Value.Trim());
        }

        return (heading.Trim(), heading.Trim());
    }

    internal static string Hash(string texto) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();
}

public static class KnowledgeIndexer
{
    /// <summary>Segmenta todos los documentos (orden ordinal por nombre) y marca la cuarentena por lista de chunkId.</summary>
    public static IReadOnlyList<Chunk> Build(IEnumerable<KnowledgeDocument> documentos, IEnumerable<string> cuarentena)
    {
        ArgumentNullException.ThrowIfNull(documentos);
        ArgumentNullException.ThrowIfNull(cuarentena);
        var q = new HashSet<string>(cuarentena.Select(x => x.Trim()).Where(x => x.Length > 0), StringComparer.Ordinal);
        return documentos
            .OrderBy(d => d.Nombre, StringComparer.Ordinal)
            .SelectMany(d => ChunkSegmenter.Segment(d.Nombre, d.Contenido))
            .Select(c => q.Contains(c.ChunkId) ? c with { Untrusted = true } : c)
            .ToList();
    }
}
