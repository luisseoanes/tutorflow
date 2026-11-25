namespace TutorFlow.Architecture.Tests;

public class NoHardcodedDatasetTests
{
    private static readonly string[] Forbidden = ["EST-00", "COORD-0", "Laura", "Andrés"];

    [Fact]
    [Trait("Criterio", "RES-12.b")]
    public void Ningun_archivo_de_src_contiene_datos_del_conjunto_original()
    {
        var files = RepoRoot.Files("src").ToList();
        Assert.NotEmpty(files);
        var hits = new List<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var token in Forbidden)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                {
                    hits.Add($"{Path.GetRelativePath(RepoRoot.Path, file)}: {token}");
                }
            }
        }

        Assert.Empty(hits);
    }
}
