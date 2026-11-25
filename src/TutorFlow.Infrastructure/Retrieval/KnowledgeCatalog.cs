using TutorFlow.Application.Retrieval;

namespace TutorFlow.Infrastructure.Retrieval;

/// <summary>Indexa al arrancar los .md de KNOWLEDGE_PATH (solo lectura) de forma determinista.</summary>
public sealed class KnowledgeCatalog : IChunkCatalog
{
    public KnowledgeCatalog(string knowledgePath, IEnumerable<string> cuarentena)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgePath);
        if (!Directory.Exists(knowledgePath))
        {
            throw new DirectoryNotFoundException($"KNOWLEDGE_PATH no existe: {knowledgePath}");
        }

        var documentos = Directory.EnumerateFiles(knowledgePath, "*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
            .Select(f => new KnowledgeDocument(Path.GetFileName(f), File.ReadAllText(f)))
            .ToList();
        if (documentos.Count == 0)
        {
            throw new InvalidOperationException($"No hay documentos .md en KNOWLEDGE_PATH: {knowledgePath}");
        }

        Chunks = KnowledgeIndexer.Build(documentos, cuarentena);
    }

    public IReadOnlyList<Chunk> Chunks { get; }
}
