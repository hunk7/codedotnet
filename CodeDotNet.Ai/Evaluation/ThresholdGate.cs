using System.Globalization;
using System.Text.Json.Nodes;

namespace CodeDotNet.Ai.Evaluation;

public sealed record GateCheck(string Profile, string Benchmark, string Limit, bool Passed, string Detail);

public sealed record GateReport(IReadOnlyList<GateCheck> Checks)
{
    /// <summary>An empty check list is a failure: a gate that checked nothing proves nothing.</summary>
    public bool Passed => Checks.Count > 0 && Checks.All(c => c.Passed);
}

/// <summary>
/// Release gate. Fails while any limit in thresholds.json is null, when a benchmark has no result,
/// when a limit is unknown, or when a measured median breaches its limit.
/// </summary>
public static class ThresholdGate
{
    private static readonly Dictionary<string, string> MetricFor = new(StringComparer.Ordinal)
    {
        ["maxLoadSeconds"] = "loadSeconds",
        ["maxPeakRamBytes"] = "peakRamBytes",
        ["maxTtftMs"] = "ttftMs",
        ["minTokensPerSecond"] = "tokensPerSecond",
        ["maxRetrievalMs"] = "retrievalMs",
        ["minAgentSuccessRate"] = "agentSuccessRate",
        ["maxUiLatencyMs"] = "uiLatencyMs",
    };

    public static GateReport Evaluate(JsonNode thresholds, IReadOnlyList<JsonObject> results)
    {
        var checks = new List<GateCheck>();
        var profiles = thresholds["profiles"] as JsonObject;
        if (profiles is null)
        {
            checks.Add(new GateCheck("-", "-", "-", false, "thresholds file has no 'profiles' object"));
            return new GateReport(checks);
        }

        foreach (var (profile, benchmarks) in profiles)
        {
            if (benchmarks is not JsonObject benchmarkObject)
            {
                continue;
            }

            foreach (var (benchmark, limits) in benchmarkObject)
            {
                if (limits is not JsonObject limitObject)
                {
                    continue;
                }

                foreach (var (limit, limitValue) in limitObject)
                {
                    checks.Add(CheckOne(profile, benchmark, limit, limitValue, results));
                }
            }
        }

        return new GateReport(checks);
    }

    public static (List<JsonObject> Results, List<string> Problems) LoadResults(string resultsDirectory, string schemaPath)
    {
        var results = new List<JsonObject>();
        var problems = new List<string>();
        if (!Directory.Exists(resultsDirectory))
        {
            return (results, problems);
        }

        foreach (var file in Directory.EnumerateFiles(resultsDirectory, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                var node = JsonNode.Parse(File.ReadAllText(file));
                var errors = node is null ? ["empty file"] : BenchmarkResults.ValidateAgainstSchema(node, schemaPath);
                if (errors.Count > 0)
                {
                    problems.Add($"{Path.GetFileName(file)}: {string.Join("; ", errors)}");
                }
                else
                {
                    results.Add(node!.AsObject());
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                problems.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        return (results, problems);
    }

    private static GateCheck CheckOne(string profile, string benchmark, string limit, JsonNode? limitValue, IReadOnlyList<JsonObject> results)
    {
        GateCheck Fail(string detail) => new(profile, benchmark, limit, false, detail);

        if (!MetricFor.TryGetValue(limit, out var metric))
        {
            return Fail("unknown limit name");
        }

        if (limitValue is null)
        {
            return Fail("limit not set (null)");
        }

        if (limitValue is not JsonValue lv || !lv.TryGetValue<double>(out var max))
        {
            return Fail("limit is not a number");
        }

        var result = results
            .Where(r => r["benchmarkId"]?.GetValue<string>() == benchmark && r["profile"]?.GetValue<string>() == profile)
            .OrderByDescending(r => DateTimeOffset.Parse(r["timestampUtc"]!.GetValue<string>(), CultureInfo.InvariantCulture))
            .FirstOrDefault();
        if (result is null)
        {
            return Fail("no result file for this profile");
        }

        var median = (result["metrics"] as JsonObject)?[metric]?["median"];
        if (median is null)
        {
            return Fail($"result has no '{metric}' metric");
        }

        var measured = median.GetValue<double>();
        var isMax = limit.StartsWith("max", StringComparison.Ordinal);
        var ok = isMax ? measured <= max : measured >= max;
        var detail = string.Create(CultureInfo.InvariantCulture, $"median {measured} {(isMax ? "<=" : ">=")} {max}: {(ok ? "ok" : "breached")}");
        return new GateCheck(profile, benchmark, limit, ok, detail);
    }
}
