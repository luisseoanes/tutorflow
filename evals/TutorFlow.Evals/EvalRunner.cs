using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Runner [eval] de TutorFlow (SPEC 7.6, AGENTS 6, T-10).
// - Habla SOLO HTTP con la API en ejecucion (no lee .env, no recibe llaves, no escribe cabeceras en el crudo).
// - Evaluacion DETERMINISTA contra `esperado` (sin LLM-juez). Lo que no se reduce a un patron (`nota`) se marca "revision manual".
// - Cuota agotada o proveedor caido: se detiene, guarda lo hecho como corrida PARCIAL y no inventa resultados.
internal static class EvalRunner
{
    private const double UmbralPrecision = 0.90;
    private const double UmbralCobertura = 0.90;
    private const double UmbralAbstencion = 0.90;
    private const double UmbralNumerica = 0.90;
    private const string TextoAbstencion = "No encuentro esa información en los documentos disponibles.";

    private sealed record Opciones(
        string Raiz,
        string BaseUrl,
        string Modelo,
        string Estrategia,
        int PausaMs,
        int Reintentos,
        int MaxFallosSeguidos,
        string? Salida,
        HashSet<string> Solo,
        string? Etiqueta,
        string? Commit);

    private sealed record Caso(
        string Id,
        string Pregunta,
        string Rol,
        string UsuarioId,
        JsonElement? CuerpoCrudo,
        IReadOnlyList<string> Criterios,
        JsonElement Esperado);

    private sealed record Chequeo(string Nombre, bool Ok, string Detalle);

    private sealed record Resultado(
        Caso Caso,
        int Status,
        int Intentos,
        long LatenciaMs,
        string Respuesta,
        IReadOnlyList<string> Citas,
        bool? UsoDatos,
        long Tokens,
        double CostoUsd,
        IReadOnlyList<Chequeo> Chequeos,
        IReadOnlyList<string> NoVerificables,
        bool RevisionManual,
        string? Error)
    {
        public bool Ok => Error is null && Chequeos.All(c => c.Ok);
    }

    internal static int Run(string[] args)
    {
        var o = LeerOpciones(args, out var error);
        if (o is null)
        {
            Console.Error.WriteLine(error);
            return 2;
        }

        try
        {
            return Ejecutar(o).GetAwaiter().GetResult();
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException)
        {
            Console.Error.WriteLine("ERROR del runner [eval]: " + e.Message);
            return 2;
        }
    }

    private static Opciones? LeerOpciones(string[] args, out string? error)
    {
        error = null;
        string? raiz = null;
        string? baseUrl = null;
        string? modelo = null;
        string? estrategia = null;
        string? salida = null;
        string? etiqueta = null;
        string? commit = null;
        var pausa = 5000;
        var reintentos = 2;
        var seguidos = 3;
        var solo = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            string Sig() => i + 1 < args.Length ? args[++i] : throw new InvalidOperationException("falta el valor de " + args[i]);
            try
            {
                switch (args[i])
                {
                    case "eval": break;
                    case "--raiz": raiz = Sig(); break;
                    case "--base-url": baseUrl = Sig(); break;
                    case "--modelo": modelo = Sig(); break;
                    case "--estrategia": estrategia = Sig(); break;
                    case "--pausa-ms": pausa = int.Parse(Sig(), CultureInfo.InvariantCulture); break;
                    case "--reintentos": reintentos = int.Parse(Sig(), CultureInfo.InvariantCulture); break;
                    case "--max-fallos-seguidos": seguidos = int.Parse(Sig(), CultureInfo.InvariantCulture); break;
                    case "--out": salida = Sig(); break;
                    case "--etiqueta": etiqueta = Sig(); break;
                    case "--commit": commit = Sig(); break;
                    case "--solo":
                        foreach (var id in Sig().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            solo.Add(id);
                        }

                        break;
                    default:
                        error = $"Argumento desconocido: {args[i]}";
                        return null;
                }
            }
            catch (Exception e) when (e is FormatException or InvalidOperationException)
            {
                error = "Argumento invalido: " + e.Message;
                return null;
            }
        }

        if (string.IsNullOrWhiteSpace(modelo) || string.IsNullOrWhiteSpace(estrategia))
        {
            error = "Uso: eval --modelo <id> --estrategia <full-context|bm25|...> [--base-url http://localhost:8080] [--pausa-ms 5000] [--reintentos 2] [--max-fallos-seguidos 3] [--solo q01,q02] [--out <ruta>] [--etiqueta <texto>]";
            return null;
        }

