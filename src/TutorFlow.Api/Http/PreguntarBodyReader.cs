using System.Text.Json;
using Microsoft.Net.Http.Headers;
using TutorFlow.Application.Preguntar;

namespace TutorFlow.Api.Http;

/// <summary>
/// Lectura manual del cuerpo con <see cref="JsonDocument"/> (SPEC 3.3, ADR A-03). Aplica en ORDEN las reglas 1-4 (400);
/// la regla 5 (403-identidad) la resuelve L1: aqui solo se extrae <c>rol</c>/<c>id</c> como string o null.
/// Los nombres de propiedad son exactos y con distincion de mayusculas; las desconocidas se ignoran.
/// </summary>
public static class PreguntarBodyReader
{
    public const int MaxBytes = 64 * 1024;
    public const int MaxPregunta = 1000;

    public static async Task<LecturaCuerpo> LeerAsync(HttpRequest request, string requestId, CancellationToken ct)
    {
        // Regla 1: Content-Type no JSON.
        if (!EsContentTypeJson(request.ContentType))
        {
            return LecturaCuerpo.Falla("Content-Type no admitido: se espera application/json.");
        }

        // Regla 1: cuerpo vacio o mayor de 64 KB (se lee a lo sumo MaxBytes + 1).
        if (request.ContentLength is > MaxBytes)
        {
            return LecturaCuerpo.Falla("El cuerpo supera el tamano maximo permitido.");
        }

        var bytes = await LeerAcotadoAsync(request.Body, ct);
        if (bytes is null)
        {
            return LecturaCuerpo.Falla("El cuerpo supera el tamano maximo permitido.");
        }

        if (bytes.Length == 0)
        {
            return LecturaCuerpo.Falla("El cuerpo esta vacio.");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(bytes);
        }
        catch (JsonException)
        {
            return LecturaCuerpo.Falla("El cuerpo no es JSON valido.");
        }

        using (doc)
        {
            return Evaluar(doc.RootElement, requestId);
        }
    }

    private static LecturaCuerpo Evaluar(JsonElement raiz, string requestId)
    {
        // Regla 1: la raiz debe ser un objeto.
        if (raiz.ValueKind != JsonValueKind.Object)
        {
            return LecturaCuerpo.Falla("La raiz del cuerpo debe ser un objeto JSON.");
        }

        // Regla 2: propiedades duplicadas en la raiz o en `usuario`.
        if (TieneDuplicados(raiz))
        {
            return LecturaCuerpo.Falla("El cuerpo contiene propiedades duplicadas.");
        }

        var tieneUsuario = raiz.TryGetProperty("usuario", out var usuario);
        if (tieneUsuario && usuario.ValueKind == JsonValueKind.Object && TieneDuplicados(usuario))
        {
            return LecturaCuerpo.Falla("El cuerpo contiene propiedades duplicadas.");
        }

        // Regla 3: pregunta string, no vacia ni solo espacios, de a lo sumo 1000 caracteres.
        if (!raiz.TryGetProperty("pregunta", out var preguntaEl) || preguntaEl.ValueKind != JsonValueKind.String)
        {
            return LecturaCuerpo.Falla("La propiedad 'pregunta' es obligatoria y debe ser un texto.");
        }

        string? pregunta;
        try
        {
            pregunta = preguntaEl.GetString();
        }
        catch (InvalidOperationException)
        {
            return LecturaCuerpo.Falla("La propiedad 'pregunta' no es un texto valido.");
        }

        if (string.IsNullOrWhiteSpace(pregunta))
        {
            return LecturaCuerpo.Falla("La propiedad 'pregunta' no puede estar vacia.");
        }

        if (pregunta.Length > MaxPregunta)
        {
            return LecturaCuerpo.Falla("La propiedad 'pregunta' supera la longitud maxima permitida.");
        }

        // Regla 4: usuario ausente o que no es un objeto.
        if (!tieneUsuario || usuario.ValueKind != JsonValueKind.Object)
        {
            return LecturaCuerpo.Falla("La propiedad 'usuario' es obligatoria y debe ser un objeto.");
        }

        // Regla 5 (la decide L1): rol/id ausente, null o de otro tipo -> null -> 403-identidad.
        var rol = LeerString(usuario, "rol");
        var id = LeerString(usuario, "id");
        return LecturaCuerpo.Ok(new PreguntarComando(pregunta, rol, id, requestId));
    }

    private static string? LeerString(JsonElement objeto, string nombre)
    {
        if (!objeto.TryGetProperty(nombre, out var el) || el.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        try
        {
            return el.GetString();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Nombres comparados ya des-escapados y de forma Ordinal (<c>usuario</c> duplica <c>usuario</c>).</summary>
    private static bool TieneDuplicados(JsonElement objeto)
    {
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var propiedad in objeto.EnumerateObject())
        {
            if (!vistos.Add(propiedad.Name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EsContentTypeJson(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType) || !MediaTypeHeaderValue.TryParse(contentType, out var media))
        {
            return false;
        }

        return string.Equals(media.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Lee hasta MaxBytes; devuelve null si el cuerpo es mayor.</summary>
    private static async Task<byte[]?> LeerAcotadoAsync(Stream body, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var leidos = await body.ReadAsync(buffer, ct);
            if (leidos == 0)
            {
                return ms.ToArray();
            }

            if (ms.Length + leidos > MaxBytes)
            {
                return null;
            }

            ms.Write(buffer, 0, leidos);
        }
    }
}

/// <summary>Resultado de leer el cuerpo: un comando valido o un 400 con un detalle fijo (sin eco de la entrada).</summary>
public sealed record LecturaCuerpo(PreguntarComando? Comando, string? Detalle)
{
    public static LecturaCuerpo Ok(PreguntarComando comando) => new(comando, null);

    public static LecturaCuerpo Falla(string detalle) => new(null, detalle);
}
