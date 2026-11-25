namespace TutorFlow.Architecture.Tests;

internal static class RepoRoot
{
    private static readonly Lazy<string> Value = new(Find);

    public static string Path => Value.Value;

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "TutorFlow.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("No se encontro TutorFlow.sln subiendo desde " + AppContext.BaseDirectory);
    }

    /// <summary>Archivos bajo una carpeta, excluyendo bin/ y obj/.</summary>
    public static IEnumerable<string> Files(string relativeDir, string searchPattern = "*")
    {
        var root = System.IO.Path.Combine(Path, relativeDir);
        return Directory.EnumerateFiles(root, searchPattern, SearchOption.AllDirectories)
            .Where(f =>
            {
                var rel = System.IO.Path.GetRelativePath(root, f)
                    .Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                return !rel.Any(s => s is "bin" or "obj");
            });
    }
}
