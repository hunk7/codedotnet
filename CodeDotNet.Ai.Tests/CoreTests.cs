using CodeDotNet.Ai.Abstractions;
using CodeDotNet.Ai.Configuration;
using CodeDotNet.Ai.Providers;
using CodeDotNet.Ai.Resources;
using Xunit;

namespace CodeDotNet.Ai.Tests;

public class ProfileTests
{
    private static HardwareInfo Hw(long ramGb, int cores) =>
        new("test-os", cores, ramGb * 1024 * 1024 * 1024, 100L * 1024 * 1024 * 1024, null);

    [Fact]
    public void Resolve_SmallMachine_PicksLowResource()
    {
        var p = new ProfileResolver().Resolve(Hw(8, 4), new ProfileOverrides());
        Assert.Equal(Profiles.LowResource, p.Name);
        Assert.Equal(1, p.SandboxPoolSize);
        Assert.False(p.LlmRerankerEnabled);
    }

    [Fact]
    public void Resolve_LargeMachine_PicksRecommended()
    {
        var p = new ProfileResolver().Resolve(Hw(32, 16), new ProfileOverrides());
        Assert.Equal(Profiles.Recommended, p.Name);
        Assert.Equal(5, p.FinalChunks);
    }

    [Fact]
    public void Resolve_UserOverrideWins()
    {
        var p = new ProfileResolver().Resolve(Hw(64, 32), new ProfileOverrides("low-resource"));
        Assert.Equal(Profiles.LowResource, p.Name);
    }

    [Fact]
    public void Resolve_UnknownOverride_FallsBackToHardware()
    {
        var p = new ProfileResolver().Resolve(Hw(8, 4), new ProfileOverrides("bogus"));
        Assert.Equal(Profiles.LowResource, p.Name);
    }

    [Fact]
    public void Profiles_RespectDocumentedLimits()
    {
        foreach (var p in new[] { Profiles.Low, Profiles.Standard })
        {
            Assert.True(p.AgentDefaultIterations <= p.AgentMaxIterations);
            Assert.True(p.NumCtx >= 4096);
            Assert.True(p.PauseEmbeddingDuringGeneration);
        }
    }

    [Fact]
    public void AiOptions_ApplyOverrides_ReplacesModelsOnly()
    {
        var o = new AiOptions { ChatModel = "custom:3b" };
        var p = o.ApplyOverrides(Profiles.Low);
        Assert.Equal("custom:3b", p.ChatModel);
        Assert.Equal(Profiles.Low.EmbeddingModel, p.EmbeddingModel);
    }

    [Theory]
    [InlineData(GpuMode.Off, 0)]
    [InlineData(GpuMode.Auto, 0)]
    public void AiOptions_NumGpu_IsCpuUnlessExplicitlyOn(GpuMode mode, int expected)
    {
        Assert.Equal(expected, new AiOptions { GpuMode = mode }.ResolveNumGpu());
    }

    [Fact]
    public void AiOptions_GpuOn_OmitsNumGpu()
    {
        Assert.Null(new AiOptions { GpuMode = GpuMode.On }.ResolveNumGpu());
    }

    [Fact]
    public async Task HardwareProbe_ReportsRuntimeFacts()
    {
        var hw = await new HardwareProbe().ProbeAsync();
        Assert.True(hw.LogicalCores > 0);
        Assert.True(hw.TotalRamBytes > 0);
        Assert.False(string.IsNullOrWhiteSpace(hw.OsDescription));
    }
}

public class ResourceGovernorTests
{
    [Fact]
    public async Task Governor_SerializesWork()
    {
        var g = new ResourceGovernor();
        var first = await g.AcquireAsync(WorkKind.Generation);
        var second = g.AcquireAsync(WorkKind.Embedding).AsTask();

        await Task.Delay(50);
        Assert.False(second.IsCompleted);

        await first.DisposeAsync();
        var lease = await second.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, g.ActiveCount);
        await lease.DisposeAsync();
        Assert.Equal(0, g.ActiveCount);
    }

    [Fact]
    public async Task Governor_DoubleDispose_ReleasesOnce()
    {
        var g = new ResourceGovernor();
        var lease = await g.AcquireAsync(WorkKind.Generation);
        await lease.DisposeAsync();
        await lease.DisposeAsync();
        Assert.Equal(0, g.ActiveCount);
        await (await g.AcquireAsync(WorkKind.Generation)).DisposeAsync();
    }

    [Fact]
    public async Task Governor_CancelledWaitDoesNotLeak()
    {
        var g = new ResourceGovernor();
        var held = await g.AcquireAsync(WorkKind.Generation);
        using var cts = new CancellationTokenSource(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await g.AcquireAsync(WorkKind.Embedding, cts.Token));
        await held.DisposeAsync();
        await (await g.AcquireAsync(WorkKind.Generation)).DisposeAsync();
    }
}

