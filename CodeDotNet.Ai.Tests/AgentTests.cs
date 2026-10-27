using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeDotNet.Ai.Abstractions;
using CodeDotNet.Ai.Providers;
using CodeDotNet.Ai.Services;
using CodeDotNet.Ai.Streaming;
using CodeDotNet.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeDotNet.Ai.Tests;

public class AgentTests
{
    private const string Broken = "int x = \"a\";";
    private const string Fixed = "System.Console.WriteLine(1);";

    private static string Fence(string code) => "Fixed:\n```csharp\n" + code + "\n```";

    private sealed class Sandbox(Func<string, CompileAndRunResponse> respond) : ISandboxClient
    {
        public List<string> Sources { get; } = [];

        public async Task<CompileAndRunResponse> CompileAndRunAsync(string source, int timeoutMs, CancellationToken ct)
        {
            Sources.Add(source);
            await Task.Yield();
            return respond(source);
        }
    }

    private static CompileAndRunResponse Run(string status = "ExecutionCompleted", int? exit = 0, string output = "") =>
        new(status, [], output, false, exit, null, null, null, 1, 1, 2);

    private static AgentRunService Service(KbFixture kb, ISandboxClient? sandbox) =>
        new(kb.Provider, new Resources.ResourceGovernor(), kb.Profile, kb.Options, sandbox);

    private static async Task<(List<SseEvent> Events, string Done)> Drain(AgentRun run)
    {
        var events = new List<SseEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await foreach (var e in run.ReadAsync(cts.Token))
        {
            events.Add(e);
        }

        return (events, JsonSerializer.Serialize(events[^1].Data));
    }

    [Fact]
    public async Task BrokenCode_IsFixedAndVerified_InTwoIterations()
    {
        using var kb = new KbFixture(new FakeProvider(_ => Fence(Fixed)));
        var sandbox = new Sandbox(_ => Run());
        var svc = Service(kb, sandbox);

        var run = svc.Start(new AgentRunRequest(Broken))!;
        var (events, done) = await Drain(run);

        Assert.Contains("\"success\":true", done);
        Assert.Contains("\"stopReason\":\"success\"", done);
        Assert.Contains("\"iterations\":2", done);
        Assert.Contains("\"changed\":true", done);
        Assert.Contains(Fixed, done);
        Assert.Contains("CS0029", kb.Provider.Requests[0].Messages[^1].Content);
        Assert.Single(sandbox.Sources);
    }

    [Fact]
    public async Task AlreadyWorkingCode_SucceedsImmediately_WithoutCallingModel()
    {
        using var kb = new KbFixture();
        var svc = Service(kb, new Sandbox(_ => Run()));
        var (_, done) = await Drain(svc.Start(new AgentRunRequest(Fixed))!);
        Assert.Contains("\"success\":true", done);
        Assert.Contains("\"changed\":false", done);
        Assert.Empty(kb.Provider.Requests);
    }

    [Fact]
    public async Task IterationBudget_IsEnforced_AndClampedToProfileMaximum()
    {
        var n = 0;
        using var kb = new KbFixture(new FakeProvider(_ => Fence($"int x = \"v{++n}\";")));
        var svc = Service(kb, new Sandbox(_ => Run()));

        var run = svc.Start(new AgentRunRequest(Broken, MaxIterations: 1000))!;
        var (_, done) = await Drain(run);

        Assert.Contains("max_iterations", done);
        Assert.Contains("\"success\":false", done);
        Assert.Contains($"\"iterations\":{kb.Profile.AgentMaxIterations}", done);
        Assert.True(kb.Provider.Requests.Count <= kb.Profile.AgentMaxIterations - 1);
        Assert.Equal(kb.Profile.AgentMaxIterations, svc.ResolveIterations(1000));
        Assert.Equal(1, svc.ResolveIterations(0));
    }

    [Fact]
    public async Task RepeatedCandidate_StopsRun()
    {
        using var kb = new KbFixture(new FakeProvider(_ => Fence(Broken)));
        var (_, done) = await Drain(Service(kb, new Sandbox(_ => Run())).Start(new AgentRunRequest(Broken))!);
        Assert.Contains("repeated_state", done);
        Assert.Contains("\"success\":false", done);
    }

    [Fact]
    public async Task Stop_CancelsPromptly_AndReportsStopped()
    {
        var gate = new TaskCompletionSource();
        var slow = new Sandbox(_ => Run("ExecutionFailed", 1, "boom"));
        using var kb = new KbFixture(new FakeProvider(_ => { Thread.Sleep(300); return Fence("System.Console.WriteLine(\"" + Guid.NewGuid() + "\"); throw new System.Exception();"); }));
        var svc = Service(kb, slow);

        var run = svc.Start(new AgentRunRequest("System.Console.WriteLine(1); throw new System.Exception();", MaxIterations: 6))!;
        await Task.Delay(150);
        run.Stop();
        var (_, done) = await Drain(run);
        gate.TrySetResult();

        Assert.Contains("\"stopReason\":\"stopped\"", done);
        Assert.Contains("\"success\":false", done);
    }

