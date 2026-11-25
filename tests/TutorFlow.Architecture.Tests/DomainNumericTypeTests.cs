using System.Reflection;
using System.Text.RegularExpressions;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Architecture.Tests;

public class DomainNumericTypeTests
{
    private static readonly Assembly Domain = typeof(PrincipalAcademico).Assembly;

    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static bool UsesBinaryFloat(Type? t)
    {
        if (t is null)
        {
            return false;
        }

        if (t.IsByRef || t.IsPointer || t.IsArray)
        {
            return UsesBinaryFloat(t.GetElementType());
        }

        if (t == typeof(double) || t == typeof(float))
        {
            return true;
        }

        return t.IsGenericType && t.GetGenericArguments().Any(UsesBinaryFloat);
    }

    private static IEnumerable<string> Offenders()
    {
        foreach (var type in Domain.GetTypes())
        {
            foreach (var f in type.GetFields(All).Where(f => UsesBinaryFloat(f.FieldType)))
            {
                yield return $"{type.FullName}.{f.Name} (campo)";
            }

            foreach (var p in type.GetProperties(All).Where(p => UsesBinaryFloat(p.PropertyType)))
            {
                yield return $"{type.FullName}.{p.Name} (propiedad)";
            }

            foreach (var e in type.GetEvents(All).Where(e => UsesBinaryFloat(e.EventHandlerType)))
            {
                yield return $"{type.FullName}.{e.Name} (evento)";
            }

            var methods = type.GetMethods(All).Cast<MethodBase>().Concat(type.GetConstructors(All));
            foreach (var m in methods)
            {
                if (m is MethodInfo mi && UsesBinaryFloat(mi.ReturnType))
                {
                    yield return $"{type.FullName}.{m.Name} (retorno)";
                }

                foreach (var prm in m.GetParameters().Where(prm => UsesBinaryFloat(prm.ParameterType)))
                {
                    yield return $"{type.FullName}.{m.Name}({prm.Name}) (parametro)";
                }
            }
        }
    }

    private static string StripCommentsAndStrings(string code)
    {
        code = Regex.Replace(code, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        code = Regex.Replace(code, @"//[^\n]*", string.Empty);
        code = Regex.Replace(code, "\"(?:\\\\.|[^\"\\\\])*\"", "\"\"");
        return code;
    }

    [Fact]
    [Trait("Criterio", "ACAD-05.b")]
    public void Ningun_tipo_de_Domain_usa_double_ni_float_por_reflexion()
    {
        Assert.NotEmpty(Domain.GetTypes());
        Assert.Empty(Offenders());
    }

    [Fact]
    [Trait("Criterio", "ACAD-05.b")]
    public void Fuentes_de_Domain_no_usan_double_ni_float()
    {
        var files = RepoRoot.Files(Path.Combine("src", "TutorFlow.Domain"), "*.cs").ToList();
        Assert.NotEmpty(files);
        var rx = new Regex(@"\b(double|float|Double|Single)\b");
        var hits = files
            .SelectMany(f => rx.Matches(StripCommentsAndStrings(File.ReadAllText(f)))
                .Select(m => $"{Path.GetRelativePath(RepoRoot.Path, f)}: {m.Value}"))
            .ToList();
        Assert.Empty(hits);
    }
}