        return new Opciones(raiz ?? BuscarRaiz(), baseUrl ?? "http://localhost:8080", modelo, estrategia, pausa, reintentos, seguidos, salida, solo, etiqueta, commit);
    }

    private static string BuscarRaiz()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TutorFlow.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("No se encontro TutorFlow.sln; use --raiz.");
    }

    private static async Task<int> Ejecutar(Opciones o)
    {
        var inv = CultureInfo.InvariantCulture;
        var criteriosEval = LeerCriteriosEval(Path.Combine(o.Raiz, "escenarios"));
        var todos = LeerDataset(Path.Combine(o.Raiz, "evals", "dataset.jsonl"));
        var omitidosSinEval = todos.Where(c => !c.Criterios.Any(criteriosEval.Contains)).Select(c => c.Id).ToList();
        var casos = todos.Where(c => c.Criterios.Any(criteriosEval.Contains)).ToList();
        if (o.Solo.Count > 0)
        {
            casos = casos.Where(c => o.Solo.Contains(c.Id)).ToList();
        }

        Console.WriteLine($"[eval] {casos.Count} casos a ejecutar contra {o.BaseUrl} (modelo={o.Modelo}, estrategia={o.Estrategia}, pausa={o.PausaMs} ms)");
        if (omitidosSinEval.Count > 0 && o.Solo.Count == 0)
        {
            Console.WriteLine("[eval] omitidos (sin criterio [eval]; solo [ret]): " + string.Join(", ", omitidosSinEval));
        }

        using var http = new HttpClient { BaseAddress = new Uri(o.BaseUrl), Timeout = TimeSpan.FromSeconds(120) };
        var resultados = new List<Resultado>();
        var n429 = 0;
        var n503 = 0;
        var seguidos = 0;
        var parcial = false;
        string? motivoParcial = null;
        var reloj = Stopwatch.StartNew();

        for (var idx = 0; idx < casos.Count; idx++)
        {
            var c = casos[idx];
            if (idx > 0 && o.PausaMs > 0)
            {
                await Task.Delay(o.PausaMs);
            }

            var r = await EjecutarCaso(http, c, o, () => n429++, () => n503++);
            resultados.Add(r);
            Console.WriteLine($"[eval] {idx + 1}/{casos.Count} {c.Id}: HTTP {(r.Status == 0 ? "-" : r.Status.ToString(inv))} tokens={r.Tokens} {r.LatenciaMs} ms intentos={r.Intentos} -> {(r.Ok ? "OK" : "FALLA")}{(r.Error is not null ? " (" + r.Error + ")" : string.Empty)}");
            foreach (var ch in r.Chequeos.Where(x => !x.Ok))
            {
                Console.WriteLine($"         - {ch.Nombre}: {ch.Detalle}");
            }

            if (r.Status is 503 or 429 or 0)
            {
                seguidos++;
                if (seguidos >= o.MaxFallosSeguidos)
                {
                    parcial = true;
                    motivoParcial = $"{seguidos} casos seguidos sin respuesta util (503/429/red) tras reintentos: posible cuota diaria agotada o proveedor no disponible";
                    Console.WriteLine("[eval] DETENIDO: " + motivoParcial);
                    break;
                }
            }
            else
            {
                seguidos = 0;
            }
        }

        reloj.Stop();
        var ejecutadosIds = resultados.Select(r => r.Caso.Id).ToHashSet(StringComparer.Ordinal);
        var noEjecutados = casos.Where(c => !ejecutadosIds.Contains(c.Id)).Select(c => c.Id).ToList();

        // Casos con respuesta util (no se cuentan en metricas los que no obtuvieron respuesta por 503/429/red).
        var validos = resultados.Where(r => r.Error is null).ToList();

        // --- Metricas SPEC 7.6 ---
        var (precision, cobertura, exactAbst, exactNum, conCitas, conAbst, conCifras) = MetricasDeterministas(validos);

        var tokens = resultados.Sum(r => r.Tokens);
        var costo = resultados.Sum(r => r.CostoUsd);
        var conLlm = validos.Where(r => r.Tokens > 0).ToList();
        var lat = validos.Select(r => r.LatenciaMs).OrderBy(x => x).ToList();
        var latLlm = conLlm.Select(r => r.LatenciaMs).OrderBy(x => x).ToList();

        // --- Criterios [eval] ---
        var filasCriterios = new List<(string Id, string Estado, string Casos, string Nota)>();
        foreach (var id in criteriosEval.OrderBy(x => x, StringComparer.Ordinal))
        {
            var delCriterio = todos.Where(c => c.Criterios.Contains(id)).ToList();
            if (delCriterio.Count == 0)
            {
                filasCriterios.Add((id, "NO EJECUTADO", "-", "sin caso en el dataset (meta o sin pregunta)"));
                continue;
            }

            var estados = new List<string>();
            var notas = new List<string>();
            foreach (var c in delCriterio)
            {
                var r = resultados.FirstOrDefault(x => x.Caso.Id == c.Id);
                if (r is null || r.Error is not null)
                {
                    estados.Add("NE");
                    continue;
                }

                // AUTH-25.c solo se refiere al codigo de estado (200 salvo la pregunta 10).
                var ok = id == "AUTH-25.c" ? r.Chequeos.Where(x => x.Nombre == "status").All(x => x.Ok) : r.Ok;
                estados.Add(ok ? "OK" : "FALLA");
                if (r.RevisionManual)
                {
                    notas.Add($"{c.Id}: revision manual de la nota");
                }

                if (r.NoVerificables.Count > 0 && id != "AUTH-25.c")
                {
                    notas.Add($"{c.Id}: no verificable por HTTP ({string.Join(",", r.NoVerificables)})");
                }
            }

            var estado = estados.Contains("FALLA") ? "NO CUMPLE" : estados.Contains("NE") ? "NO EJECUTADO" : "CUMPLE";
            filasCriterios.Add((id, estado, string.Join(",", delCriterio.Select(c => c.Id)), string.Join("; ", notas.Distinct())));
        }

        // --- Consola ---
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("| Criterio [eval] | Estado | Casos | Notas |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var f in filasCriterios)
        {
            sb.AppendLine($"| {f.Id} | {f.Estado} | {f.Casos} | {f.Nota} |");
        }

        sb.AppendLine();
        string Umb(double? v, double u) => v is null ? "n/a" : $"{v.Value.ToString("F4", inv)} (umbral >= {u.ToString("F2", inv)}: {(v >= u ? "CUMPLE" : "NO CUMPLE")})";
        sb.AppendLine($"- Precision de citas: {Umb(precision, UmbralPrecision)} sobre {conCitas.Count} casos");
        sb.AppendLine($"- Cobertura de citas: {Umb(cobertura, UmbralCobertura)}");
        sb.AppendLine($"- Exactitud de abstencion: {Umb(exactAbst, UmbralAbstencion)} sobre {conAbst.Count} casos");
        sb.AppendLine($"- Exactitud numerica: {Umb(exactNum, UmbralNumerica)} sobre {conCifras.Count} casos");
        sb.AppendLine($"- Tokens={tokens}, costoUsd (equivalente a precio de pago)={costo.ToString("F6", inv)}, 429={n429}, 503={n503}");
        sb.AppendLine($"- Latencia (todas) p50={Pct(lat, 50)} ms, p95={Pct(lat, 95)} ms; (con LLM) p50={Pct(latLlm, 50)} ms, p95={Pct(latLlm, 95)} ms");
        sb.AppendLine($"- Casos ejecutados {resultados.Count}/{casos.Count}; OK {resultados.Count(r => r.Ok)}; FALLA {resultados.Count(r => !r.Ok)}");
        sb.AppendLine(parcial ? $"RESULTADO: PARCIAL ({motivoParcial}); sin ejecutar: {string.Join(", ", noEjecutados)}" : "RESULTADO: COMPLETA");
        Console.Write(sb.ToString());

        // --- Crudo ---
        var commitCompleto = o.Commit ?? Git(o.Raiz, "rev-parse HEAD");
        var sha = commitCompleto is null ? "sin-git" : commitCompleto[..Math.Min(7, commitCompleto.Length)];
        var fecha = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", inv);
        var ruta = o.Salida ?? Path.Combine(o.Raiz, "evals", "runs", $"eval-{fecha}-{sha}-{Slug(o.Modelo)}-{Slug(o.Estrategia)}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ruta))!);
        var umbralesOk = new[] { precision is null || precision >= UmbralPrecision, cobertura is null || cobertura >= UmbralCobertura, exactAbst is null || exactAbst >= UmbralAbstencion, exactNum is null || exactNum >= UmbralNumerica };
        var doc = new
        {
            modo = "eval",
            etiqueta = o.Etiqueta,
            commit = commitCompleto,
            fecha = DateTime.UtcNow.ToString("o", inv),
            modelo = o.Modelo,
            estrategia = o.Estrategia,
            baseUrl = o.BaseUrl,
            pausaMs = o.PausaMs,
            resultado = parcial ? "PARCIAL" : "COMPLETA",
            motivoParcial,
            duracionS = Math.Round(reloj.Elapsed.TotalSeconds, 1),
            casosPlanificados = casos.Count,
            casosEjecutados = resultados.Count,
            noEjecutados,
            omitidosSinCriterioEval = omitidosSinEval,
            metricas = new
            {
                precisionCitas = Redondeo(precision),
                coberturaCitas = Redondeo(cobertura),
                exactitudAbstencion = Redondeo(exactAbst),
                exactitudNumerica = Redondeo(exactNum),
                umbrales = new { precisionCitas = UmbralPrecision, coberturaCitas = UmbralCobertura, exactitudAbstencion = UmbralAbstencion, exactitudNumerica = UmbralNumerica },
                cumpleUmbrales = !parcial && umbralesOk.All(x => x),
                tokens,
                costoUsdEquivalente = Math.Round(costo, 6),
                preguntasEjecutadas = resultados.Count,
                preguntasConLlm = conLlm.Count,
                costoMedioPorPregunta = resultados.Count == 0 ? (double?)null : Math.Round(costo / resultados.Count, 6),
                costoMedioPorPreguntaConLlm = conLlm.Count == 0 ? (double?)null : Math.Round(conLlm.Sum(r => r.CostoUsd) / conLlm.Count, 6),
                tokensMediosPorPreguntaConLlm = conLlm.Count == 0 ? (double?)null : Math.Round((double)conLlm.Sum(r => r.Tokens) / conLlm.Count, 1),
                latenciaMs = new { p50 = Pct(lat, 50), p95 = Pct(lat, 95), p50ConLlm = Pct(latLlm, 50), p95ConLlm = Pct(latLlm, 95) },
                respuestas429 = n429,
                respuestas503 = n503,
            },
            criterios = filasCriterios.Select(f => new { id = f.Id, estado = f.Estado, casos = f.Casos, notas = f.Nota }),
            casos = resultados.Select(r => new
            {
                id = r.Caso.Id,
                criterios = r.Caso.Criterios,
                usuario = new { rol = r.Caso.Rol, id = r.Caso.UsuarioId },
                pregunta = r.Caso.Pregunta,
                status = r.Status,
                intentos = r.Intentos,
                latenciaMs = r.LatenciaMs,
                tokens = r.Tokens,
                costoUsd = r.CostoUsd,
                respuesta = r.Respuesta,
                citas = r.Citas,
                usoDatosEstudiante = r.UsoDatos,
                ok = r.Ok,
                error = r.Error,
                revisionManual = r.RevisionManual,
                noVerificablesPorHttp = r.NoVerificables,
                chequeos = r.Chequeos.Select(x => new { nombre = x.Nombre, ok = x.Ok, detalle = x.Detalle }),
            }),
        };
        var opciones = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        File.WriteAllText(ruta, JsonSerializer.Serialize(doc, opciones) + "\n");
        Console.WriteLine("JSON: " + Path.GetRelativePath(o.Raiz, ruta));

        if (parcial)
        {
            return 3;
        }

        var hayFalla = filasCriterios.Any(f => f.Estado == "NO CUMPLE") || umbralesOk.Any(x => !x);
        return hayFalla ? 1 : 0;
    }

    private static (double? Precision, double? Cobertura, double? ExactAbst, double? ExactNum, List<Resultado> ConCitas, List<Resultado> ConAbst, List<Resultado> ConCifras) MetricasDeterministas(List<Resultado> validos)
    {
        var conCitas = validos.Where(r => Esp(r.Caso).TryGetProperty("citas", out var a) && a.GetArrayLength() > 0).ToList();
        int devueltas = 0, devueltasEsperadas = 0, esperadas = 0, esperadasPresentes = 0;
        foreach (var r in conCitas)
        {
            var esp = Esp(r.Caso);
            var set = Lista(esp, "citas");
            var permitidas = Lista(esp, "citas_permitidas");
            var universo = set.Concat(permitidas).ToList();
            devueltas += r.Citas.Count;
            devueltasEsperadas += r.Citas.Count(x => universo.Contains(x, StringComparer.OrdinalIgnoreCase));
            esperadas += set.Count;
            esperadasPresentes += set.Count(x => r.Citas.Contains(x, StringComparer.OrdinalIgnoreCase));
        }

        double? precision = devueltas == 0 ? null : (double)devueltasEsperadas / devueltas;
        double? cobertura = esperadas == 0 ? null : (double)esperadasPresentes / esperadas;

        var conAbst = validos.Where(r => Esp(r.Caso).TryGetProperty("abstencion", out _)).ToList();
        var abstOk = conAbst.Count(r => EsAbstencion(r) == Esp(r.Caso).GetProperty("abstencion").GetBoolean());
        double? exactAbst = conAbst.Count == 0 ? null : (double)abstOk / conAbst.Count;

        var conCifras = validos.Where(r => Lista(Esp(r.Caso), "cifras").Count > 0).ToList();
        var cifrasOk = conCifras.Count(r => Lista(Esp(r.Caso), "cifras").All(f => CifraPresente(f, r.Respuesta)));
        double? exactNum = conCifras.Count == 0 ? null : (double)cifrasOk / conCifras.Count;
        return (precision, cobertura, exactAbst, exactNum, conCitas, conAbst, conCifras);
    }

    /// <summary>
    /// Re-evaluacion OFFLINE (sin API ni LLM): aplica los chequeos deterministas del dataset actual a las respuestas
    /// guardadas en un crudo. No sustituye a una corrida real: no mide nada nuevo del producto.
    /// Uso: eval-reevaluar &lt;crudo.json&gt; [--raiz dir] [--detalle]
    /// </summary>
    internal static int Reevaluar(string[] args)
    {
        string? crudo = null;
        string? raiz = null;
        var detalle = false;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--raiz": raiz = args[++i]; break;
                case "--detalle": detalle = true; break;
                default: crudo = args[i]; break;
            }
        }

        if (crudo is null)
        {
            Console.Error.WriteLine("Uso: reevaluar <crudo.json> [--raiz <dir>] [--detalle]");
            return 2;
        }

        try
        {
            raiz ??= BuscarRaiz();
            var inv = CultureInfo.InvariantCulture;
            var porId = LeerDataset(Path.Combine(raiz, "evals", "dataset.jsonl")).ToDictionary(c => c.Id, StringComparer.Ordinal);
            using var doc = JsonDocument.Parse(File.ReadAllText(crudo));
            var r = doc.RootElement;
            var resultados = new List<Resultado>();
            foreach (var cs in r.GetProperty("casos").EnumerateArray())
            {
                var id = cs.GetProperty("id").GetString()!;
                if (!porId.TryGetValue(id, out var caso))
                {
                    Console.WriteLine($"[reevaluar] caso {id} del crudo ya no existe en el dataset; se omite");
                    continue;
                }

                var err = cs.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
                var citas = cs.GetProperty("citas").EnumerateArray().Select(x => x.GetString()!).ToList();
                bool? uso = cs.TryGetProperty("usoDatosEstudiante", out var u) && u.ValueKind is JsonValueKind.True or JsonValueKind.False ? u.GetBoolean() : null;
                resultados.Add(err is not null
                    ? new Resultado(caso, cs.GetProperty("status").GetInt32(), 0, 0, string.Empty, [], null, 0, 0, [], [], false, err)
                    : EvaluarCampos(caso, cs.GetProperty("status").GetInt32(), cs.GetProperty("respuesta").GetString() ?? string.Empty, citas, uso, cs.GetProperty("tokens").GetInt64(), cs.GetProperty("costoUsd").GetDouble(), 0, 0, null));
            }

            var validos = resultados.Where(x => x.Error is null).ToList();
            var (precision, cobertura, exactAbst, exactNum, conCitas, conAbst, conCifras) = MetricasDeterministas(validos);
            string F(double? v, double u) => v is null ? "n/a" : $"{v.Value.ToString("F4", inv)} (umbral >= {u.ToString("F2", inv)}: {(v >= u ? "CUMPLE" : "NO CUMPLE")})";
            Console.WriteLine($"[reevaluar] RE-EVALUACION OFFLINE (sin LLM ni API) de {Path.GetFileName(crudo)} (modelo={r.GetProperty("modelo").GetString()}, estrategia={r.GetProperty("estrategia").GetString()}, commit={r.GetProperty("commit").GetString()?[..7]}) con el dataset actual");
            Console.WriteLine($"- Precision de citas: {F(precision, UmbralPrecision)} sobre {conCitas.Count} casos");
            Console.WriteLine($"- Cobertura de citas: {F(cobertura, UmbralCobertura)}");
            Console.WriteLine($"- Exactitud de abstencion: {F(exactAbst, UmbralAbstencion)} sobre {conAbst.Count} casos");
            Console.WriteLine($"- Exactitud numerica: {F(exactNum, UmbralNumerica)} sobre {conCifras.Count} casos");
            Console.WriteLine($"- Casos {resultados.Count}; OK {resultados.Count(x => x.Ok)}; FALLA {resultados.Count(x => !x.Ok)}");
            if (detalle)
            {
                foreach (var x in resultados.Where(x => !x.Ok))
                {
                    Console.WriteLine($"  FALLA {x.Caso.Id}");
                    foreach (var ch in x.Chequeos.Where(ch => !ch.Ok))
                    {
                        Console.WriteLine($"    - {ch.Nombre}: {ch.Detalle}");
                    }
                }
            }

            return 0;
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            Console.Error.WriteLine("ERROR de reevaluar: " + e.Message);
            return 2;
        }
    }

    private static double? Redondeo(double? v) => v is null ? null : Math.Round(v.Value, 4);

    private static string Slug(string s) => Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9.]+", "-").Trim('-');

    private static long? Pct(List<long> ordenada, int p)
    {
        if (ordenada.Count == 0)
        {
            return null;
        }

        var rango = (int)Math.Ceiling(p / 100.0 * ordenada.Count);
        return ordenada[Math.Clamp(rango - 1, 0, ordenada.Count - 1)];
    }

    private static JsonElement Esp(Caso c) => c.Esperado;

    private static List<string> Lista(JsonElement esperado, string clave) =>
        esperado.TryGetProperty(clave, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Select(x => x.GetString()!).ToList() : [];

    private static bool EsAbstencion(Resultado r) =>
        r.Status == 200 && r.Citas.Count == 0 && string.Equals(r.Respuesta.Trim(), TextoAbstencion, StringComparison.OrdinalIgnoreCase);

    private static bool CifraPresente(string cifra, string texto) =>
        Regex.IsMatch(texto, $@"(?<![\d.]){Regex.Escape(cifra)}(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool Coincide(string patron, string texto) =>
        patron.StartsWith("re:", StringComparison.Ordinal)
            ? Regex.IsMatch(texto, patron[3..], RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            : texto.Contains(patron, StringComparison.OrdinalIgnoreCase);

    private static bool CoincidePrefijo(string patron, string cita) =>
        patron.EndsWith('*') ? cita.StartsWith(patron[..^1], StringComparison.OrdinalIgnoreCase) : string.Equals(cita, patron, StringComparison.OrdinalIgnoreCase);

    // Patrones de credenciales de Google/OpenAI/Anthropic por si una respuesta los contuviera: nunca llegan al crudo.
    private static readonly Regex Credencial = new(@"AIza[0-9A-Za-z_\-]{20,}|AQ\.[0-9A-Za-z_\-]{20,}|sk-[0-9A-Za-z_\-]{20,}", RegexOptions.CultureInvariant);

    private static string Sanear(string s) => Credencial.Replace(s, "[REDACTADO]");

    private static async Task<Resultado> EjecutarCaso(HttpClient http, Caso c, Opciones o, Action cuenta429, Action cuenta503)
    {
        var cuerpo = c.CuerpoCrudo is { } cr
            ? cr.GetRawText()
            : JsonSerializer.Serialize(new { pregunta = c.Pregunta, usuario = new { rol = c.Rol, id = c.UsuarioId } }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        var intentos = 0;
        HttpStatusCode? ultimo = null;
        string texto = string.Empty;
        long ms = 0;
        string? errorRed = null;
        for (; intentos <= o.Reintentos;)
        {
            intentos++;
            errorRed = null;
            var sw = Stopwatch.StartNew();
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "/api/asistente/preguntar")
                {
                    Content = new StringContent(cuerpo, Encoding.UTF8, "application/json"),
                };
                using var resp = await http.SendAsync(req);
                texto = await resp.Content.ReadAsStringAsync();
                ultimo = resp.StatusCode;
                sw.Stop();
                ms = sw.ElapsedMilliseconds;
                if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    cuenta429();
                }
                else if (resp.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    cuenta503();
                }

                if (resp.StatusCode is not (HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable))
                {
                    break;
                }
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                sw.Stop();
                ms = sw.ElapsedMilliseconds;
                errorRed = e.GetType().Name;
                ultimo = null;
            }

            if (intentos > o.Reintentos)
            {
                break;
            }

            var espera = TimeSpan.FromSeconds(20 * intentos); // backoff limitado: 20 s, 40 s
            Console.WriteLine($"[eval]   {c.Id}: sin respuesta util (HTTP {(ultimo is null ? "-" : ((int)ultimo.Value).ToString(CultureInfo.InvariantCulture))}); reintento {intentos}/{o.Reintentos} en {espera.TotalSeconds:F0} s");
            await Task.Delay(espera);
        }

        if (ultimo is null || ultimo is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
        {
            var st = ultimo is null ? 0 : (int)ultimo.Value;
            return new Resultado(c, st, intentos, ms, string.Empty, [], null, 0, 0, [], [], false, errorRed ?? $"HTTP {st} tras {intentos} intentos");
        }

        return Evaluar(c, (int)ultimo.Value, texto, intentos, ms);
    }

    private static Resultado Evaluar(Caso c, int status, string texto, int intentos, long ms)
    {
        string respuesta = string.Empty;
        var citas = new List<string>();
        bool? uso = null;
        long tokens = 0;
        double costo = 0;
        string? error = null;
        try
        {
            using var doc = JsonDocument.Parse(texto);
            var r = doc.RootElement;
            if (r.ValueKind == JsonValueKind.Object && r.TryGetProperty("respuesta", out var rp))
            {
                respuesta = Sanear(rp.GetString() ?? string.Empty);
                if (r.TryGetProperty("citas", out var cs) && cs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ct in cs.EnumerateArray())
                    {
                        citas.Add($"{ct.GetProperty("documento").GetString()} · {ct.GetProperty("seccion").GetString()}");
                    }
                }

                if (r.TryGetProperty("usoDatosEstudiante", out var u) && u.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    uso = u.GetBoolean();
                }

                if (r.TryGetProperty("tokens", out var t) && t.TryGetInt64(out var tk))
                {
                    tokens = tk;
                }

                if (r.TryGetProperty("costoUsd", out var cu) && cu.TryGetDouble(out var cd))
                {
                    costo = cd;
                }
            }
            else
            {
                respuesta = Sanear(texto.Length > 300 ? texto[..300] : texto);
            }
        }
        catch (JsonException)
        {
            respuesta = Sanear(texto.Length > 300 ? texto[..300] : texto);
            error = null;
        }

        return EvaluarCampos(c, status, respuesta, citas, uso, tokens, costo, intentos, ms, error);
    }

    // Chequeos deterministas sobre una respuesta ya parseada (comun a la corrida real y a `reevaluar`).
    private static Resultado EvaluarCampos(Caso c, int status, string respuesta, List<string> citas, bool? uso, long tokens, double costo, int intentos, long ms, string? error)
    {
        var esp = c.Esperado;
        var chequeos = new List<Chequeo>();

        // status
        if (esp.TryGetProperty("status", out var st))
        {
            var admitidos = st.ValueKind == JsonValueKind.Array ? st.EnumerateArray().Select(x => x.GetInt32()).ToList() : [st.GetInt32()];
            chequeos.Add(new Chequeo("status", admitidos.Contains(status), $"esperado [{string.Join(",", admitidos)}], obtenido {status}"));
        }

        foreach (var f in Lista(esp, "cifras"))
        {
            chequeos.Add(new Chequeo("cifra " + f, CifraPresente(f, respuesta), "token numerico en respuesta"));
        }

        foreach (var s in Lista(esp, "contiene"))
        {
            chequeos.Add(new Chequeo("contiene " + s, Coincide(s, respuesta), "texto obligatorio en respuesta"));
        }

        foreach (var s in Lista(esp, "prohibido"))
        {
            chequeos.Add(new Chequeo("prohibido " + s, !Coincide(s, respuesta), "no debe aparecer en respuesta"));
        }

        if (esp.TryGetProperty("citas", out var ec) && ec.ValueKind == JsonValueKind.Array)
        {
            var esperadas = Lista(esp, "citas");
            if (esperadas.Count == 0)
            {
                chequeos.Add(new Chequeo("citas == []", citas.Count == 0, $"obtenidas [{string.Join("; ", citas)}]"));
            }
            else
            {
                var faltan = esperadas.Where(x => !citas.Contains(x, StringComparer.OrdinalIgnoreCase)).ToList();
                chequeos.Add(new Chequeo("citas ⊇ esperadas", faltan.Count == 0, faltan.Count == 0 ? "todas presentes" : $"faltan [{string.Join("; ", faltan)}]; obtenidas [{string.Join("; ", citas)}]"));
            }
        }

        if (esp.TryGetProperty("citas_permitidas", out _))
        {
            var perm = Lista(esp, "citas_permitidas");
            var fuera = citas.Where(x => !perm.Contains(x, StringComparer.OrdinalIgnoreCase)).ToList();
            chequeos.Add(new Chequeo("citas ⊆ permitidas", fuera.Count == 0, fuera.Count == 0 ? "ok" : $"fuera de lo permitido [{string.Join("; ", fuera)}]"));
        }

        var prohibidas = Lista(esp, "citas_prohibidas");
        if (prohibidas.Count > 0)
        {
            var malas = citas.Where(x => prohibidas.Any(p => CoincidePrefijo(p, x))).ToList();
            chequeos.Add(new Chequeo("citas prohibidas", malas.Count == 0, malas.Count == 0 ? "ausentes" : $"presentes [{string.Join("; ", malas)}]"));
        }

        var provisional = new Resultado(c, status, intentos, ms, respuesta, citas, uso, tokens, costo, chequeos, [], false, error);
        if (esp.TryGetProperty("abstencion", out var ab))
        {
            var debe = ab.GetBoolean();
            var abstuvo = EsAbstencion(provisional);
            chequeos.Add(new Chequeo("abstencion", abstuvo == debe, $"debe={debe}, abstuvo={abstuvo} (texto exacto SPEC 7.5.5 y citas [])"));
        }

        if (esp.TryGetProperty("usoDatosEstudiante", out var ud))
        {
            chequeos.Add(new Chequeo("usoDatosEstudiante", uso == ud.GetBoolean(), $"esperado {ud.GetBoolean()}, obtenido {uso?.ToString() ?? "null"}"));
        }

        var noVerif = new List<string>();
        if (esp.TryGetProperty("estudiantesConsultados", out _))
        {
            noVerif.Add("estudiantesConsultados (solo en la auditoria, no en la respuesta HTTP)");
        }

        var manual = esp.TryGetProperty("nota", out _);
        return new Resultado(c, status, intentos, ms, respuesta, citas, uso, tokens, costo, chequeos, noVerif, manual, error);
    }

    private static HashSet<string> LeerCriteriosEval(string dir)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var titulo = new Regex(@"^###\s+((?:AUTH|ACAD|RAG|ADV|RES)-\d{2})\s+·");
        var item = new Regex(@"^\s*-\s+([a-z])\.\s+`\[eval\]`");
        foreach (var f in Directory.EnumerateFiles(dir, "*.md").OrderBy(x => x, StringComparer.Ordinal))
        {
            string? actual = null;
            foreach (var l in File.ReadLines(f))
            {
                var m = titulo.Match(l);
                if (m.Success)
                {
                    actual = m.Groups[1].Value;
                    continue;
                }

                if (Regex.IsMatch(l, @"^#{1,3}\s"))
                {
                    actual = null;
                    continue;
                }

                var i = item.Match(l);
                if (i.Success && actual is not null)
                {
                    set.Add($"{actual}.{i.Groups[1].Value}");
                }
            }
        }

        return set;
    }

    private static List<Caso> LeerDataset(string ruta)
    {
        var casos = new List<Caso>();
        foreach (var linea in File.ReadLines(ruta).Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            var doc = JsonDocument.Parse(linea);
            var r = doc.RootElement.Clone();
            if (r.TryGetProperty("tipo", out var t) && t.GetString() == "meta")
            {
                continue;
            }

            var u = r.GetProperty("usuario");
            casos.Add(new Caso(
                r.GetProperty("id").GetString()!,
                r.TryGetProperty("pregunta", out var p) ? p.GetString() ?? string.Empty : string.Empty,
                u.GetProperty("rol").GetString()!,
                u.GetProperty("id").GetString()!,
                r.TryGetProperty("cuerpo_crudo", out var cc) ? cc.Clone() : null,
                r.GetProperty("criterios").EnumerateArray().Select(x => x.GetString()!).ToList(),
                r.TryGetProperty("esperado", out var e) ? e.Clone() : JsonDocument.Parse("{}").RootElement.Clone()));
        }

        return casos;
    }

    private static string? Git(string raiz, string args)
    {
        try
        {
            var psi = new ProcessStartInfo("git", args) { WorkingDirectory = raiz, RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi)!;
            var o = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return p.ExitCode == 0 && o.Length > 0 ? o : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
