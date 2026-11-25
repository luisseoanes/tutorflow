using System.Reflection;
using TutorFlow.Application.Ports;
using TutorFlow.Domain.Authorization;
using TutorFlow.Infrastructure.DependencyInjection;

namespace TutorFlow.Architecture.Tests;

public class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(PrincipalAcademico).Assembly;
    private static readonly Assembly Application = typeof(IClock).Assembly;
    private static readonly Assembly Infrastructure = typeof(CoreServiceCollectionExtensions).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    private static string[] Refs(Assembly a) => a.GetReferencedAssemblies().Select(n => n.Name!).ToArray();

    [Fact]
    [Trait("Estructura", "capas")]
    public void Domain_no_referencia_otras_capas_ni_aspnet()
    {
        var refs = Refs(Domain);
        Assert.DoesNotContain(refs, r => r.StartsWith("TutorFlow.", StringComparison.Ordinal));
        Assert.DoesNotContain(refs, r => r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(refs, r => r.StartsWith("Microsoft.Extensions", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Estructura", "capas")]
    public void Domain_csproj_sin_referencias_de_proyecto_ni_paquetes()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot.Path, "src", "TutorFlow.Domain", "TutorFlow.Domain.csproj"));
        Assert.DoesNotContain("ProjectReference", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("PackageReference", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("FrameworkReference", csproj, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Estructura", "capas")]
    public void Application_solo_depende_de_Domain()
    {
        var refs = Refs(Application);
        Assert.Contains("TutorFlow.Domain", refs);
        Assert.DoesNotContain("TutorFlow.Infrastructure", refs);
        Assert.DoesNotContain("TutorFlow.Api", refs);
    }

    [Fact]
    [Trait("Estructura", "capas")]
    public void Infrastructure_no_depende_de_Api()
    {
        var refs = Refs(Infrastructure);
        Assert.DoesNotContain("TutorFlow.Api", refs);
    }

    [Fact]
    [Trait("Estructura", "capas")]
    public void Api_referencia_Application_e_Infrastructure()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot.Path, "src", "TutorFlow.Api", "TutorFlow.Api.csproj"));
        Assert.Contains("TutorFlow.Application.csproj", csproj, StringComparison.Ordinal);
        Assert.Contains("TutorFlow.Infrastructure.csproj", csproj, StringComparison.Ordinal);
        Assert.NotNull(Api);
    }
}
