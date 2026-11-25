using System.Text.Json;

namespace TutorFlow.Api.Tests.Contract;

/// <summary>
/// Esquema JSON del contrato de salida (SPEC 3.1/3.2) y un validador minimo escrito a mano con System.Text.Json
/// (sin paquetes). Soporta el subconjunto de JSON Schema que usa el esquema: type (object, array, string, boolean,
/// number, integer), properties, required, additionalProperties:false, items y minimum.
/// </summary>
internal static class EsquemaContrato
{
    public const string Esquema = """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["respuesta", "citas", "usoDatosEstudiante", "tokens", "costoUsd"],
          "properties": {
            "respuesta": { "type": "string" },
            "citas": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["documento", "seccion"],
                "properties": {
                  "documento": { "type": "string" },
                  "seccion": { "type": "string" }
                }
              }
            },
            "usoDatosEstudiante": { "type": "boolean" },
            "tokens": { "type": "integer", "minimum": 0 },
            "costoUsd": { "type": "number", "minimum": 0 }
          }
        }
        """;

    /// <summary>Valida <paramref name="instancia"/> contra el esquema del contrato; devuelve los errores (vacio = valido).</summary>
    public static IReadOnlyList<string> Validar(JsonElement instancia)
    {
        using var esquema = JsonDocument.Parse(Esquema);
        var errores = new List<string>();
        Validar(instancia, esquema.RootElement, "$", errores);
        return errores;
    }

    public static IReadOnlyList<string> Validar(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Validar(doc.RootElement);
    }

    private static void Validar(JsonElement valor, JsonElement esquema, string ruta, List<string> errores)
    {
        if (esquema.TryGetProperty("type", out var tipo) && !CumpleTipo(valor, tipo.GetString()!))
        {
            errores.Add($"{ruta}: se esperaba {tipo.GetString()} y llego {valor.ValueKind}");
            return;
        }

        if (esquema.TryGetProperty("minimum", out var minimo) && valor.ValueKind == JsonValueKind.Number
            && valor.GetDecimal() < minimo.GetDecimal())
        {
            errores.Add($"{ruta}: {valor.GetRawText()} es menor que el minimo {minimo.GetRawText()}");
        }

        if (valor.ValueKind == JsonValueKind.Object)
        {
            var props = esquema.TryGetProperty("properties", out var p) ? p : default;
            if (esquema.TryGetProperty("required", out var requeridas))
            {
                foreach (var r in requeridas.EnumerateArray())
                {
                    if (!valor.TryGetProperty(r.GetString()!, out _))
                    {
                        errores.Add($"{ruta}: falta la propiedad requerida '{r.GetString()}'");
                    }
                }
            }

            foreach (var propiedad in valor.EnumerateObject())
            {
                // Los nombres distinguen mayusculas: 'Usuario' no es 'usuario'.
                if (props.ValueKind == JsonValueKind.Object && props.TryGetProperty(propiedad.Name, out var sub))
                {
                    Validar(propiedad.Value, sub, $"{ruta}.{propiedad.Name}", errores);
                }
                else if (esquema.TryGetProperty("additionalProperties", out var extra) && extra.ValueKind == JsonValueKind.False)
                {
                    errores.Add($"{ruta}: propiedad no permitida '{propiedad.Name}'");
                }
            }
        }
        else if (valor.ValueKind == JsonValueKind.Array && esquema.TryGetProperty("items", out var items))
        {
            var i = 0;
            foreach (var elemento in valor.EnumerateArray())
            {
                Validar(elemento, items, $"{ruta}[{i++}]", errores);
            }
        }
    }

    private static bool CumpleTipo(JsonElement v, string tipo) => tipo switch
    {
        "object" => v.ValueKind == JsonValueKind.Object,
        "array" => v.ValueKind == JsonValueKind.Array,
        "string" => v.ValueKind == JsonValueKind.String,
        "boolean" => v.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "number" => v.ValueKind == JsonValueKind.Number,
        "integer" => v.ValueKind == JsonValueKind.Number && EsEntero(v),
        _ => throw new InvalidOperationException("tipo de esquema no soportado: " + tipo),
    };

    private static bool EsEntero(JsonElement v)
    {
        var crudo = v.GetRawText();
        return v.TryGetInt64(out _) && !crudo.Contains('.', StringComparison.Ordinal)
            && !crudo.Contains('e', StringComparison.OrdinalIgnoreCase);
    }
}
