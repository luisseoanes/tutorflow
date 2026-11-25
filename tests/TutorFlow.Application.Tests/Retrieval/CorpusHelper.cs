using TutorFlow.Application.Retrieval;

namespace TutorFlow.Application.Tests.Retrieval;

internal static class CorpusHelper
{
    public const string DefaultQuarantine = "bienestar_y_becas.md#Anexo";

    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TutorFlow.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("No se encontro TutorFlow.sln subiendo desde " + AppContext.BaseDirectory);
    }

    public static string KnowledgeDir() => Path.Combine(RepoRoot(), "q10", "material", "reglamento");

    public static IReadOnlyList<KnowledgeDocument> Documents() =>
        Directory.EnumerateFiles(KnowledgeDir(), "*.md")
            .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
            .Select(f => new KnowledgeDocument(Path.GetFileName(f), File.ReadAllText(f)))
            .ToList();

    public static IReadOnlyList<Chunk> Build(params string[] quarantine) =>
        KnowledgeIndexer.Build(Documents(), quarantine);

    public static IReadOnlyList<Chunk> BuildDefault() => Build(DefaultQuarantine);

    public static IChunkCatalog Catalog(params string[] quarantine) => new StaticCatalog(Build(quarantine));

    public static IChunkCatalog DefaultCatalog() => Catalog(DefaultQuarantine);

    private sealed class StaticCatalog(IReadOnlyList<Chunk> chunks) : IChunkCatalog
    {
        public IReadOnlyList<Chunk> Chunks { get; } = chunks;
    }
}
