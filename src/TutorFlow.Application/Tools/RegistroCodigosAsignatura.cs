using System.Text.Json;
using TutorFlow.Application.Ports;
using TutorFlow.Domain.Authorization;

namespace TutorFlow.Application.Tools;

/// <summary>
/// Decorador por peticion de <see cref="IToolDispatcher"/>: acumula los valores de las propiedades "codigo" (codigos de
/// asignatura) que devuelven las herramientas ejecutadas en ESTA peticion. L5 los usa para no confundir un codigo de
/// asignatura con un id de estudiante (SPEC 6.2 L5 habla de estudiantes). No decide permisos ni altera el resultado.
/// </summary>
internal sealed class RegistroCodigosAsignatura : IToolDispatcher
{
    private readonly IToolDispatcher _interno;
    private readonly HashSet<string> _codigos = new(StringComparer.Ordinal);

    public RegistroCodigosAsignatura(IToolDispatcher interno)
    {
        ArgumentNullException.ThrowIfNull(interno);
        _interno = interno;
    }

    public IReadOnlyCollection<string> Codigos => _codigos;

    public IReadOnlyList<ToolDefinition> HerramientasExpuestas(PrincipalAcademico principal) =>
        _interno.HerramientasExpuestas(principal);

    public async Task<ResultadoHerramienta> DespacharAsync(
        PrincipalAcademico principal,
        InvocacionPropuesta invocacion,
        CancellationToken ct)
    {
        var resultado = await _interno.DespacharAsync(principal, invocacion, ct).ConfigureAwait(false);
        try
        {
            using var doc = JsonDocument.Parse(resultado.Contenido);
            Recoger(doc.RootElement);
        }
        catch (JsonException)
        {
            // Contenido no JSON: no aporta codigos.
        }

        return resultado;
    }

    private void Recoger(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                {
                    if (p.Name == "codigo" && p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() is { Length: > 0 } c)
                    {
                        _codigos.Add(c);
                    }
                    else
                    {
                        Recoger(p.Value);
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var x in e.EnumerateArray())
                {
                    Recoger(x);
                }

                break;
        }
    }
}