    [Fact]
    public async Task OnlyOneActiveRun_SecondStartIsRejected_ThenAllowedAfterFinish()
    {
        using var kb = new KbFixture(new FakeProvider(_ => { Thread.Sleep(300); return Fence(Fixed); }));
        var svc = Service(kb, new Sandbox(_ => Run()));

        var first = svc.Start(new AgentRunRequest(Broken))!;
        Assert.Null(svc.Start(new AgentRunRequest(Broken)));
        await Drain(first);
        Assert.NotNull(svc.Start(new AgentRunRequest(Fixed)));
    }

    [Fact]
    public async Task NoSandbox_StopsWithoutGuessing()
    {
        using var kb = new KbFixture();
        var (_, done) = await Drain(Service(kb, null).Start(new AgentRunRequest(Fixed))!);
        Assert.Contains("sandbox_unavailable", done);
        Assert.Contains("\"success\":false", done);
    }

    [Fact]
    public async Task SandboxFailure_StopsRun()
    {
        using var kb = new KbFixture();
        var (_, done) = await Drain(Service(kb, new Sandbox(_ => throw new InvalidOperationException("down"))).Start(new AgentRunRequest(Fixed))!);
        Assert.Contains("sandbox_unavailable", done);
    }

    [Fact]
    public async Task ProviderFailure_StopsRun()
    {
        using var kb = new KbFixture(new FakeProvider(_ => throw new InvalidOperationException("model gone")));
        var (_, done) = await Drain(Service(kb, new Sandbox(_ => Run())).Start(new AgentRunRequest(Broken))!);
        Assert.Contains("provider_error", done);
    }

    [Fact]
    public async Task ReplyWithoutCode_IsIgnored_NotApplied()
    {
        using var kb = new KbFixture(new FakeProvider(_ => "sorry"));
        var (events, done) = await Drain(Service(kb, new Sandbox(_ => Run())).Start(new AgentRunRequest(Broken, MaxIterations: 2))!);
        Assert.Contains("\"changed\":false", done);
        Assert.Contains("max_iterations", done);
        Assert.Contains(events, e => JsonSerializer.Serialize(e.Data).Contains("no code block"));
    }

    [Fact]
    public async Task LateSubscriber_ReplaysFullHistory()
    {
        using var kb = new KbFixture();
        var run = Service(kb, new Sandbox(_ => Run())).Start(new AgentRunRequest(Fixed))!;
        await Drain(run);
        var (events, _) = await Drain(run);
        Assert.Equal("started", events[0].Name);
        Assert.Equal("done", events[^1].Name);
    }

    [Fact]
    public void Validate_EnforcesLimits()
    {
        var p = Resources.Profiles.Low;
        Assert.NotNull(AgentRunService.Validate(new AgentRunRequest(" "), p));
        Assert.NotNull(AgentRunService.Validate(null, p));
        Assert.NotNull(AgentRunService.Validate(new AgentRunRequest("x", Goal: new string('g', 600)), p));
        Assert.NotNull(AgentRunService.Validate(new AgentRunRequest("x", MaxIterations: 0), p));
        Assert.NotNull(AgentRunService.Validate(new AgentRunRequest(new string('x', 100_000)), p));
        Assert.Null(AgentRunService.Validate(new AgentRunRequest("x"), p));
        Assert.Equal(AgentRunService.MaxTimeBudgetSeconds, AgentRunService.ResolveBudgetSeconds(99999));
    }

    [Fact]
    public async Task Api_RequiresAuth_RunsEndToEnd_AndStopsUnknown()
    {
        var sandbox = new Sandbox(_ => Run());
        await using var server = new AiServer(new FakeProvider(_ => Fence(Fixed)), s => s.AddSingleton<ISandboxClient>(sandbox));

        using var anon = server.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/v1/agent/runs", new { source = Broken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/v1/agent/runs/abc/events")).StatusCode);

        using var client = await server.CreatePairedClientAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/v1/agent/runs", new { source = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/v1/agent/runs/nope/events")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/v1/agent/runs/nope/stop", null)).StatusCode);

        var start = await client.PostAsJsonAsync("/v1/agent/runs", new { source = Broken });
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var id = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("runId").GetString();

        var body = await client.GetStringAsync($"/v1/agent/runs/{id}/events");
        Assert.Contains("event: iteration", body);
        Assert.Contains("event: done", body);
        Assert.Contains("\"success\":true", body);
    }
}
