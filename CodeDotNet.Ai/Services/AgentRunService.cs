using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using CodeDotNet.Ai.Abstractions;
using CodeDotNet.Ai.Analysis;
using CodeDotNet.Ai.Configuration;
using CodeDotNet.Ai.Streaming;
using CodeDotNet.Contracts;

namespace CodeDotNet.Ai.Services;

public sealed record AgentRunRequest(string? Source, string? Goal = null, int? MaxIterations = null, int? TimeBudgetSeconds = null);

/// <summary>One agent run: an append-only event log that late subscribers can replay and then follow.</summary>
public sealed class AgentRun(string id)
{
    private readonly object _lock = new();
    private readonly List<SseEvent> _events = [];
    private TaskCompletionSource _signal = NewSignal();
    private CancellationTokenSource? _cts;

    public string Id { get; } = id;

    public bool Finished { get; private set; }

    public bool StopRequested { get; private set; }

    internal void Attach(CancellationTokenSource cts) => _cts = cts;

    public void Stop()
    {
        StopRequested = true;
        try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
    }

    internal void Publish(SseEvent e, bool final = false)
    {
        TaskCompletionSource old;
        lock (_lock)
        {
            _events.Add(e);
            Finished |= final;
            old = _signal;
            _signal = NewSignal();
        }

        old.TrySetResult();
    }

