using System.Runtime.CompilerServices;
using System.Text;
using CodeDotNet.Ai.Abstractions;
using CodeDotNet.Ai.Analysis;
using CodeDotNet.Ai.Configuration;
using CodeDotNet.Ai.Streaming;
using CodeDotNet.Contracts;

namespace CodeDotNet.Ai.Services;

public sealed record CodeRequest(string? Source, bool? Run = null, string? Focus = null);

/// <summary>
/// Review and debug workflows. The model sees Roslyn evidence (and, for debug, real sandbox
/// output) rather than the source alone, and is told to cite line numbers from that evidence.
/// Responses always carry the evidence as an SSE <c>evidence</c> event so the UI can show it.
/// </summary>
public sealed class CodeWorkflowService(
    IChatProvider chat,
    IResourceGovernor governor,
    ResourceProfile profile,
    AiOptions options,
    TestGenerationService tests,
    ISandboxClient? sandbox = null)
{
    public const int DefaultRunTimeoutMs = 10_000;

    private const string ReviewSystem =
        "You are a senior C# code reviewer. Use ONLY the compiler diagnostics, analyzer findings and numbered source provided. " +
        "Cite line numbers. Do not invent diagnostics or APIs. If the evidence does not support a claim, say you are not sure. " +
        "Be concise: list the most important issues first, each with a one-line fix.";

    private const string DebugSystem =
        "You are a C# debugging assistant. Use ONLY the compiler diagnostics, analyzer findings, program output and numbered source provided. " +
        "State the most likely cause with the line number, then the smallest fix. If the evidence is insufficient, say what you would need. " +
        "Do not claim a fix works; you cannot run code.";

    public IAsyncEnumerable<SseEvent> ReviewAsync(CodeRequest request, CancellationToken ct) =>
        RunAsync(request, debug: false, ct);

    public IAsyncEnumerable<SseEvent> DebugAsync(CodeRequest request, CancellationToken ct) =>
        RunAsync(request, debug: true, ct);

    public IAsyncEnumerable<SseEvent> TestsAsync(CodeRequest request, CancellationToken ct) =>
        tests.GenerateAsync(request, ct);

    internal static string? Validate(CodeRequest? request)
    {
        if (string.IsNullOrWhiteSpace(request?.Source))
        {
            return "'source' is required.";
        }

        return request.Source.Length > RoslynAnalyzer.MaxSourceChars
            ? $"'source' exceeds {RoslynAnalyzer.MaxSourceChars} characters."
            : null;
    }

    private async IAsyncEnumerable<SseEvent> RunAsync(CodeRequest request, bool debug, [EnumeratorCancellation] CancellationToken ct)
    {
        var source = request.Source!;
        var evidence = RoslynAnalyzer.Analyze(source);

        CompileAndRunResponse? run = null;
        string? runNote = null;
        if (debug && evidence.Compiles && request.Run != false)
        {
            if (sandbox is null)
            {
                runNote = "Sandbox client not configured; the program was not run.";
            }
            else
            {
                try
                {
                    run = await sandbox.CompileAndRunAsync(source, DefaultRunTimeoutMs, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.Net.WebSockets.WebSocketException or OperationCanceledException && !ct.IsCancellationRequested)
                {
                    runNote = "Sandbox unavailable; the program was not run (" + ex.Message + ").";
                }
            }
        }

        yield return new SseEvent("evidence", new
        {
            compiles = evidence.Compiles,
            diagnostics = evidence.Diagnostics,
            findings = evidence.Findings,
            run = run is null ? null : new
            {
                status = run.Status,
                output = Truncate(run.Output, 2000),
                exception = run.ExceptionType,
                run.ExceptionMessage,
            },
            runNote,
        });

        var maxChars = SourceWindow.MaxSourceChars(profile.NumCtx, profile.NumPredict);
        var (window, truncated) = SourceWindow.Build(source, evidence.InterestingLines(), maxChars);
        var prompt = BuildPrompt(evidence, window, truncated, run, runNote, request.Focus, debug);

        var chatRequest = new ChatRequest(
            [new ChatMessage("system", debug ? DebugSystem : ReviewSystem), new ChatMessage("user", prompt)],
            new ModelOptions(profile.ChatModel, profile.NumCtx, profile.NumPredict, profile.KeepAlive, options.ResolveNumGpu()));

        await using var lease = await governor.AcquireAsync(WorkKind.Generation, ct).ConfigureAwait(false);

        var outcome = "ok";
        var stopReason = "complete";
        string? error = null;
        var enumerator = chat.StreamChatAsync(chatRequest, ct).GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                ChatToken token;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        break;
                    }

                    token = enumerator.Current;
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
                {
                    outcome = "error";
                    stopReason = "provider_error";
                    error = ex.Message;
                    break;
                }

                if (token.Text.Length > 0)
                {
                    yield return new SseEvent("token", new { text = token.Text });
                }

                if (token.Done)
                {
                    stopReason = token.DoneReason == "length" ? "length" : "complete";
                    break;
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        if (error is not null)
        {
            yield return new SseEvent("error", new { code = "provider_error", message = error });
        }

        yield return new SseEvent("done", new { outcome, stopReason, sourceTruncated = truncated, evidenceBased = true });
    }

    internal static string BuildPrompt(
        CodeEvidence evidence, string window, bool truncated, CompileAndRunResponse? run, string? runNote, string? focus, bool debug)
    {
        var sb = new StringBuilder();
        sb.AppendLine(debug ? "Task: find why this program fails or misbehaves." : "Task: review this program.");
        if (!string.IsNullOrWhiteSpace(focus))
        {
            sb.Append("Focus: ").AppendLine(focus.Length > 1500 ? focus[..1500] : focus);
        }

        sb.AppendLine().AppendLine($"Compiles: {(evidence.Compiles ? "yes" : "no")}");

        sb.AppendLine("Compiler diagnostics:");
        var diags = evidence.Diagnostics.Where(d => d.Severity is "Error" or "Warning").Take(20).ToList();
        if (diags.Count == 0)
        {
            sb.AppendLine("  (none)");
        }

        foreach (var d in diags)
        {
            sb.AppendLine($"  line {d.StartLine}: {d.Severity} {d.Id}: {d.Message}");
        }

        sb.AppendLine("Analyzer findings (syntax-only leads, not proof):");
        if (evidence.Findings.Count == 0)
        {
            sb.AppendLine("  (none)");
        }

        foreach (var f in evidence.Findings.Take(20))
        {
            sb.AppendLine($"  line {f.Line}: {f.RuleId}: {f.Message}");
        }

        if (run is not null)
        {
            sb.AppendLine($"Program run: status {run.Status}");
            if (!string.IsNullOrEmpty(run.ExceptionType))
            {
                sb.AppendLine($"  exception: {run.ExceptionType}: {run.ExceptionMessage}");
            }

            if (!string.IsNullOrEmpty(run.Output))
            {
                sb.AppendLine("  output:").AppendLine(Indent(Truncate(run.Output, 2000)));
            }
        }
        else if (runNote is not null)
        {
            sb.AppendLine($"Program run: not available. {runNote}");
        }

        sb.AppendLine().AppendLine(truncated
            ? "Source (numbered; TRUNCATED to fit the context window, so omitted regions are unseen):"
            : "Source (numbered):");
        sb.AppendLine("```csharp").AppendLine(window).AppendLine("```");
        return sb.ToString();
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "\n[truncated]";

    private static string Indent(string s) => "    " + s.Replace("\n", "\n    ");
}
