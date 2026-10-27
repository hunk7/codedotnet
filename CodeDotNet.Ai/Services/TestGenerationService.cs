using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using CodeDotNet.Ai.Abstractions;
using CodeDotNet.Ai.Analysis;
using CodeDotNet.Ai.Configuration;
using CodeDotNet.Ai.Streaming;
using CodeDotNet.Contracts;

namespace CodeDotNet.Ai.Services;

/// <summary>
/// Generates a self-contained test harness (the sandbox compiles a single Program.cs, so there is no xUnit),
/// then verifies it: Roslyn compile check, then a real run in the sandbox. Failures are fed back to the model
/// for a bounded number of attempts. Output is only marked <c>verified</c> when it compiled and ran clean.
/// </summary>
public sealed partial class TestGenerationService(
    IChatProvider chat,
    IResourceGovernor governor,
    ResourceProfile profile,
    AiOptions options,
    ISandboxClient? sandbox = null)
{
    public const int MaxAttemptsCap = 3;
    public const int RunTimeoutMs = 10_000;

    private const string SystemPrompt =
        "You write C# test harnesses. Output ONE complete Program.cs in a single ```csharp block and nothing else. " +
        "It must contain the original code under test (unchanged) plus a test runner using top-level statements or Main. " +
        "No external packages or test frameworks. For each test print 'PASS: <name>' or 'FAIL: <name>: <reason>'. " +
        "If any test fails, return exit code 1; otherwise 0. Test only behavior you can see in the code. Do not invent APIs.";

    [GeneratedRegex(@"```(?:csharp|cs|c#)?[ \t]*\r?\n(.*?)(?:```|\z)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex FenceRx();

    public int MaxAttempts => Math.Clamp(profile.AgentDefaultIterations, 1, MaxAttemptsCap);

    internal static string? ExtractCode(string modelOutput)
    {
        var m = FenceRx().Matches(modelOutput);
        return m.Count == 0 ? null : m.OrderByDescending(x => x.Groups[1].Length).First().Groups[1].Value.Trim();
    }

    internal static bool RunPassed(CompileAndRunResponse run) =>
        run.Status == "ExecutionCompleted"
        && run.ExitCode is null or 0
        && !run.Output.Split('\n').Any(l => l.TrimStart().StartsWith("FAIL", StringComparison.Ordinal));

    public async IAsyncEnumerable<SseEvent> GenerateAsync(CodeRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        var source = request.Source!;
        var evidence = RoslynAnalyzer.Analyze(source);

        yield return new SseEvent("evidence", new { compiles = evidence.Compiles, diagnostics = evidence.Diagnostics, findings = evidence.Findings });

        var budget = SourceWindow.MaxSourceChars(profile.NumCtx, profile.NumPredict) / 2;
        if (source.Length > budget)
        {
            yield return new SseEvent("error", new { code = "source_too_large_for_context", message = $"Source ({source.Length} chars) is too large to embed in a test harness within the model context (limit about {budget})." });
            yield return new SseEvent("done", new { outcome = "error", stopReason = "source_too_large", verified = false });
            yield break;
        }

        if (!evidence.Compiles)
        {
            yield return new SseEvent("error", new { code = "source_does_not_compile", message = "The code under test does not compile; fix compile errors before generating tests." });
            yield return new SseEvent("done", new { outcome = "error", stopReason = "source_does_not_compile", verified = false });
            yield break;
        }

        if (sandbox is null)
        {
            yield return new SseEvent("error", new { code = "sandbox_unavailable", message = "Sandbox client is not configured, so tests cannot be verified." });
            yield return new SseEvent("done", new { outcome = "error", stopReason = "sandbox_unavailable", verified = false });
            yield break;
        }

        var messages = new List<ChatMessage>
        {
            new("system", SystemPrompt),
            new("user", BuildPrompt(source, request.Focus)),
        };

        string? lastCode = null;
        string? lastFailure = null;
        var seen = new HashSet<string>();
        var max = MaxAttempts;

        for (var attempt = 1; attempt <= max; attempt++)
        {
            string output = "";
            string? providerError = null;
            try
            {
                output = await GenerateOnceAsync(messages, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
            {
                providerError = ex.Message;
            }

            if (providerError is not null)
            {
                yield return new SseEvent("error", new { code = "provider_error", message = providerError });
                yield return new SseEvent("done", new { outcome = "error", stopReason = "provider_error", verified = false, attempts = attempt });
                yield break;
            }

            var code = ExtractCode(output);
            if (code is null)
            {
                lastFailure = "The reply contained no ```csharp code block.";
                yield return new SseEvent("attempt", new { attempt, extracted = false, passed = false, failure = lastFailure });
                messages.Add(new("assistant", Cap(output)));
                messages.Add(new("user", "Your reply had no ```csharp block. Reply with the complete Program.cs in one ```csharp block."));
                continue;
            }

            lastCode = code;

            if (!seen.Add(code))
            {
                yield return new SseEvent("attempt", new { attempt, extracted = true, passed = false, failure = "Repeated an earlier candidate unchanged; stopping." });
                yield return new SseEvent("done", new { outcome = "ok", stopReason = "repeated_state", verified = false, attempts = attempt });
                yield break;
            }

            var compile = RoslynAnalyzer.Analyze(code);
            if (!compile.Compiles)
            {
                var errors = compile.Diagnostics.Where(d => d.Severity == "Error").Take(10).ToList();
                lastFailure = "Compile errors:\n" + string.Join("\n", errors.Select(d => $"line {d.StartLine}: {d.Id}: {d.Message}"));
                yield return new SseEvent("attempt", new { attempt, extracted = true, compiled = false, passed = false, diagnostics = errors });
                messages.Add(new("assistant", Cap(output)));
                messages.Add(new("user", lastFailure + "\nFix these and reply with the complete corrected Program.cs."));
                continue;
            }

            CompileAndRunResponse? run = null;
            string? sandboxError = null;
            try
            {
                run = await sandbox.CompileAndRunAsync(code, RunTimeoutMs, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Net.WebSockets.WebSocketException or HttpRequestException)
            {
                sandboxError = ex.Message;
            }

            if (run is null)
            {
                yield return new SseEvent("error", new { code = "sandbox_unavailable", message = sandboxError });
                yield return new SseEvent("done", new { outcome = "error", stopReason = "sandbox_unavailable", verified = false, attempts = attempt, code = lastCode });
                yield break;
            }

            var passed = RunPassed(run);
            yield return new SseEvent("attempt", new
            {
                attempt,
                extracted = true,
                compiled = true,
                passed,
                status = run.Status,
                exitCode = run.ExitCode,
                output = Cap(run.Output),
                exception = run.ExceptionType,
            });

            if (passed)
            {
                yield return new SseEvent("result", new { code });
                yield return new SseEvent("done", new { outcome = "ok", stopReason = "verified", verified = true, attempts = attempt });
                yield break;
            }

            lastFailure = $"Run status {run.Status}, exit code {run.ExitCode}.\n{(run.ExceptionType is null ? "" : run.ExceptionType + ": " + run.ExceptionMessage + "\n")}Output:\n{Cap(run.Output)}";
            messages.Add(new("assistant", Cap(output)));
            messages.Add(new("user", lastFailure + "\nSome tests failed or crashed. If a test expectation is wrong, fix the test; do NOT change the code under test to make tests pass. Reply with the complete corrected Program.cs."));
        }

        if (lastCode is not null)
        {
            yield return new SseEvent("result", new { code = lastCode, unverified = true });
        }

        yield return new SseEvent("done", new { outcome = "ok", stopReason = "attempts_exhausted", verified = false, attempts = max, lastFailure });
    }

    private async Task<string> GenerateOnceAsync(List<ChatMessage> messages, CancellationToken ct)
    {
        var request = new ChatRequest(messages.ToList(),
            new ModelOptions(profile.ChatModel, profile.NumCtx, profile.NumPredict, profile.KeepAlive, options.ResolveNumGpu()));
        var sb = new StringBuilder();

        await using var lease = await governor.AcquireAsync(WorkKind.Generation, ct).ConfigureAwait(false);
        await foreach (var token in chat.StreamChatAsync(request, ct).ConfigureAwait(false))
        {
            sb.Append(token.Text);
            if (token.Done)
            {
                break;
            }
        }

        return sb.ToString();
    }

    internal static string BuildPrompt(string source, string? focus)
    {
        var sb = new StringBuilder("Write tests for this program. Include the original code verbatim.\n");
        if (!string.IsNullOrWhiteSpace(focus))
        {
            sb.Append("Focus: ").AppendLine(focus.Length > 1500 ? focus[..1500] : focus);
        }

        sb.AppendLine("```csharp").AppendLine(source).AppendLine("```");
        return sb.ToString();
    }

    private static string Cap(string s) => s.Length <= 2000 ? s : s[..2000] + "\n[truncated]";
}
