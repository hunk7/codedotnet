using System.Text.Json.Nodes;
using CodeDotNet.Ai.Evaluation;
using CodeDotNet.Ai.Knowledge;
using Microsoft.Extensions.DependencyInjection;

namespace CodeDotNet.Ai;

/// <summary>Offline maintenance commands: <c>gate</c> (release gate) and <c>eval</c> (retrieval evaluation against live providers).</summary>
public static class Cli
{
    public const string DefaultBenchDir = "docs/ai/benchmarks";

    public static bool IsCommand(string[] args) => args.Length > 0 && args[0] is "gate" or "eval";

    public static async Task<int> RunAsync(string[] args, TextWriter output)
    {
        var dir = Option(args, "--dir") ?? ResolveDefaultBenchDir();
        try
        {
            return args[0] switch
            {
                "gate" => Gate(dir, output),
                "eval" => await EvalAsync(args, dir, output).ConfigureAwait(false),
                _ => 2,
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or HttpRequestException)
        {
            output.WriteLine($"{args[0]} failed: {ex.Message}");
            return 1;
        }
    }

    /// <summary>Walks up from the current and app directories to find the repo root, so the command works from any folder.</summary>
    public static string ResolveDefaultBenchDir()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, DefaultBenchDir);
                if (File.Exists(Path.Combine(candidate, "thresholds.json")))
                {
                    return candidate;
                }
            }
        }

        return DefaultBenchDir;
    }

    public static int Gate(string benchDir, TextWriter output)
    {
        var schema = Path.Combine(benchDir, "result.schema.json");
        var thresholdsPath = Path.Combine(benchDir, "thresholds.json");
        if (!File.Exists(schema) || !File.Exists(thresholdsPath))
        {
            output.WriteLine($"GATE FAIL: missing {schema} or {thresholdsPath}");
            return 1;
        }

        var (results, problems) = ThresholdGate.LoadResults(Path.Combine(benchDir, "results"), schema);
        foreach (var p in problems)
        {
            output.WriteLine("INVALID RESULT: " + p);
        }

        var report = ThresholdGate.Evaluate(JsonNode.Parse(File.ReadAllText(thresholdsPath))!, results);
        foreach (var c in report.Checks)
        {
            output.WriteLine($"{(c.Passed ? "pass" : "FAIL")}  {c.Profile}/{c.Benchmark}/{c.Limit}: {c.Detail}");
        }

        var ok = report.Passed && problems.Count == 0;
        output.WriteLine(ok ? "GATE PASS" : "GATE FAIL");
        return ok ? 0 : 1;
    }

    private static async Task<int> EvalAsync(string[] args, string benchDir, TextWriter output)
    {
        var datasetPath = Option(args, "--dataset") ?? Path.Combine(benchDir, "golden-retrieval.json");
        var dataset = GoldenDataset.Load(datasetPath);
        var k = int.TryParse(Option(args, "--k"), out var kk) && kk > 0 ? kk : 5;

        var dataDir = Path.Combine(Path.GetTempPath(), "codedotnet-eval-" + Guid.NewGuid().ToString("N"));
        var app = AiApp.Build(["--environment=Testing", "--urls=http://127.0.0.1:0", $"--Ai:DataDirectory={dataDir}"]);
        try
        {
            var knowledge = app.Services.GetRequiredService<KnowledgeService>();
            await RetrievalEvaluator.IngestAsync(knowledge, dataset, default).ConfigureAwait(false);
            var report = await RetrievalEvaluator.RunAsync(knowledge, dataset, k, repeats: 3, default).ConfigureAwait(false);

            var latency = Metrics.Summarize(report.LatenciesMs);
            output.WriteLine($"cases={dataset.Cases.Count} documents={dataset.Documents.Count} k={k}");
            output.WriteLine($"recall@{k}={report.MeanRecall:0.000} mrr={report.Mrr:0.000}");
            output.WriteLine($"latency ms median={latency.Median:0.0} p95={latency.P95:0.0}");
            output.WriteLine($"vector search used in {report.Cases.Count(c => c.UsedVectors)}/{report.Cases.Count} queries");
            output.WriteLine("Printed only. Result files are written by a benchmark run that records machine and model digests; see docs/ai/benchmarks.md.");
            return 0;
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(dataDir, true); } catch (IOException) { }
        }
    }

    private static string? Option(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
