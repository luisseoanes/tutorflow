using TutorFlow.Application.Ports;

namespace TutorFlow.Application.Retrieval;

/// <summary>Reglas comunes de elegibilidad (SPEC 7.3): nunca untrusted; no vigentes solo con mencion explicita.</summary>
public static class RetrievalFilter
{
    public static bool MencionaReglamentoAnterior(string pregunta)
    {
        if (string.IsNullOrWhiteSpace(pregunta))
        {
            return false;
        }

        var t = SpanishTextNormalizer.RemoveDiacritics(pregunta).ToLowerInvariant();
        return t.Contains("2023", StringComparison.Ordinal) || t.Contains("reglamento anterior", StringComparison.Ordinal);
    }

    public static IEnumerable<Chunk> Elegibles(IEnumerable<Chunk> chunks, string pregunta)
    {
        var incluirNoVigentes = MencionaReglamentoAnterior(pregunta);
        return chunks.Where(c => !c.Untrusted && (c.Vigente || incluirNoVigentes));
    }

    public static Fragmento ToFragmento(Chunk c) => new(c.ChunkId, c.Documento, c.Seccion, c.Texto);
}

/// <summary>Todos los chunks vigentes y confiables (o tambien los no vigentes con mencion explicita).</summary>
public sealed class FullContextRetriever(IChunkCatalog catalog) : IRetriever
{
    public Task<IReadOnlyList<Fragmento>> RecuperarAsync(string pregunta, CancellationToken ct)
    {
        IReadOnlyList<Fragmento> result = RetrievalFilter.Elegibles(catalog.Chunks, pregunta)
            .Select(RetrievalFilter.ToFragmento)
            .ToList();
        return Task.FromResult(result);
    }
}

/// <summary>BM25 propio en memoria (k1=1.2, b=0.75), top-k=4. Sin abstencion por puntaje.</summary>
public sealed class Bm25Retriever : IRetriever
{
    public const int DefaultTopK = 4;
    private readonly IChunkCatalog _catalog;
    private readonly int _topK;
    private readonly Bm25Index _index;

    public Bm25Retriever(IChunkCatalog catalog, int topK = DefaultTopK)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentOutOfRangeException.ThrowIfLessThan(topK, 1);
        _catalog = catalog;
        _topK = topK;
        // El indice solo contiene chunks confiables: la cuarentena no influye ni en el IDF.
        _index = new Bm25Index(catalog.Chunks.Where(c => !c.Untrusted).ToList());
    }

    public Task<IReadOnlyList<Fragmento>> RecuperarAsync(string pregunta, CancellationToken ct)
    {
        var elegibles = RetrievalFilter.Elegibles(_catalog.Chunks, pregunta).Select(c => c.ChunkId).ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<Fragmento> result = _index.Search(pregunta)
            .Where(s => s.Score > 0 && elegibles.Contains(s.Chunk.ChunkId))
            .Take(_topK)
            .Select(s => RetrievalFilter.ToFragmento(s.Chunk))
            .ToList();
        return Task.FromResult(result);
    }
}

public readonly record struct ScoredChunk(Chunk Chunk, double Score);

/// <summary>Indice BM25 (Robertson/Lucene IDF). Desempate estable por orden del catalogo.</summary>
public sealed class Bm25Index
{
    private const double K1 = 1.2;
    private const double B = 0.75;

    private readonly IReadOnlyList<Chunk> _chunks;
    private readonly List<Dictionary<string, int>> _tf = [];
    private readonly int[] _len;
    private readonly Dictionary<string, int> _df = new(StringComparer.Ordinal);
    private readonly double _avgLen;

    public Bm25Index(IReadOnlyList<Chunk> chunks)
    {
        _chunks = chunks;
        _len = new int[chunks.Count];
        for (var i = 0; i < chunks.Count; i++)
        {
            var tokens = SpanishTextNormalizer.Tokenize(chunks[i].Titulo + " " + chunks[i].Texto);
            _len[i] = tokens.Count;
            var tf = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var t in tokens)
            {
                tf[t] = tf.GetValueOrDefault(t) + 1;
            }

            _tf.Add(tf);
            foreach (var term in tf.Keys)
            {
                _df[term] = _df.GetValueOrDefault(term) + 1;
            }
        }

        _avgLen = chunks.Count == 0 ? 0 : _len.Average();
    }

    public IReadOnlyList<ScoredChunk> Search(string query)
    {
        var terms = SpanishTextNormalizer.Tokenize(query).Distinct(StringComparer.Ordinal).ToList();
        var n = _chunks.Count;
        var scored = new List<(int Index, double Score)>(n);
        for (var i = 0; i < n; i++)
        {
            var score = 0.0;
            foreach (var term in terms)
            {
                if (!_tf[i].TryGetValue(term, out var f))
                {
                    continue;
                }

                var df = _df[term];
                var idf = Math.Log(1 + ((n - df + 0.5) / (df + 0.5)));
                score += idf * (f * (K1 + 1)) / (f + (K1 * (1 - B + (B * _len[i] / _avgLen))));
            }

            scored.Add((i, score));
        }

        return scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Index)
            .Select(s => new ScoredChunk(_chunks[s.Index], s.Score))
            .ToList();
    }
}
