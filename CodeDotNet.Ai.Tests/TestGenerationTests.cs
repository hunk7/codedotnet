using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeDotNet.Ai.Abstractions;
using CodeDotNet.Ai.Services;
using CodeDotNet.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeDotNet.Ai.Tests;

public class TestGenerationTests
{
    private const string Code = "int Add(int a, int b) => a + b;\nSystem.Console.WriteLine(Add(1, 2));";

    private const string GoodHarness = "int Add(int a, int b) => a + b;\nSystem.Console.WriteLine(Add(1, 2) == 3 ? \"PASS: add\" : \"FAIL: add\");";

    private static string Fence(string code) => "Here you go:\n```csharp\n" + code + "\n```";

    private sealed class ScriptedSandbox(Func<string, CompileAndRunResponse> respond) : ISandboxClient
    {
        public List<string> Sources { get; } = [];

        public Task<CompileAndRunResponse> CompileAndRunAsync(string source, string stdin, int timeoutMs, CancellationToken ct)
        {
            Sources.Add(source);
            return Task.FromResult(respond(source));
        }
    }

    private static CompileAndRunResponse Run(string status, string output, int? exit = 0, string? ex = null) =>
        new(status, [], output, false, exit, ex, null, null, 1, 1, 2);

    private static async Task<List<(string Name, string Json)>> Collect(Func<KbFixture, IAsyncEnumerable<CodeDotNet.Ai.Streaming.SseEvent>> start, KbFixture kb)
    {
        var list = new List<(string, string)>();
        await foreach (var e in start(kb))
        {
            list.Add((e.Name, JsonSerializer.Serialize(e.Data)));
        }

        return list;
    }

    private static TestGenerationService Service(KbFixture kb, ISandboxClient? sandbox) =>
        new(kb.Provider, new Resources.ResourceGovernor(), kb.Profile, kb.Options, sandbox);

    [Fact]
    public async Task ValidHarness_RunsClean_IsVerified()
    {
        using var kb = new KbFixture(new Providers.FakeProvider(_ => Fence(GoodHarness)));
        var sandbox = new ScriptedSandbox(_ => Run("ExecutionCompleted", "PASS: add\n"));
        var events = await Collect(k => Service(k, sandbox).GenerateAsync(new CodeRequest(Code), default), kb);

        Assert.Contains("\"verified\":true", events[^1].Json);
        Assert.Contains(events, e => e.Name == "result");
        Assert.Single(sandbox.Sources);
    }

    [Fact]
    public async Task CompileError_IsFedBack_AndNextAttemptSucceeds()
    {
        var replies = new Queue<string>([Fence("int x = \"a\";"), Fence(GoodHarness)]);
        using var kb = new KbFixture(new Providers.FakeProvider(_ => replies.Dequeue()));
        var sandbox = new ScriptedSandbox(_ => Run("ExecutionCompleted", "PASS: add\n"));
        var events = await Collect(k => Service(k, sandbox).GenerateAsync(new CodeRequest(Code), default), kb);

        Assert.Contains("\"verified\":true", events[^1].Json);
        Assert.Contains("\"attempts\":2", events[^1].Json);
        Assert.Single(sandbox.Sources);
        Assert.Contains("CS0029", kb.Provider.Requests[1].Messages[^1].Content);
    }

    [Fact]
    public async Task FailingRun_IsFedBack_ThenFixed()
    {
        var replies = new Queue<string>([Fence(GoodHarness + "\n// v1"), Fence(GoodHarness + "\n// v2")]);
        using var kb = new KbFixture(new Providers.FakeProvider(_ => replies.Dequeue()));
        var sandbox = new ScriptedSandbox(src => src.Contains("v1")
            ? Run("ExecutionCompleted", "FAIL: add: expected 4\n", 1)
            : Run("ExecutionCompleted", "PASS: add\n"));
        var events = await Collect(k => Service(k, sandbox).GenerateAsync(new CodeRequest(Code), default), kb);

        Assert.Contains("\"verified\":true", events[^1].Json);
        Assert.Contains("FAIL: add", kb.Provider.Requests[1].Messages[^1].Content);
        Assert.Contains("do NOT change the code under test", kb.Provider.Requests[1].Messages[^1].Content);
    }

    [Fact]
    public async Task AlwaysFailing_StopsAtBudget_AndIsNotVerified()
    {
        var n = 0;
        using var kb = new KbFixture(new Providers.FakeProvider(_ => Fence(GoodHarness + "\n// attempt " + ++n)));
        var sandbox = new ScriptedSandbox(_ => Run("ExecutionCompleted", "FAIL: add\n", 1));
        var svc = Service(kb, sandbox);
        var events = await Collect(k => svc.GenerateAsync(new CodeRequest(Code), default), kb);

        Assert.Contains("attempts_exhausted", events[^1].Json);
        Assert.Contains("\"verified\":false", events[^1].Json);
        Assert.Equal(svc.MaxAttempts, sandbox.Sources.Count);
        Assert.Contains("\"unverified\":true", events.Single(e => e.Name == "result").Json);
    }

