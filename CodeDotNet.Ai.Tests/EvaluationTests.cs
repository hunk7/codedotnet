using System.Text.Json.Nodes;
using CodeDotNet.Ai.Evaluation;
using Xunit;

namespace CodeDotNet.Ai.Tests;

internal static class RepoPaths
{
    public static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "codedotnet.slnx")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        }
    }

    public static string Bench => Path.Combine(Root, "docs", "ai", "benchmarks");

    public static string Schema => Path.Combine(Bench, "result.schema.json");
}

public class MetricsTests
{
    [Fact]
    public void RecallAtK_CountsOnlyTopK_AndDeduplicates()
    {
        var relevant = new HashSet<string> { "a", "b" };
        Assert.Equal(0.5, Metrics.RecallAtK(["a", "x", "b"], relevant, 2));
        Assert.Equal(1.0, Metrics.RecallAtK(["a", "a", "b"], relevant, 2));
        Assert.Equal(0, Metrics.RecallAtK(["x"], relevant, 5));
        Assert.Equal(0, Metrics.RecallAtK(["a"], new HashSet<string>(), 5));
    }

    [Fact]
    public void ReciprocalRank_UsesFirstRelevantPosition()
    {
        var relevant = new HashSet<string> { "b" };
        Assert.Equal(0.5, Metrics.ReciprocalRank(["a", "b", "b"], relevant));
        Assert.Equal(0, Metrics.ReciprocalRank(["a"], relevant));
    }

    [Fact]
    public void CitationPrecision_HandlesZero()
    {
        Assert.Equal(0.75, Metrics.CitationPrecision(3, 1));
        Assert.Equal(0, Metrics.CitationPrecision(0, 0));
    }

    [Fact]
    public void Summarize_ComputesMedianMinMaxP95()
    {
        var s = Metrics.Summarize([5, 1, 3, 2, 4]);
        Assert.Equal(3, s.Median);
        Assert.Equal(1, s.Min);
        Assert.Equal(5, s.Max);
        Assert.Equal(5, s.P95);
        Assert.Equal(2.5, Metrics.Summarize([1, 2, 3, 4]).Median);
        Assert.Throws<ArgumentException>(() => Metrics.Summarize([]));
    }
}

public class GoldenDatasetTests
{
    [Fact]
    public void SeedDataset_LoadsAndIsInternallyConsistent()
    {
        var d = GoldenDataset.Load(Path.Combine(RepoPaths.Bench, "golden-retrieval.json"));
        Assert.NotEmpty(d.Documents);
        Assert.NotEmpty(d.Cases);
        Assert.Empty(d.Validate());
    }

    [Fact]
    public void Validate_FlagsUnknownSourcesDuplicatesAndEmptyQueries()
    {
        var d = new GoldenDataset(1, null,
            [new("T", "s1", "text")],
            [new("c1", "q", ["missing"]), new("c1", " ", ["s1"]), new("c2", "q", [])]);
        var problems = string.Join("|", d.Validate());
        Assert.Contains("unknown source 'missing'", problems);
        Assert.Contains("duplicate case id 'c1'", problems);
        Assert.Contains("empty query", problems);
        Assert.Contains("no relevant sources", problems);
    }

    [Fact]
    public async Task RetrievalEvaluator_RunsSeedDataset_WithPerfectLexicalRecall()
    {
        using var kb = new KbFixture();
        var dataset = GoldenDataset.Load(Path.Combine(RepoPaths.Bench, "golden-retrieval.json"));
        var knowledge = kb.Knowledge();
        await RetrievalEvaluator.IngestAsync(knowledge, dataset, default);

        var report = await RetrievalEvaluator.RunAsync(knowledge, dataset, k: 3, repeats: 2, default);

        Assert.Equal(dataset.Cases.Count * 2, report.Cases.Count);
        Assert.True(report.MeanRecall >= 0.8, $"recall was {report.MeanRecall}");
        Assert.True(report.Mrr > 0);
        Assert.All(report.LatenciesMs, l => Assert.True(l >= 0));
    }
}

public class SchemaAndResultsTests
{
    private static JsonObject ValidResult() => BenchmarkResults.Build(
        "BM-07", new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero), "abc1234",
        new MachineInfo("Windows", "cpu", 16, 32_000_000_000),
        new SoftwareInfo("0.0.0", "model", "digest"),
        "low-resource", "off", warm: true, runs: 3,
        new Dictionary<string, MetricSeries> { ["retrievalMs"] = new("ms", Metrics.Summarize([10, 12, 14])) },
        "unit test fixture");

    [Fact]
    public void BuiltResult_ValidatesAgainstRealSchema() =>
        Assert.Empty(BenchmarkResults.ValidateAgainstSchema(ValidResult(), RepoPaths.Schema));

    [Fact]
    public void Schema_RejectsBadResults()
    {
        var bad = ValidResult();
        bad["benchmarkId"] = "BM-99";
        bad["gitCommit"] = "x";
        bad["extra"] = 1;
        bad.Remove("machine");
        var errors = string.Join("|", BenchmarkResults.ValidateAgainstSchema(bad, RepoPaths.Schema));
        Assert.Contains("benchmarkId", errors);
        Assert.Contains("gitCommit", errors);
        Assert.Contains("unexpected property 'extra'", errors);
        Assert.Contains("missing required 'machine'", errors);
    }

    [Fact]
    public void Schema_RejectsEmptyMetricsAndBadGpuMode()
    {
        var bad = ValidResult();
        bad["metrics"] = new JsonObject();
        bad["gpuMode"] = "maybe";
        var errors = string.Join("|", BenchmarkResults.ValidateAgainstSchema(bad, RepoPaths.Schema));
        Assert.Contains("at least 1 properties", errors);
        Assert.Contains("gpuMode", errors);
    }

    [Fact]
    public void Write_RefusesInvalid_AndWritesValidWithConventionalName()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cdn-bench-" + Guid.NewGuid().ToString("N"));
        try
        {
            var bad = ValidResult();
            bad["runs"] = 0;
            Assert.Throws<InvalidDataException>(() => BenchmarkResults.Write(dir, RepoPaths.Schema, bad));
            Assert.False(Directory.Exists(dir));

            var path = BenchmarkResults.Write(dir, RepoPaths.Schema, ValidResult());
            Assert.Equal("2030-01-02-BM-07-low-resource.json", Path.GetFileName(path));
            Assert.Empty(BenchmarkResults.ValidateAgainstSchema(JsonNode.Parse(File.ReadAllText(path))!, RepoPaths.Schema));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}