    public async IAsyncEnumerable<SseEvent> ReadAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var next = 0;
        while (true)
        {
            SseEvent? e = null;
            Task wait;
            bool done;
            lock (_lock)
            {
                if (next < _events.Count)
                {
                    e = _events[next++];
                }

                wait = _signal.Task;
                done = Finished && next >= _events.Count;
            }

            if (e is not null)
            {
                yield return e;
                continue;
            }

            if (done)
            {
                yield break;
            }

            await wait.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>
/// Bounded fix-it agent. One model plays Executor/Reviewer/Fixer in a state machine; it can only use three typed tools
/// (analyze, run_in_sandbox, propose_patch). It never writes to the user's files: the final code is returned for the
/// user to apply. Hard limits: iteration count, wall-clock budget, one active run, repeated-state detection, explicit stop.
/// </summary>
public sealed class AgentRunService(
    IChatProvider chat,
    IResourceGovernor governor,
    ResourceProfile profile,
    AiOptions options,
    ISandboxClient? sandbox = null)
{
    public const int DefaultTimeBudgetSeconds = 120;
    public const int MaxTimeBudgetSeconds = 300;
    public const int MaxRetainedRuns = 20;
    public const int RunTimeoutMs = 10_000;
    public const int MaxGoalChars = 500;

    private const string FixerPrompt =
        "You fix C# programs. You are given a numbered program, its compiler diagnostics and/or its real run output. " +
        "Reply with ONE complete corrected Program.cs in a single ```csharp block, then one sentence explaining the change. " +
        "Make the smallest change. Do not add packages. Do not remove functionality to hide an error.";

    private readonly ConcurrentDictionary<string, AgentRun> _runs = new();
    private readonly object _startLock = new();

    public int DefaultIterations => profile.AgentDefaultIterations;

    public int MaxIterations => profile.AgentMaxIterations;

    public static string? Validate(AgentRunRequest? r, ResourceProfile profile)
    {
        if (string.IsNullOrWhiteSpace(r?.Source))
        {
            return "'source' is required.";
        }

        var limit = SourceWindow.MaxSourceChars(profile.NumCtx, profile.NumPredict) / 2;
        if (r.Source.Length > limit)
        {
            return $"'source' exceeds {limit} characters for the active profile.";
        }

        if (r.Goal is { Length: > MaxGoalChars })
        {
            return $"'goal' exceeds {MaxGoalChars} characters.";
        }

        return r.MaxIterations is < 1 || r.TimeBudgetSeconds is < 1 ? "'maxIterations' and 'timeBudgetSeconds' must be positive." : null;
    }

    public int ResolveIterations(int? requested) => Math.Clamp(requested ?? DefaultIterations, 1, MaxIterations);

    public static int ResolveBudgetSeconds(int? requested) => Math.Clamp(requested ?? DefaultTimeBudgetSeconds, 1, MaxTimeBudgetSeconds);

    /// <summary>Starts a run unless one is already active. Returns null when busy.</summary>
    public AgentRun? Start(AgentRunRequest request)
    {
        AgentRun run;
        lock (_startLock)
        {
            if (_runs.Values.Any(r => !r.Finished))
            {
                return null;
            }

            foreach (var old in _runs.Where(kv => kv.Value.Finished).Select(kv => kv.Key).Skip(MaxRetainedRuns - 1).ToList())
            {
                _runs.TryRemove(old, out _);
            }

            run = new AgentRun(Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant());
            _runs[run.Id] = run;
        }

        var iterations = ResolveIterations(request.MaxIterations);
        var budget = TimeSpan.FromSeconds(ResolveBudgetSeconds(request.TimeBudgetSeconds));
        var cts = new CancellationTokenSource(budget);
        run.Attach(cts);
        run.Publish(new SseEvent("started", new { runId = run.Id, maxIterations = iterations, timeBudgetSeconds = (int)budget.TotalSeconds }));
        _ = Task.Run(() => ExecuteAsync(run, request, iterations, cts));
        return run;
    }

    public AgentRun? Get(string id) => _runs.TryGetValue(id, out var r) ? r : null;

    internal async Task ExecuteAsync(AgentRun run, AgentRunRequest request, int maxIterations, CancellationTokenSource cts)
    {
        var ct = cts.Token;
        var code = request.Source!;
        var original = code;
        var seen = new HashSet<string> { Hash(code) };
        var iterations = 0;
        string stopReason;
        var success = false;

        try
        {
            stopReason = "max_iterations";
            for (var i = 1; i <= maxIterations; i++)
            {
                ct.ThrowIfCancellationRequested();
                iterations = i;
                run.Publish(new SseEvent("iteration", new { n = i, of = maxIterations }));

                var analysis = RoslynAnalyzer.Analyze(code);
                var errors = analysis.Diagnostics.Where(d => d.Severity == "Error").Take(10).ToList();
                run.Publish(new SseEvent("tool", new { name = "analyze", compiles = analysis.Compiles, errors = errors.Count, findings = analysis.Findings.Count }));

                CompileAndRunResponse? result = null;
                if (analysis.Compiles)
                {
                    if (sandbox is null)
                    {
                        stopReason = "sandbox_unavailable";
                        break;
                    }

                    try
                    {
                        result = await sandbox.CompileAndRunAsync(code, "", RunTimeoutMs, ct).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or System.Net.WebSockets.WebSocketException or HttpRequestException)
                    {
                        run.Publish(new SseEvent("error", new { code = "sandbox_unavailable", message = ex.Message }));
                        stopReason = "sandbox_unavailable";
                        break;
                    }

                    run.Publish(new SseEvent("tool", new { name = "run_in_sandbox", status = result.Status, exitCode = result.ExitCode, output = Cap(result.Output, 1000), exception = result.ExceptionType }));

                    if (RunSucceeded(result))
                    {
                        success = true;
                        stopReason = "success";
                        break;
                    }
                }

                if (i == maxIterations)
                {
                    break;
                }

                string reply;
                try
                {
                    reply = await AskModelAsync(code, errors, result, request.Goal, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
                {
                    run.Publish(new SseEvent("error", new { code = "provider_error", message = ex.Message }));
                    stopReason = "provider_error";
                    break;
                }

                var patch = TestGenerationService.ExtractCode(reply);
                if (patch is null)
                {
                    run.Publish(new SseEvent("tool", new { name = "propose_patch", accepted = false, reason = "no code block in reply" }));
                    continue;
                }

                if (!seen.Add(Hash(patch)))
                {
                    run.Publish(new SseEvent("tool", new { name = "propose_patch", accepted = false, reason = "repeated an earlier candidate" }));
                    stopReason = "repeated_state";
                    break;
                }

                code = patch;
                run.Publish(new SseEvent("tool", new { name = "propose_patch", accepted = true, chars = patch.Length }));
            }
        }
        catch (OperationCanceledException)
        {
            stopReason = run.StopRequested ? "stopped" : "time_budget";
        }
        catch (Exception ex)
        {
            run.Publish(new SseEvent("error", new { code = "internal_error", message = ex.GetType().Name }));
            stopReason = "internal_error";
        }
        finally
        {
            cts.Dispose();
        }

        run.Publish(new SseEvent("done", new
        {
            outcome = stopReason == "internal_error" ? "error" : "ok",
            stopReason,
            success,
            iterations,
            changed = !string.Equals(code, original, StringComparison.Ordinal),
            finalCode = code,
            note = success ? "Verified by a sandbox run. Review the code and apply it yourself." : "Not verified. Treat finalCode as a suggestion only.",
        }), final: true);
    }

    internal static bool RunSucceeded(CompileAndRunResponse r) => r.Status == "ExecutionCompleted" && r.ExitCode is null or 0;

    private async Task<string> AskModelAsync(string code, List<DiagnosticInfo> errors, CompileAndRunResponse? run, string? goal, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.IsNullOrWhiteSpace(goal) ? "Goal: make the program compile and run without errors." : "Goal: " + goal);
        if (errors.Count > 0)
        {
            sb.AppendLine("Compiler errors:");
            foreach (var d in errors)
            {
                sb.AppendLine($"  line {d.StartLine}: {d.Id}: {d.Message}");
            }
        }

        if (run is not null)
        {
            sb.AppendLine($"Run: status {run.Status}, exit code {run.ExitCode}");
            if (run.ExceptionType is not null)
            {
                sb.AppendLine($"  exception: {run.ExceptionType}: {run.ExceptionMessage}");
            }

            if (run.Output.Length > 0)
            {
                sb.AppendLine("  output: " + Cap(run.Output, 1000));
            }
        }

        sb.AppendLine("```csharp").AppendLine(code).AppendLine("```");

        var request = new ChatRequest(
            [new ChatMessage("system", FixerPrompt), new ChatMessage("user", sb.ToString())],
            new ModelOptions(profile.ChatModel, profile.NumCtx, profile.NumPredict, profile.KeepAlive, options.ResolveNumGpu()));

        var text = new StringBuilder();
        await using var lease = await governor.AcquireAsync(WorkKind.Generation, ct).ConfigureAwait(false);
        await foreach (var token in chat.StreamChatAsync(request, ct).ConfigureAwait(false))
        {
            text.Append(token.Text);
            if (token.Done)
            {
                break;
            }
        }

        return text.ToString();
    }

    private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    private static string Cap(string s, int max) => s.Length <= max ? s : s[..max] + "\n[truncated]";
}