public class OllamaProviderTests
{
    [Fact]
    public void BuildOptions_AlwaysSetsExplicitLimits()
    {
        var o = OllamaProvider.BuildOptions(new ModelOptions("m", 8192, 512, "2m", 0));
        Assert.Equal(8192, o["num_ctx"]);
        Assert.Equal(512, o["num_predict"]);
        Assert.Equal(0, o["num_gpu"]);
    }

    [Fact]
    public void BuildOptions_OmitsNumGpuWhenUndecided()
    {
        var o = OllamaProvider.BuildOptions(new ModelOptions("m", 8192, 512, "2m", null));
        Assert.False(o.ContainsKey("num_gpu"));
    }

    [Fact]
    public void ParseChatLine_ReadsContentAndDone()
    {
        var t = OllamaProvider.ParseChatLine("""{"message":{"role":"assistant","content":"hi"},"done":false}""");
        Assert.Equal("hi", t.Text);
        Assert.False(t.Done);

        var last = OllamaProvider.ParseChatLine("""{"message":{"content":""},"done":true,"done_reason":"stop"}""");
        Assert.True(last.Done);
        Assert.Equal("stop", last.DoneReason);
    }

    [Fact]
    public void ParseChatLine_ErrorThrows()
    {
        Assert.Throws<InvalidOperationException>(() => OllamaProvider.ParseChatLine("""{"error":"model not found"}"""));
    }

    [Fact]
    public void ParseEmbeddings_ReadsVectors()
    {
        var e = OllamaProvider.ParseEmbeddings("""{"embeddings":[[0.1,0.2],[0.3,0.4]]}""");
        Assert.Equal(2, e.Count);
        Assert.Equal(0.3f, e[1][0]);
    }

    [Fact]
    public void ParseEmbeddings_MissingArrayThrows()
    {
        Assert.Throws<InvalidOperationException>(() => OllamaProvider.ParseEmbeddings("""{"x":1}"""));
    }

    [Fact]
    public async Task StreamChat_ParsesNdjsonFromHttp()
    {
        var ndjson = "{\"message\":{\"content\":\"a\"},\"done\":false}\n{\"message\":{\"content\":\"b\"},\"done\":false}\n{\"message\":{\"content\":\"\"},\"done\":true,\"done_reason\":\"stop\"}\n";
        var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(ndjson),
        })) { BaseAddress = new Uri("http://localhost:11434/") };

        var provider = new OllamaProvider(http);
        var text = "";
        await foreach (var t in provider.StreamChatAsync(new ChatRequest([new ChatMessage("user", "x")], new ModelOptions("m", 8192, 64, "1m", 0))))
        {
            text += t.Text;
        }

        Assert.Equal("ab", text);
    }

    [Fact]
    public async Task Capabilities_UnreachableServer_ReportsNotReachable()
    {
        var http = new HttpClient(new StubHandler(_ => throw new HttpRequestException("connection refused")))
        {
            BaseAddress = new Uri("http://localhost:1/"),
        };
        var caps = await new OllamaProvider(http).GetCapabilitiesAsync();
        Assert.False(caps.Reachable);
        Assert.Contains("refused", caps.Error);
    }

    [Fact]
    public async Task Capabilities_ReadsVersionAndModels()
    {
        var http = new HttpClient(new StubHandler(req => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(req.RequestUri!.AbsolutePath.EndsWith("version")
                ? """{"version":"0.0.0-test"}"""
                : """{"models":[{"name":"m:1b","digest":"abc","size":42}]}"""),
        })) { BaseAddress = new Uri("http://localhost:11434/") };

        var caps = await new OllamaProvider(http).GetCapabilitiesAsync();
        Assert.True(caps.Reachable);
        Assert.Equal("0.0.0-test", caps.Version);
        Assert.Equal("m:1b", Assert.Single(caps.Models).Name);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}

public class FakeProviderTests
{
    [Fact]
    public void Embed_IsDeterministicAndNormalized()
    {
        var a = FakeProvider.Embed("hello world");
        var b = FakeProvider.Embed("hello world");
        Assert.Equal(a, b);
        Assert.Equal(1.0, Math.Sqrt(a.Sum(x => x * x)), 3);
    }

    [Fact]
    public void Embed_SimilarTextIsCloserThanUnrelated()
    {
        static double Dot(float[] x, float[] y) => x.Zip(y, (p, q) => (double)p * q).Sum();
        var q = FakeProvider.Embed("sort a list of integers");
        Assert.True(Dot(q, FakeProvider.Embed("sort integers in a list")) > Dot(q, FakeProvider.Embed("open tcp socket connection")));
    }

    [Fact]
    public async Task StreamChat_EmitsTokensThenDone()
    {
        var p = new FakeProvider(_ => "one two");
        var tokens = new List<ChatToken>();
        await foreach (var t in p.StreamChatAsync(new ChatRequest([new ChatMessage("user", "x")], new ModelOptions("m", 1, 1, "1m", 0))))
        {
            tokens.Add(t);
        }

        Assert.True(tokens[^1].Done);
        Assert.Equal("one two ", string.Concat(tokens.Select(t => t.Text)));
        Assert.Single(p.Requests);
    }
}
