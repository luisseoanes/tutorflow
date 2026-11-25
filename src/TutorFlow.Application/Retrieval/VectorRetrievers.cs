using TutorFlow.Application.Ports;

namespace TutorFlow.Application.Retrieval;

/// <summary>Ranking completo de chunks elegibles para una pregunta (base de top-k y de RRF).</summary>
public interface IChunkRanker
{
    Task<IReadOnlyList<Chunk>> RankearAsync(string pregunta, CancellationToken ct);
}

/// <summary>Persistencia opcional de embeddings de chunks (clave = modelo + hash del chunk).</summary>
public interface IEmbeddingCache
{
    float[]? Obtener(string clave);

    void Guardar(string clave, float[] vector);

    void Persistir();
}

public static class Vectores
{
    public static double Coseno(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Dimensiones de embedding distintas.");
        }

        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            na += (double)a[i] * a[i];
            nb += (double)b[i] * b[i];
        }

        return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }
}

/// <summary>Embeddings de chunks confiables + coseno en memoria, top-k=4. Los untrusted jamas se envian al proveedor.</summary>
public sealed class VectorRetriever : IRetriever, IChunkRanker
{
    public const int DefaultTopK = 4;
    private const int Lote = 64;
    private readonly IChunkCatalog _catalog;
    private readonly IEmbeddingClient _embeddings;
    private readonly string _modelo;
    private readonly IEmbeddingCache? _cache;
    private readonly int _topK;
    private readonly SemaphoreSlim _init = new(1, 1);
    private Dictionary<string, float[]>? _vectores;

    public VectorRetriever(IChunkCatalog catalog, IEmbeddingClient embeddings, string modelo, IEmbeddingCache? cache = null, int topK = DefaultTopK)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(embeddings);
        ArgumentOutOfRangeException.ThrowIfLessThan(topK, 1);
        _catalog = catalog;
        _embeddings = embeddings;
        _modelo = modelo ?? "";
        _cache = cache;
        _topK = topK;
    }

    public async Task<IReadOnlyList<Fragmento>> RecuperarAsync(string pregunta, CancellationToken ct)
    {
        var rank = await RankearAsync(pregunta, ct).ConfigureAwait(false);
        return rank.Take(_topK).Select(RetrievalFilter.ToFragmento).ToList();
    }

    public async Task<IReadOnlyList<Chunk>> RankearAsync(string pregunta, CancellationToken ct)
    {
        var elegibles = RetrievalFilter.Elegibles(_catalog.Chunks, pregunta).ToList();
        if (elegibles.Count == 0 || string.IsNullOrWhiteSpace(pregunta))
        {
            return [];
        }

        var vectores = await VectoresDeChunksAsync(ct).ConfigureAwait(false);
        var q = (await _embeddings.VectorizarAsync([pregunta], ct).ConfigureAwait(false)).Single();
        return elegibles
            .Select((c, i) => (Chunk: c, Indice: i, Score: Vectores.Coseno(q, vectores[c.ChunkId])))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Indice)
            .Select(x => x.Chunk)
            .ToList();
    }

    private static string TextoIndexable(Chunk c) => c.Titulo + "\n" + c.Texto;

    private async Task<Dictionary<string, float[]>> VectoresDeChunksAsync(CancellationToken ct)
    {
        if (_vectores is not null)
        {
            return _vectores;
        }

        await _init.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_vectores is not null)
            {
                return _vectores;
            }

            // Solo chunks confiables (vigentes o no): el contenido en cuarentena no sale hacia el proveedor.
            var confiables = _catalog.Chunks.Where(c => !c.Untrusted).ToList();
            var resultado = new Dictionary<string, float[]>(StringComparer.Ordinal);
            var pendientes = new List<Chunk>();
            foreach (var c in confiables)
            {
                var v = _cache?.Obtener(Clave(c));
                if (v is not null)
                {
                    resultado[c.ChunkId] = v;
                }
                else
                {
                    pendientes.Add(c);
                }
            }

            for (var i = 0; i < pendientes.Count; i += Lote)
            {
                var lote = pendientes.Skip(i).Take(Lote).ToList();
                var vs = await _embeddings.VectorizarAsync(lote.Select(TextoIndexable).ToList(), ct).ConfigureAwait(false);
                if (vs.Count != lote.Count)
                {
                    throw new InvalidOperationException("El proveedor de embeddings devolvio una cantidad distinta de vectores.");
                }

                for (var j = 0; j < lote.Count; j++)
                {
                    resultado[lote[j].ChunkId] = vs[j];
                    _cache?.Guardar(Clave(lote[j]), vs[j]);
                }
            }

            if (pendientes.Count > 0)
            {
                _cache?.Persistir();
            }

            _vectores = resultado;
            return resultado;
        }
        finally
        {
            _init.Release();
        }
    }

    private string Clave(Chunk c) => _modelo + "|" + c.Hash;
}

/// <summary>Reciprocal Rank Fusion: score = suma 1/(k + rango), rango desde 1.</summary>
public static class Rrf
{
    public const int DefaultK = 60;

    public static IReadOnlyList<string> Fusionar(IReadOnlyList<IReadOnlyList<string>> rankings, int k = DefaultK)
    {
        var score = new Dictionary<string, double>(StringComparer.Ordinal);
        var orden = new List<string>();
        foreach (var r in rankings)
        {
            for (var i = 0; i < r.Count; i++)
            {
                if (!score.ContainsKey(r[i]))
                {
                    orden.Add(r[i]);
                }

                score[r[i]] = score.GetValueOrDefault(r[i]) + (1.0 / (k + i + 1));
            }
        }

        // Desempate estable: primera aparicion (la lista del primer ranking manda).
        return orden.Select((id, i) => (id, i)).OrderByDescending(x => score[x.id]).ThenBy(x => x.i).Select(x => x.id).ToList();
    }
}

/// <summary>BM25 + vector con RRF (k=60), top-k=4.</summary>
public sealed class HybridRetriever(IChunkRanker bm25, IChunkRanker vector, int topK = 4, int k = Rrf.DefaultK) : IRetriever
{
    public async Task<IReadOnlyList<Fragmento>> RecuperarAsync(string pregunta, CancellationToken ct)
    {
        var a = await bm25.RankearAsync(pregunta, ct).ConfigureAwait(false);
        var b = await vector.RankearAsync(pregunta, ct).ConfigureAwait(false);
        var porId = new Dictionary<string, Chunk>(StringComparer.Ordinal);
        foreach (var c in a.Concat(b))
        {
            porId[c.ChunkId] = c;
        }

        var fusion = Rrf.Fusionar([a.Select(c => c.ChunkId).ToList(), b.Select(c => c.ChunkId).ToList()], k);
        return fusion.Take(topK).Select(id => RetrievalFilter.ToFragmento(porId[id])).ToList();
    }
}
