using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TutorFlow.Application.Ports;
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

    private sealed record Eval(
        string Estrategia,
        Caso Caso,
        IReadOnlyList<string> Chunks,
        int? Rango,
        bool Hit,
        IReadOnlyList<string> ProhibidosPresentes)
    {
        public bool Ok => (Caso.Relevantes.Count == 0 || Hit) && ProhibidosPresentes.Count == 0;
    }

    private static readonly string[] EstrategiasDisponibles = ["full-context", "bm25"];

    /// <summary>Estrategia por defecto: RETRIEVAL_STRATEGY del entorno o, si esta vacia, el default de IA (una unica fuente).</summary>
    private static string EstrategiaPorDefecto()
    {
        var v = Environment.GetEnvironmentVariable("RETRIEVAL_STRATEGY");
        return (string.IsNullOrWhiteSpace(v) ? TutorFlow.Infrastructure.Retrieval.IaServiceCollectionExtensions.DefaultStrategy : v)
            .Trim().ToLowerInvariant();
    }

    private static List<Eval> Evaluar(string estrategia, IRetriever retriever, IReadOnlyList<Caso> casos)
    {
        var lista = new List<Eval>();
        foreach (var c in casos)
        {
            var ids = retriever.RecuperarAsync(c.Pregunta, CancellationToken.None).GetAwaiter().GetResult().Select(f => f.ChunkId).ToList();
            int? rango = null;
            for (var i = 0; estrategia != "full-context" && i < ids.Count; i++)
            {
                if (c.Relevantes.Contains(ids[i], StringComparer.Ordinal))
                {
                    rango = i + 1;
                    break;
                }
            }

            lista.Add(new Eval(
                estrategia,
                c,
                ids,
                rango,
                c.Relevantes.Count > 0 && c.Relevantes.All(r => ids.Contains(r, StringComparer.Ordinal)),
                Prohibidos(c, ids)));
        }

        return lista;
    }

    // Metricas de una estrategia con ranking. Un caso es acierto de Recall@4 si TODOS sus relevantes estan en el top 4;
    // el rango del MRR es el del primer relevante (0 si ninguno). Los casos sin relevantes no cuentan.
    private static (double Recall, double Mrr, int Casos) Metricas(IReadOnlyList<Eval> evals)
    {
        var rel = evals.Where(e => e.Caso.Relevantes.Count > 0).ToList();
        if (rel.Count == 0)
        {
            return (0.0, 0.0, 0);
        }

        return (rel.Count(e => e.Hit) / (double)rel.Count, rel.Average(e => e.Rango is int k ? 1.0 / k : 0.0), rel.Count);
    }

    private static int Ejecutar(string raiz, string cuarentena, bool json, string? jsonOut)
    {
        var retIds = LeerCriteriosRet(Path.Combine(raiz, "escenarios"));
        var casos = LeerDataset(Path.Combine(raiz, "evals", "dataset.jsonl"));
        var porDefecto = EstrategiaPorDefecto();
        if (!EstrategiasDisponibles.Contains(porDefecto))
        {
            throw new InvalidOperationException($"RETRIEVAL_STRATEGY '{porDefecto}' no esta disponible en el runner [ret] ({string.Join(" | ", EstrategiasDisponibles)}).");
        }

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
        var retrievers = new Dictionary<string, IRetriever>
        {
            ["full-context"] = new FullContextRetriever(catalog),
            ["bm25"] = new Bm25Retriever(catalog, TopK),
        };

        // Casos con expectativas de recuperacion y al menos un criterio [ret].
        var evaluados = casos
            .Where(c => c.Criterios.Any(retIds.Contains) && (c.Relevantes.Count > 0 || c.Prohibidos.Count > 0))
            .ToList();
        var porEstrategia = EstrategiasDisponibles.ToDictionary(e => e, e => Evaluar(e, retrievers[e], evaluados));
        var gate = porEstrategia[porDefecto];
        var conRanking = porDefecto != "full-context";

        var inv = CultureInfo.InvariantCulture;
        var fallos = new List<string>();
        var sb = new StringBuilder();
        sb.AppendLine($"Runner [ret] determinista (corpus real, cuarentena: {string.Join(", ", q)})");
        sb.AppendLine($"Estrategia por defecto (bloqueante): {porDefecto}  [RETRIEVAL_STRATEGY o default de IA; provisional hasta T-10]");
        sb.AppendLine();

        // --- Tabla bloqueante: estrategia por defecto ---
        sb.AppendLine($"## Estrategia por defecto: {porDefecto}");
        sb.AppendLine();
        TablaCasos(sb, gate, inv);

        var filas = new List<(string Id, string Estado, string Casos)>();
        foreach (var id in retIds.OrderBy(x => x, StringComparer.Ordinal))
        {
            var rs = gate.Where(r => r.Caso.Criterios.Contains(id)).ToList();
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
                fallos.Add($"criterio {id} ({porDefecto}): falla en {string.Join(", ", rs.Where(r => !r.Ok).Select(r => r.Caso.Id))}");
            }

            filas.Add((id, estado, string.Join(",", rs.Select(r => r.Caso.Id))));
        }

        sb.AppendLine();
        sb.AppendLine("| Criterio [ret] | Estado | Casos |");
        sb.AppendLine("|---|---|---|");
        foreach (var f in filas)
        {
            sb.AppendLine($"| {f.Id} | {f.Estado} | {f.Casos} |");
        }

        sb.AppendLine();
        var m = Metricas(gate);
        bool? recallOk = null;
        bool? mrrOk = null;
        if (conRanking)
        {
            recallOk = m.Recall >= UmbralRecall;
            mrrOk = m.Mrr >= UmbralMrr;
            sb.AppendLine($"- {porDefecto}: Recall@4 = {m.Recall.ToString("F4", inv)} (umbral >= {UmbralRecall.ToString("F2", inv)}: {(recallOk == true ? "OK" : "NO CUMPLE")}) sobre {m.Casos} casos");
            sb.AppendLine($"- {porDefecto}: MRR = {m.Mrr.ToString("F4", inv)} (umbral >= {UmbralMrr.ToString("F2", inv)}: {(mrrOk == true ? "OK" : "NO CUMPLE")})");
            if (recallOk != true)
            {
                fallos.Add($"umbral ({porDefecto}): Recall@4 {m.Recall.ToString("F4", inv)} < {UmbralRecall.ToString("F2", inv)}");
            }

            if (mrrOk != true)
            {
                fallos.Add($"umbral ({porDefecto}): MRR {m.Mrr.ToString("F4", inv)} < {UmbralMrr.ToString("F2", inv)}");
            }
        }
        else
        {
            sb.AppendLine($"- {porDefecto}: Recall@4 y MRR no aplican (SPEC 7.6); presencia de relevantes en {gate.Count(e => e.Caso.Relevantes.Count > 0 && e.Hit)}/{m.Casos} casos.");
        }

        // --- Experimento: demas estrategias (informativo, no bloqueante) ---
        var experimento = new Dictionary<string, (double Recall, double Mrr, int Casos, List<Eval> Evals)>();
        foreach (var e in EstrategiasDisponibles.Where(x => x != porDefecto))
        {
            var ev = porEstrategia[e];
            var me = Metricas(ev);
            experimento[e] = (me.Recall, me.Mrr, me.Casos, ev);
            sb.AppendLine();
            sb.AppendLine($"## Experimento (no bloqueante): {e}");
            sb.AppendLine();
            TablaCasos(sb, ev, inv);
            sb.AppendLine();
            if (e == "full-context")
            {
                sb.AppendLine($"- {e}: Recall@4 y MRR no aplican (SPEC 7.6); presencia de relevantes en {ev.Count(x => x.Caso.Relevantes.Count > 0 && x.Hit)}/{me.Casos} casos.");
            }
            else
            {
                sb.AppendLine($"- {e}: Recall@4 = {me.Recall.ToString("F4", inv)} (umbral de referencia {UmbralRecall.ToString("F2", inv)}: {(me.Recall >= UmbralRecall ? "cumple" : "no cumple")}) sobre {me.Casos} casos");
                sb.AppendLine($"- {e}: MRR = {me.Mrr.ToString("F4", inv)} (umbral de referencia {UmbralMrr.ToString("F2", inv)}: {(me.Mrr >= UmbralMrr ? "cumple" : "no cumple")})");
            }

            var malos = ev.Where(x => !x.Ok).ToList();
            if (malos.Count > 0)
            {
                sb.AppendLine($"- {e}: casos que no cumplen (top real recuperado):");
                foreach (var x in malos)
                {
                    sb.AppendLine($"  - {x.Caso.Id} \"{x.Caso.Pregunta}\": esperado [{string.Join("; ", x.Caso.Relevantes)}]; recuperado [{string.Join("; ", x.Chunks)}]");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine(fallos.Count == 0 ? "RESULTADO: OK (estrategia por defecto)" : "RESULTADO: FALLA (estrategia por defecto)");
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
            object CasoJson(Eval r) => new
            {
                id = r.Caso.Id,
                pregunta = r.Caso.Pregunta,
                criterios = r.Caso.Criterios,
                relevantes = r.Caso.Relevantes,
                prohibidos = r.Caso.Prohibidos,
                recuperado = r.Chunks,
                rango = r.Rango,
                hit = r.Hit,
                prohibidosPresentes = r.ProhibidosPresentes,
                ok = r.Ok,
            };
            var doc = new
            {
                modo = "ret",
                commit = Git(raiz, "rev-parse HEAD"),
                fecha = DateTime.UtcNow.ToString("o", inv),
                cuarentena = q,
                estrategiaPorDefecto = porDefecto,
                umbrales = new { recallAt4 = UmbralRecall, mrr = UmbralMrr, aplicanA = conRanking ? porDefecto : "ninguna (full-context: no aplica)" },
                porDefecto = new
                {
                    estrategia = porDefecto,
                    bloqueante = true,
                    recallAt4 = conRanking ? (double?)m.Recall : null,
                    mrr = conRanking ? (double?)m.Mrr : null,
                    casos = m.Casos,
                    criterios = filas.Select(f => new { id = f.Id, estado = f.Estado, casos = f.Casos }),
                    detalle = gate.Select(CasoJson),
                },
                experimento = experimento.Select(kv => new
                {
                    estrategia = kv.Key,
                    bloqueante = false,
                    recallAt4 = kv.Key == "full-context" ? null : (double?)kv.Value.Recall,
                    mrr = kv.Key == "full-context" ? null : (double?)kv.Value.Mrr,
                    casos = kv.Value.Casos,
                    detalle = kv.Value.Evals.Select(CasoJson),
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

    private static void TablaCasos(StringBuilder sb, IReadOnlyList<Eval> evals, CultureInfo inv)
    {
        sb.AppendLine("| Caso | Rango | Relevantes | Prohibidos | Estado |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var r in evals)
        {
            var rel = r.Caso.Relevantes.Count == 0 ? "-" : r.Hit ? "OK" : "FALLA";
            var pr = r.Caso.Prohibidos.Count == 0 ? "-" : r.ProhibidosPresentes.Count == 0 ? "ausentes" : "PRESENTES " + string.Join(";", r.ProhibidosPresentes);
            sb.AppendLine($"| {r.Caso.Id} | {(r.Rango?.ToString(inv) ?? "-")} | {rel} | {pr} | {(r.Ok ? "OK" : "FALLA")} |");
        }
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