public class ThresholdGateTests
{
    private static JsonNode Thresholds(string limitJson) => JsonNode.Parse(
        "{\"profiles\":{\"low-resource\":{\"BM-07\":{\"maxRetrievalMs\":" + limitJson + "}}}}")!;

    private static JsonObject Result(double median, string timestamp = "2030-01-01T00:00:00Z") => JsonNode.Parse(
        "{\"benchmarkId\":\"BM-07\",\"profile\":\"low-resource\",\"timestampUtc\":\"" + timestamp + "\",\"metrics\":{\"retrievalMs\":{\"unit\":\"ms\",\"median\":" + median + ",\"min\":1,\"max\":99}}}")!.AsObject();

    [Fact]
    public void NullLimit_Fails() =>
        Assert.False(ThresholdGate.Evaluate(Thresholds("null"), [Result(1)]).Passed);

    [Fact]
    public void MissingResult_Fails() =>
        Assert.False(ThresholdGate.Evaluate(Thresholds("100"), []).Passed);

    [Fact]
    public void WithinLimit_Passes_AndBreach_Fails()
    {
        Assert.True(ThresholdGate.Evaluate(Thresholds("100"), [Result(50)]).Passed);
        Assert.False(ThresholdGate.Evaluate(Thresholds("100"), [Result(150)]).Passed);
    }

    [Fact]
    public void LatestResultWins()
    {
        var report = ThresholdGate.Evaluate(Thresholds("100"), [Result(500, "2030-01-01T00:00:00Z"), Result(50, "2030-06-01T00:00:00Z")]);
        Assert.True(report.Passed);
    }

    [Fact]
    public void MinLimits_AreCheckedInTheOtherDirection()
    {
        var thresholds = JsonNode.Parse("{\"profiles\":{\"low-resource\":{\"BM-04\":{\"minTokensPerSecond\":5}}}}")!;
        JsonObject R(double m) => JsonNode.Parse("{\"benchmarkId\":\"BM-04\",\"profile\":\"low-resource\",\"timestampUtc\":\"2030-01-01T00:00:00Z\",\"metrics\":{\"tokensPerSecond\":{\"median\":" + m + "}}}")!.AsObject();
        Assert.True(ThresholdGate.Evaluate(thresholds, [R(6)]).Passed);
        Assert.False(ThresholdGate.Evaluate(thresholds, [R(4)]).Passed);
    }

    [Fact]
    public void UnknownLimitName_AndMalformedFile_Fail()
    {
        Assert.False(ThresholdGate.Evaluate(JsonNode.Parse("{\"profiles\":{\"p\":{\"BM-01\":{\"bogus\":1}}}}")!, []).Passed);
        Assert.False(ThresholdGate.Evaluate(JsonNode.Parse("{}")!, []).Passed);
        Assert.False(ThresholdGate.Evaluate(JsonNode.Parse("{\"profiles\":{}}")!, []).Passed);
    }

    [Fact]
    public void RealRepositoryThresholds_FailTheGate_BecauseNothingIsMeasuredYet()
    {
        var writer = new StringWriter();
        var exit = Cli.Gate(RepoPaths.Bench, writer);
        Assert.Equal(1, exit);
        Assert.Contains("limit not set (null)", writer.ToString());
        Assert.Contains("GATE FAIL", writer.ToString());
    }

    [Fact]
    public void Gate_FlagsInvalidResultFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cdn-gate-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "results"));
            File.Copy(RepoPaths.Schema, Path.Combine(dir, "result.schema.json"));
            File.WriteAllText(Path.Combine(dir, "thresholds.json"), Thresholds("100").ToJsonString());
            File.WriteAllText(Path.Combine(dir, "results", "bad.json"), "{\"benchmarkId\":\"BM-07\"}");
            var writer = new StringWriter();
            Assert.Equal(1, Cli.Gate(dir, writer));
            Assert.Contains("INVALID RESULT: bad.json", writer.ToString());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Gate_MissingFiles_Fails() =>
        Assert.Equal(1, Cli.Gate(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N")), new StringWriter()));

    [Theory]
    [InlineData("gate", true)]
    [InlineData("eval", true)]
    [InlineData("serve", false)]
    public void Cli_IsCommand(string arg, bool expected) => Assert.Equal(expected, Cli.IsCommand([arg]));

    [Fact]
    public void Cli_IsCommand_EmptyArgs_IsFalse() => Assert.False(Cli.IsCommand([]));
}
