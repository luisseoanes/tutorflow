using TutorFlow.Application.Retrieval;

namespace TutorFlow.Infrastructure.Retrieval;

/// <summary>Indexa al arrancar los .md de KNOWLEDGE_PATH (solo lectura) de forma determinista.</summary>
public sealed class KnowledgeCatalog : IChunkCatalog
{
    public KnowledgeCatalog(string knowledgePath, IEnumerable<string> cuarentena, Action<string>? advertir = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgePath);

        // Robustez (RES-11.a): sin corpus la app arranca con catalogo vacio y las preguntas normativas se abstienen.
        if (!Directory.Exists(knowledgePath))
        {
            advertir?.Invoke($"KNOWLEDGE_PATH no existe ({knowledgePath}); catalogo vacio, las preguntas normativas se abstendran.");
            Chunks = [];
            return;
        }

        var documentos = Directory.EnumerateFiles(knowledgePath, "*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
            .Select(f => new KnowledgeDocument(Path.GetFileName(f), File.ReadAllText(f)))
            .ToList();
        if (documentos.Count == 0)
        {
            advertir?.Invoke($"No hay documentos .md en KNOWLEDGE_PATH ({knowledgePath}); catalogo vacio, las preguntas normativas se abstendran.");
            Chunks = [];
            return;
        }

        Chunks = KnowledgeIndexer.Build(documentos, cuarentena);
    }

    public IReadOnlyList<Chunk> Chunks { get; }
}
