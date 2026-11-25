using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TutorFlow.Application.Retrieval;

// Runner [ret] determinista (SPEC 7.6, AGENTS 6). Sin LLM, sin red, sin llaves.
// Uso: dotnet run --project evals/TutorFlow.Evals -- ret [--json] [--json-out <ruta>] [--raiz <dir>] [--cuarentena a,b]
return RetRunner.Run(args);

internal static class RetRunner
{
    private const double UmbralRecall = 0.95;
    private const double UmbralMrr = 0.85;
    private const int TopK = 4;
    private const string CuarentenaPorDefecto = "bienestar_y_becas.md#Anexo";

    internal static int Run(string[] args)
    {
        string? raiz = null;
        string? jsonOut = null;
        var json = false;
        var cuarentena = CuarentenaPorDefecto;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "ret": break;
                case "--raiz": raiz = args[++i]; break;
                case "--json": json = true; break;
                case "--json-out": jsonOut = args[++i]; json = true; break;
                case "--cuarentena": cuarentena = args[++i]; break;
                default:
                    Console.Error.WriteLine($"Argumento desconocido: {args[i]}");
                    return 2;
            }
        }

        try
        {
            raiz ??= BuscarRaiz();
            return Ejecutar(raiz, cuarentena, json, jsonOut);
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException)
        {
            Console.Error.WriteLine("ERROR del runner [ret]: " + e.Message);
            return 2;
        }
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

    private sealed record Caso(string Id, string Pregunta, IReadOnlyList<string> Criterios, IReadOnlyList<string> Relevantes, IReadOnlyList<string> Prohibidos);

    private sealed record Resultado(
        Caso Caso,
        IReadOnlyList<string> Top4,
        int FullContextChunks,
        int? RangoBm25,
        bool Bm25Hit,
        bool FullHit,
        IReadOnlyList<string> Bm25Prohibidos,
        IReadOnlyList<string> FullProhibidos)
    {
        public bool Ok => (Caso.Relevantes.Count == 0 || (Bm25Hit && FullHit)) && Bm25Prohibidos.Count == 0 && FullProhibidos.Count == 0;
    }

    private static int Ejecutar(string raiz, string cuarentena, bool json, string? jsonOut)
    {
        var retIds = LeerCriteriosRet(Path.Combine(raiz, "escenarios"));
        var casos = LeerDataset(Path.Combine(raiz, "evals", "dataset.jsonl"));

        var docs = Directory.EnumerateFiles(Path.Combine(raiz, "q10", "material", "reglamento"), "*.md")
            .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
            .Select(f => new KnowledgeDocument(Path.GetFileName(f), File.ReadAllText(f)))
            .ToList();
        if (docs.Count == 0)
        {
            throw new InvalidOperationException("Corpus vacio en q10/material/reglamento.");
        }

        var q = cuarentena.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var catalog = new Catalogo(KnowledgeIndexer.Build(docs, q));
        var bm25 = new Bm25Retriever(catalog, TopK);
        var full = new FullContextRetriever(catalog);

        // Se evaluan los casos con expectativas de recuperacion que llevan al menos un criterio [ret].
        var evaluados = casos
            .Where(c => c.Criterios.Any(retIds.Contains) && (c.Relevantes.Count > 0 || c.Prohibidos.Count > 0))
            .ToList();
        var resultados = new List<Resultado>();
        foreach (var c in evaluados)
        {
            var top = bm25.RecuperarAsync(c.Pregunta, CancellationToken.None).GetAwaiter().GetResult().Select(f => f.ChunkId).ToList();
            var fc = full.RecuperarAsync(c.Pregunta, CancellationToken.None).GetAwaiter().GetResult().Select(f => f.ChunkId).ToList();
            int? rango = null;
            for (var i = 0; i < top.Count; i++)
            {
                if (c.Relevantes.Contains(top[i], StringComparer.Ordinal))
                {
                    rango = i + 1;
                    break;
                }
            }

            resultados.Add(new Resultado(
                c,
                top,
                fc.Count,
                rango,
                c.Relevantes.Count > 0 && c.Relevantes.All(r => top.Contains(r, StringComparer.Ordinal)),
                c.Relevantes.Count > 0 && c.Relevantes.All(r => fc.Contains(r, StringComparer.Ordinal)),
                Prohibidos(c, top),
                Prohibidos(c, fc)));
        }

        // Metricas bm25 (solo casos con chunk relevante). Un caso es acierto si TODOS sus relevantes estan en el top 4;
        // el rango del MRR es el del primer relevante.
        var conRel = resultados.Where(r => r.Caso.Relevantes.Count > 0).ToList();
        var recall = conRel.Count == 0 ? 0.0 : conRel.Count(r => r.Bm25Hit) / (double)conRel.Count;
        var mrr = conRel.Count == 0 ? 0.0 : conRel.Average(r => r.RangoBm25 is int k ? 1.0 / k : 0.0);
        var fullPres = conRel.Count == 0 ? 0.0 : conRel.Count(r => r.FullHit) / (double)conRel.Count;
        var conProh = resultados.Where(r => r.Caso.Prohibidos.Count > 0).ToList();
        var bm25Aus = conProh.Count(r => r.Bm25Prohibidos.Count == 0);
        var fullAus = conProh.Count(r => r.FullProhibidos.Count == 0);

        var filas = new List<(string Id, string Estado, string Casos)>();
        var fallos = new List<string>();
        foreach (var id in retIds.OrderBy(x => x, StringComparer.Ordinal))
        {
            var rs = resultados.Where(r => r.Caso.Criterios.Contains(id)).ToList();
            string estado;
            if (rs.Count == 0)
            {
                estado = "SIN CASO";
                fallos.Add($"criterio {id}: sin caso evaluable en el dataset");
            }
            else if (rs.All(r => r.Ok))
            {
                estado = "OK";
            }
            else
            {
                estado = "FALLA";
                fallos.Add($"criterio {id}: falla en {string.Join(", ", rs.Where(r => !r.Ok).Select(r => r.Caso.Id))}");
            }

            filas.Add((id, estado, string.Join(",", rs.Select(r => r.Caso.Id))));
        }

        // Un caso fallido que no pertenece a ningun criterio [ret] listado tampoco puede pasar inadvertido.
        foreach (var r in resultados.Where(r => !r.Ok && !r.Caso.Criterios.Any(retIds.Contains)))
        {
            fallos.Add($"caso {r.Caso.Id}: falla");
        }

        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("Runner [ret] determinista (corpus real, cuarentena: " + string.Join(", ", q) + ")");
        sb.AppendLine();
        sb.AppendLine("| Caso | Rango bm25 | bm25 top-4 | full-context | Prohibidos (bm25/full) | Estado |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var r in resultados)
        {
            var rel = r.Caso.Relevantes.Count == 0 ? "-" : r.Bm25Hit ? "OK" : "FALLA";
            var fc = r.Caso.Relevantes.Count == 0 ? "-" : r.FullHit ? "OK" : "FALLA";
            var pr = r.Caso.Prohibidos.Count == 0
                ? "-"
                : $"{(r.Bm25Prohibidos.Count == 0 ? "ausentes" : "PRESENTES " + string.Join(";", r.Bm25Prohibidos))}/{(r.FullProhibidos.Count == 0 ? "ausentes" : "PRESENTES " + string.Join(";", r.FullProhibidos))}";
            sb.AppendLine($"| {r.Caso.Id} | {(r.RangoBm25?.ToString(inv) ?? "-")} | {rel} | {fc} | {pr} | {(r.Ok ? "OK" : "FALLA")} |");
        }

        sb.AppendLine();
        sb.AppendLine("| Criterio [ret] | Estado | Casos |");
        sb.AppendLine("|---|---|---|");
        foreach (var f in filas)
        {
            sb.AppendLine($"| {f.Id} | {f.Estado} | {f.Casos} |");
        }

        var recallOk = recall >= UmbralRecall;
        var mrrOk = mrr >= UmbralMrr;
        if (!recallOk)
        {
            fallos.Add($"umbral: Recall@4 {recall.ToString("F4", inv)} < {UmbralRecall.ToString("F2", inv)}");
        }

        if (!mrrOk)
        {
            fallos.Add($"umbral: MRR {mrr.ToString("F4", inv)} < {UmbralMrr.ToString("F2", inv)}");
        }

        sb.AppendLine();
        sb.AppendLine("Metricas por estrategia");
        sb.AppendLine($"- bm25: Recall@4 = {recall.ToString("F4", inv)} (umbral >= {UmbralRecall.ToString("F2", inv)}: {(recallOk ? "OK" : "NO CUMPLE")}) sobre {conRel.Count} casos");
        sb.AppendLine($"- bm25: MRR = {mrr.ToString("F4", inv)} (umbral >= {UmbralMrr.ToString("F2", inv)}: {(mrrOk ? "OK" : "NO CUMPLE")})");
        sb.AppendLine($"- bm25: chunks prohibidos ausentes en {bm25Aus}/{conProh.Count} casos");
        sb.AppendLine($"- full-context: chunk relevante presente en {conRel.Count(r => r.FullHit)}/{conRel.Count} casos (presencia {fullPres.ToString("F4", inv)}); prohibidos ausentes en {fullAus}/{conProh.Count} casos");
        sb.AppendLine();
        sb.AppendLine(fallos.Count == 0 ? "RESULTADO: OK" : "RESULTADO: FALLA");
        foreach (var f in fallos)
        {
            sb.AppendLine("  - " + f);
        }

        Console.Write(sb.ToString());

        if (json)
        {
            var sha = Git(raiz, "rev-parse --short HEAD") ?? "sin-git";
            var fecha = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", inv);
            var ruta = jsonOut ?? Path.Combine(raiz, "evals", "runs", $"ret-{fecha}-{sha}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ruta))!);
            var doc = new
            {
                modo = "ret",
                commit = Git(raiz, "rev-parse HEAD"),
                fecha = DateTime.UtcNow.ToString("o", inv),
                cuarentena = q,
                umbrales = new { recallAt4 = UmbralRecall, mrr = UmbralMrr },
                metricas = new
                {
                    bm25 = new { recallAt4 = recall, mrr, casos = conRel.Count, prohibidosAusentes = bm25Aus, casosConProhibidos = conProh.Count },
                    fullContext = new { presencia = fullPres, casos = conRel.Count, prohibidosAusentes = fullAus, casosConProhibidos = conProh.Count },
                },
                criterios = filas.Select(f => new { id = f.Id, estado = f.Estado, casos = f.Casos }),
                casos = resultados.Select(r => new
                {
                    id = r.Caso.Id,
                    pregunta = r.Caso.Pregunta,
                    criterios = r.Caso.Criterios,
                    relevantes = r.Caso.Relevantes,
                    prohibidos = r.Caso.Prohibidos,
                    bm25Top4 = r.Top4,
                    rangoBm25 = r.RangoBm25,
                    fullContextChunks = r.FullContextChunks,
                    bm25Hit = r.Bm25Hit,
                    fullHit = r.FullHit,
                    bm25ProhibidosPresentes = r.Bm25Prohibidos,
                    fullProhibidosPresentes = r.FullProhibidos,
                    ok = r.Ok,
                }),
                resultado = fallos.Count == 0 ? "OK" : "FALLA",
                fallos,
            };
            var opciones = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            File.WriteAllText(ruta, JsonSerializer.Serialize(doc, opciones) + "\n");
            Console.WriteLine("JSON: " + Path.GetRelativePath(raiz, ruta));
        }

        return fallos.Count == 0 ? 0 : 1;
    }

    private static List<string> Prohibidos(Caso c, IReadOnlyList<string> ids) =>
        ids.Where(id => c.Prohibidos.Any(p => p.EndsWith('*') ? id.StartsWith(p[..^1], StringComparison.Ordinal) : id == p)).ToList();

    private static HashSet<string> LeerCriteriosRet(string dir)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var titulo = new Regex(@"^###\s+((?:AUTH|ACAD|RAG|ADV|RES)-\d{2})\s+·");
        var item = new Regex(@"^\s*-\s+([a-z])\.\s+`\[ret\]`");
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
            using var doc = JsonDocument.Parse(linea);
            var r = doc.RootElement;
            if (r.TryGetProperty("tipo", out var t) && t.GetString() == "meta")
            {
                continue;
            }

            var id = r.GetProperty("id").GetString()!;
            var pregunta = r.TryGetProperty("pregunta", out var p) ? p.GetString() ?? string.Empty : string.Empty;
            var crit = r.GetProperty("criterios").EnumerateArray().Select(x => x.GetString()!).ToList();
            var rel = new List<string>();
            var pro = new List<string>();
            if (r.TryGetProperty("esperado", out var e))
            {
                if (e.TryGetProperty("chunks_relevantes", out var a))
                {
                    rel.AddRange(a.EnumerateArray().Select(x => x.GetString()!));
                }

                if (e.TryGetProperty("chunks_prohibidos", out var b))
                {
                    pro.AddRange(b.EnumerateArray().Select(x => x.GetString()!));
                }
            }

            casos.Add(new Caso(id, pregunta, crit, rel, pro));
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

    private sealed class Catalogo(IReadOnlyList<Chunk> chunks) : IChunkCatalog
    {
        public IReadOnlyList<Chunk> Chunks { get; } = chunks;
    }
}