    [Fact]
    public async Task RepeatedCandidate_StopsEarly()
    {
        using var kb = new KbFixture(new Providers.FakeProvider(_ => Fence(GoodHarness)));
        var sandbox = new ScriptedSandbox(_ => Run("ExecutionCompleted", "FAIL: add\n", 1));
        var events = await Collect(k => Service(k, sandbox).GenerateAsync(new CodeRequest(Code), default), kb);

        Assert.Contains("repeated_state", events[^1].Json);
        Assert.Single(sandbox.Sources);
    }

    [Fact]
    public async Task ReplyWithoutCodeBlock_IsRetried_ThenGivesUp()
    {
        using var kb = new KbFixture(new Providers.FakeProvider(_ => "I cannot do that."));
        var sandbox = new ScriptedSandbox(_ => Run("ExecutionCompleted", ""));
        var events = await Collect(k => Service(k, sandbox).GenerateAsync(new CodeRequest(Code), default), kb);

        Assert.Empty(sandbox.Sources);
        Assert.Contains("\"verified\":false", events[^1].Json);
        Assert.DoesNotContain(events, e => e.Name == "result");
    }

    [Fact]
    public async Task SourceThatDoesNotCompile_IsRejected_WithoutCallingModelOrSandbox()
    {
        using var kb = new KbFixture();
        var sandbox = new ScriptedSandbox(_ => Run("ExecutionCompleted", ""));
        var events = await Collect(k => Service(k, sandbox).GenerateAsync(new CodeRequest("int x = \"a\";"), default), kb);

        Assert.Empty(kb.Provider.Requests);
        Assert.Empty(sandbox.Sources);
        Assert.Contains("source_does_not_compile", events[^1].Json);
    }

    [Fact]
    public async Task NoSandbox_CannotVerify_AndDoesNotCallModel()
    {
        using var kb = new KbFixture();
        var events = await Collect(k => Service(k, null).GenerateAsync(new CodeRequest(Code), default), kb);
        Assert.Empty(kb.Provider.Requests);
        Assert.Contains("sandbox_unavailable", events[^1].Json);
    }

    [Fact]
    public async Task SandboxFailure_IsReported_NotVerified()
    {
        using var kb = new KbFixture(new Providers.FakeProvider(_ => Fence(GoodHarness)));
        var sandbox = new ScriptedSandbox(_ => throw new InvalidOperationException("sandbox offline"));
        var events = await Collect(k => Service(k, sandbox).GenerateAsync(new CodeRequest(Code), default), kb);

        Assert.Contains("sandbox_unavailable", events[^1].Json);
        Assert.Contains("\"verified\":false", events[^1].Json);
    }

    [Fact]
    public async Task ProviderFailure_IsReported()
    {
        using var kb = new KbFixture(new Providers.FakeProvider(_ => throw new InvalidOperationException("model gone")));
        var sandbox = new ScriptedSandbox(_ => Run("ExecutionCompleted", ""));
        var events = await Collect(k => Service(k, sandbox).GenerateAsync(new CodeRequest(Code), default), kb);
        Assert.Contains("provider_error", events[^1].Json);
    }

    [Fact]
    public async Task OversizedSource_IsRejectedBeforeModel()
    {
        using var kb = new KbFixture();
        var sandbox = new ScriptedSandbox(_ => Run("ExecutionCompleted", ""));
        var big = "int x = 0;\n" + string.Join("\n", Enumerable.Range(0, 3000).Select(i => $"x += {i};"));
        var events = await Collect(k => Service(k, sandbox).GenerateAsync(new CodeRequest(big), default), kb);
        Assert.Empty(kb.Provider.Requests);
        Assert.Contains("source_too_large", events[^1].Json);
    }

    [Theory]
    [InlineData("ExecutionCompleted", "PASS: a\n", 0, true)]
    [InlineData("ExecutionCompleted", "PASS: a\nFAIL: b\n", 0, false)]
    [InlineData("ExecutionCompleted", "PASS: a\n", 1, false)]
    [InlineData("ExecutionFailed", "PASS: a\n", 0, false)]
    [InlineData("TimedOut", "", null, false)]
    public void RunPassed_RequiresCleanCompletion(string status, string output, int? exit, bool expected) =>
        Assert.Equal(expected, TestGenerationService.RunPassed(Run(status, output, exit)));

    [Fact]
    public void ExtractCode_PicksLargestFence_AndHandlesAliases()
    {
        Assert.Equal("class Big {}", TestGenerationService.ExtractCode("```cs\nx\n```\ntext\n```csharp\nclass Big {}\n```"));
        Assert.Null(TestGenerationService.ExtractCode("no code here"));
    }

    [Fact]
    public async Task TestsEndpoint_RequiresAuth_ValidatesInput_AndStreams()
    {
        var sandbox = new ScriptedSandbox(_ => Run("ExecutionCompleted", "PASS: add\n"));
        await using var server = new AiServer(new Providers.FakeProvider(_ => Fence(GoodHarness)), s => s.AddSingleton<ISandboxClient>(sandbox));

        using var anonymous = server.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/v1/tests", new { source = Code })).StatusCode);

        using var client = await server.CreatePairedClientAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/v1/tests", new { source = "" })).StatusCode);

        var body = await (await client.PostAsJsonAsync("/v1/tests", new { source = Code })).Content.ReadAsStringAsync();
        Assert.Contains("event: result", body);
        Assert.Contains("\"verified\":true", body);
    }
}
