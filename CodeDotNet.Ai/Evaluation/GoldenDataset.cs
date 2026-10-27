using System.Diagnostics;
using System.Text.Json;
using CodeDotNet.Ai.Knowledge;

namespace CodeDotNet.Ai.Evaluation;

public sealed record GoldenDocument(string Title, string Source, string Text);

public sealed record GoldenCase(string Id, string Query, IReadOnlyList<string> RelevantSources);

/// <summary>Versioned golden retrieval dataset: documents to ingest plus queries with the sources that should be found.</summary>
public sealed record GoldenDataset(int Version, string? Note, IReadOnlyList<GoldenDocument> Documents, IReadOnlyList<GoldenCase> Cases)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static GoldenDataset Load(string path)
    {
        var dataset = JsonSerializer.Deserialize<GoldenDataset>(File.ReadAllText(path), Json)
            ?? throw new InvalidDataException("Golden dataset is empty.");
        var problems = dataset.Validate();
        if (problems.Count > 0)
        {
            throw new InvalidDataException("Golden dataset is invalid: " + string.Join("; ", problems));
        }

        return dataset;
    }

    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        var sources = (Documents ?? []).Select(d => d.Source).ToHashSet(StringComparer.Ordinal);
        if (sources.Count == 0)
        {
            problems.Add("no documents");
        }

        if ((Cases ?? []).Count == 0)
        {
            problems.Add("no cases");
        }

        foreach (var dup in (Cases ?? []).GroupBy(c => c.Id).Where(g => g.Count() > 1))
        {
            problems.Add($"duplicate case id '{dup.Key}'");
        }

        foreach (var c in Cases ?? [])
        {
            if (string.IsNullOrWhiteSpace(c.Query))
            {
                problems.Add($"case '{c.Id}' has an empty query");
            }

            if (c.RelevantSources is null || c.RelevantSources.Count == 0)
            {
                problems.Add($"case '{c.Id}' has no relevant sources");
            }
            else
            {
                problems.AddRange(c.RelevantSources.Where(s => !sources.Contains(s)).Select(s => $"case '{c.Id}' references unknown source '{s}'"));
            }
        }

        return problems;
    }
}

public sealed record CaseResult(string Id, double Recall, double ReciprocalRank, double LatencyMs, bool UsedVectors);

public sealed record RetrievalReport(IReadOnlyList<CaseResult> Cases, int K)
{
    public double MeanRecall => Cases.Count == 0 ? 0 : Cases.Average(c => c.Recall);

    public double Mrr => Cases.Count == 0 ? 0 : Cases.Average(c => c.ReciprocalRank);

    public IReadOnlyList<double> LatenciesMs => Cases.Select(c => c.LatencyMs).ToList();
}

public static class RetrievalEvaluator
{
    public static async Task IngestAsync(KnowledgeService knowledge, GoldenDataset dataset, CancellationToken ct)
    {
        foreach (var d in dataset.Documents)
        {
            await knowledge.IngestAsync(d.Title, d.Source, d.Text, ct).ConfigureAwait(false);
        }
    }

    public static async Task<RetrievalReport> RunAsync(KnowledgeService knowledge, GoldenDataset dataset, int k, int repeats, CancellationToken ct)
    {
        var results = new List<CaseResult>();
        for (var r = 0; r < Math.Max(1, repeats); r++)
        {
            foreach (var c in dataset.Cases)
            {
                var sw = Stopwatch.StartNew();
                var retrieval = await knowledge.RetrieveAsync(c.Query, ct).ConfigureAwait(false);
                sw.Stop();

                var ranked = retrieval.Chunks.Select(x => x.Source).ToList();
                var relevant = c.RelevantSources.ToHashSet(StringComparer.Ordinal);
                results.Add(new CaseResult(c.Id, Metrics.RecallAtK(ranked, relevant, k), Metrics.ReciprocalRank(ranked, relevant), sw.Elapsed.TotalMilliseconds, retrieval.UsedVectors));
            }
        }

        return new RetrievalReport(results, k);
    }
}
