namespace CodeDotNet.Ai.Evaluation;

public sealed record Summary(double Median, double Min, double Max, double P95, int Count);

/// <summary>Deterministic retrieval/answer metrics and run statistics. No model involved.</summary>
public static class Metrics
{
    public static double RecallAtK(IReadOnlyList<string> ranked, IReadOnlySet<string> relevant, int k)
    {
        if (relevant.Count == 0 || k <= 0)
        {
            return 0;
        }

        return (double)ranked.Distinct().Take(k).Count(relevant.Contains) / relevant.Count;
    }

    public static double ReciprocalRank(IReadOnlyList<string> ranked, IReadOnlySet<string> relevant)
    {
        var position = 0;
        foreach (var item in ranked.Distinct())
        {
            position++;
            if (relevant.Contains(item))
            {
                return 1.0 / position;
            }
        }

        return 0;
    }

    /// <summary>Share of emitted citations that point at evidence that was actually supplied.</summary>
    public static double CitationPrecision(int valid, int invalid) =>
        valid + invalid == 0 ? 0 : (double)valid / (valid + invalid);

    public static Summary Summarize(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            throw new ArgumentException("Cannot summarize zero values.", nameof(values));
        }

        var sorted = values.Order().ToArray();
        var n = sorted.Length;
        var median = n % 2 == 1 ? sorted[n / 2] : (sorted[(n / 2) - 1] + sorted[n / 2]) / 2.0;
        var p95 = sorted[Math.Max(0, (int)Math.Ceiling(0.95 * n) - 1)];
        return new Summary(median, sorted[0], sorted[^1], p95, n);
    }
}
