namespace TutorFlow.Application.Retrieval;

/// <summary>Unidad indexable del reglamento (SPEC 7.2).</summary>
public sealed record Chunk(
    string ChunkId,
    string Documento,
    string Seccion,
    string Titulo,
    string Texto,
    bool Vigente,
    bool Untrusted,
    string Hash);

public sealed record KnowledgeDocument(string Nombre, string Contenido);

/// <summary>Catalogo inmutable de chunks indexados al arrancar.</summary>
public interface IChunkCatalog
{
    IReadOnlyList<Chunk> Chunks { get; }
}
