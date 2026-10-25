using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeDotNet.Ai.Abstractions;
using CodeDotNet.Ai.Analysis;
using CodeDotNet.Ai.Configuration;
using CodeDotNet.Ai.Providers;
using CodeDotNet.Ai.Resources;
using CodeDotNet.Ai.Services;
using CodeDotNet.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeDotNet.Ai.Tests;

public class RoslynAnalyzerTests
{
    private static IEnumerable<string> Rules(string source) =>
        RoslynAnalyzer.Analyze(source).Findings.Select(f => f.RuleId);

    [Fact]
    public void ValidProgram_Compiles_WithNoFindings()
    {
        var e = RoslynAnalyzer.Analyze("using System;\nConsole.WriteLine(\"hi\");");
        Assert.True(e.Compiles);
        Assert.Empty(e.Findings);
        Assert.DoesNotContain(e.Diagnostics, d => d.Severity == "Error");
    }

    [Fact]
    public void CompileError_IsReportedWithLine()
    {
        var e = RoslynAnalyzer.Analyze("int x = \"text\";");
        Assert.False(e.Compiles);
        var error = Assert.Single(e.Diagnostics, d => d.Severity == "Error");
        Assert.Equal("CS0029", error.Id);
        Assert.Equal(1, error.StartLine);
    }

    [Fact]
    public void Diagnostics_MatchEngineCompilerHost()
    {
        const string source = "class P { static void Main() { int unused; undefined(); } }";
        var viaAnalyzer = RoslynAnalyzer.Analyze(source).Diagnostics;
        var viaEngine = CodeDotNet.Engine.CompilerHost.Compile(source).Diagnostics;
        Assert.Equal(viaEngine, viaAnalyzer);
    }

    [Fact]
    public void AsyncVoid_IsFlagged()
    {
        const string src = "using System.Threading.Tasks;\nclass P {\n  static async void Run() { await Task.Delay(1); }\n  static void Main() {}\n}";
        var f = Assert.Single(RoslynAnalyzer.Analyze(src).Findings, x => x.RuleId == "CDN001");
        Assert.Equal(3, f.Line);
    }

    [Fact]
    public void AsyncVoid_EventHandlerShape_IsNotFlagged()
    {
        const string src = "using System;\nclass P {\n  static async void OnClick(object sender, EventArgs e) { await System.Threading.Tasks.Task.Delay(1); }\n  static void Main() {}\n}";
        Assert.DoesNotContain("CDN001", Rules(src));
    }

    [Fact]
    public void AsyncTask_IsNotFlagged()
    {
        const string src = "using System.Threading.Tasks;\nclass P {\n  static async Task Run() { await Task.Delay(1); }\n  static void Main() {}\n}";
        Assert.DoesNotContain("CDN001", Rules(src));
    }

    [Fact]
    public void EmptyCatch_IsFlagged_NonEmptyIsNot()
    {
        Assert.Contains("CDN003", Rules("try { int.Parse(\"x\"); } catch { }"));
        Assert.DoesNotContain("CDN003", Rules("try { int.Parse(\"x\"); } catch (System.Exception e) { System.Console.WriteLine(e.Message); }"));
    }

    [Theory]
    [InlineData("while (true) { }")]
    [InlineData("for (;;) { }")]
    [InlineData("do { } while (true);")]
    public void InfiniteLoopsWithoutExit_AreFlagged(string source) =>
        Assert.Contains("CDN004", Rules(source));

    [Theory]
    [InlineData("while (true) { if (System.DateTime.Now.Year > 0) break; }")]
    [InlineData("while (true) { return; }")]
    [InlineData("int i = 0; while (i < 3) { i++; }")]
    public void LoopsWithExitOrCondition_AreNotFlagged(string source) =>
        Assert.DoesNotContain("CDN004", Rules(source));

    [Fact]
    public void BreakInsideLambda_DoesNotCountAsLoopExit()
    {
        const string src = "using System;\nwhile (true) { Action a = () => { for (;;) { break; } }; a(); }";
        Assert.Contains("CDN004", Rules(src));
    }

    [Fact]
    public void BlockingWait_IsFlagged_AsInfoLead()
    {
        const string src = "using System.Threading.Tasks;\nvar t = Task.FromResult(1);\nvar v = t.Result;\nt.Wait();";
        var rules = RoslynAnalyzer.Analyze(src).Findings.Where(f => f.RuleId == "CDN002").ToList();
        Assert.Equal(2, rules.Count);
        Assert.All(rules, r => Assert.Equal("Info", r.Severity));
    }

    [Fact]
    public void Facts_ListTypesMethodsAndUsings()
    {
        const string src = "using System;\nclass A { public int F() => 1; }\nrecord R(int X);\nclass P { static void Main() {} }";
        var e = RoslynAnalyzer.Analyze(src);
        Assert.Contains("System", e.Usings);
        Assert.Contains(e.Types, t => t.Name == "A");
        Assert.Contains(e.Types, t => t.Name == "R");
        Assert.Contains(e.Methods, m => m.Name == "F" && m.ReturnType == "int");
        Assert.Equal(4, e.LineCount);
    }

    [Fact]
    public void EmptySource_DoesNotThrow()
    {
        var e = RoslynAnalyzer.Analyze("");
        Assert.Equal(0, e.LineCount);
    }

    [Fact]
    public void SyntaxGarbage_DoesNotThrow_AndDoesNotCompile()
    {
        var e = RoslynAnalyzer.Analyze("class { ??? ;;; }}} <<<");
        Assert.False(e.Compiles);
        Assert.NotEmpty(e.Diagnostics);
    }

    [Fact]
    public void InterestingLines_CombineDiagnosticsAndFindings()
    {
        var e = RoslynAnalyzer.Analyze("int x = \"a\";\nwhile (true) { }");
        var lines = e.InterestingLines();
        Assert.Contains(1, lines);
        Assert.Contains(2, lines);
    }
}

public class SourceWindowTests
{
    private static string Lines(int n) => string.Join('\n', Enumerable.Range(1, n).Select(i => $"var v{i} = {i};"));

    [Fact]
    public void ShortSource_IsReturnedWhole_WithLineNumbers()
    {
        var (text, truncated) = SourceWindow.Build("a\nb\nc", [], 10_000);
        Assert.False(truncated);
        Assert.Equal("1: a\n2: b\n3: c", text);
    }

    [Fact]
    public void LongSource_IsTruncated_AroundInterestingLines_WithinBudget()
    {
        var (text, truncated) = SourceWindow.Build(Lines(2000), [1500], 3000);
        Assert.True(truncated);
        Assert.True(text.Length <= 3000 + 200);
        Assert.Contains("var v1500 = 1500;", text);
        Assert.Contains("lines omitted", text);
    }

    [Fact]
    public void Truncation_NeverKeepsFarLinesWhenBudgetIsTight()
    {
        var (text, _) = SourceWindow.Build(Lines(2000), [10], 1500);
        Assert.Contains("var v10 = 10;", text);
        Assert.DoesNotContain("var v1999 = 1999;", text);
    }

    [Fact]
    public void MaxSourceChars_ScalesWithContext_AndHasFloor()
    {
        Assert.True(SourceWindow.MaxSourceChars(8192, 1024) > SourceWindow.MaxSourceChars(4096, 1024));
        Assert.Equal(2000, SourceWindow.MaxSourceChars(1000, 900));
    }
}

public class SandboxResponseTests
{
    [Fact]
    public void ParseResponse_ReadsToolResult()
    {
        const string inner = """{"status":"ExecutionCompleted","diagnostics":[],"output":"hi\n","outputTruncated":false,"exitCode":0,"exceptionType":null,"exceptionMessage":null,"exceptionStackTrace":null,"compilationDurationMs":1,"executionDurationMs":2,"totalDurationMs":3}""";
        var rpc = JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1, result = new { content = new[] { new { type = "text", text = inner } }, isError = false } });
        var r = WebSocketSandboxClient.ParseResponse(rpc);
        Assert.Equal("ExecutionCompleted", r.Status);
        Assert.Equal("hi\n", r.Output);
    }

    [Fact]
    public void ParseResponse_RpcError_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            WebSocketSandboxClient.ParseResponse("""{"jsonrpc":"2.0","id":1,"error":{"code":-32602,"message":"bad"}}"""));
    }
}

public class WorkflowPromptTests
{
    [Fact]
    public void Prompt_ContainsEvidence_AndMarksTruncation()
    {
        var evidence = RoslynAnalyzer.Analyze("int x = \"a\";\nwhile (true) { }");
        var prompt = CodeWorkflowService.BuildPrompt(evidence, "1: int x = \"a\";", truncated: true, run: null, runNote: null, focus: null, debug: false);
        Assert.Contains("CS0029", prompt);
        Assert.Contains("CDN004", prompt);
        Assert.Contains("TRUNCATED", prompt);
        Assert.Contains("Compiles: no", prompt);
    }

    [Fact]
    public void DebugPrompt_IncludesRunOutputAndException()
    {
        var evidence = RoslynAnalyzer.Analyze("System.Console.WriteLine(1);");
        var run = new CompileAndRunResponse("ExecutionFailed", [], "partial output", false, null,
            "System.DivideByZeroException", "Attempted to divide by zero.", null, 1, 1, 2);
        var prompt = CodeWorkflowService.BuildPrompt(evidence, "1: x", false, run, null, "why crash", debug: true);
        Assert.Contains("DivideByZeroException", prompt);
        Assert.Contains("partial output", prompt);
        Assert.Contains("Focus: why crash", prompt);
    }

    [Fact]
    public void Validate_RejectsMissingAndOversizedSource()
    {
        Assert.NotNull(CodeWorkflowService.Validate(new CodeRequest(" ")));
        Assert.NotNull(CodeWorkflowService.Validate(null));
        Assert.NotNull(CodeWorkflowService.Validate(new CodeRequest(new string('x', RoslynAnalyzer.MaxSourceChars + 1))));
        Assert.Null(CodeWorkflowService.Validate(new CodeRequest("int x = 1;")));
    }
}

public class WorkflowApiTests
{
    private sealed class StubSandbox(Func<CompileAndRunResponse> respond) : ISandboxClient
    {
        public int Calls { get; private set; }

        public Task<CompileAndRunResponse> CompileAndRunAsync(string source, string stdin, int timeoutMs, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(respond());
        }
    }

    private static CompileAndRunResponse Failed() =>
        new("ExecutionFailed", [], "before crash", false, null, "System.DivideByZeroException", "Attempted to divide by zero.", null, 1, 1, 2);

    [Fact]
    public async Task Review_StreamsEvidenceThenTokensThenDone_AndNeverRunsCode()
    {
        var sandbox = new StubSandbox(Failed);
        await using var server = new AiServer(extra: s => s.AddSingleton<ISandboxClient>(sandbox));
        using var client = await server.CreatePairedClientAsync();

        var response = await client.PostAsJsonAsync("/v1/review", new { source = "int x = \"a\";" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.IndexOf("event: evidence", StringComparison.Ordinal) < body.IndexOf("event: token", StringComparison.Ordinal));
        Assert.Contains("CS0029", body);
        Assert.Contains("\"evidenceBased\":true", body);
        Assert.Equal(0, sandbox.Calls);

        var prompt = Assert.Single(server.Provider.Requests).Messages[^1].Content;
        Assert.Contains("CS0029", prompt);
    }

    [Fact]
    public async Task Debug_RunsInSandbox_AndFeedsRealOutputToModel()
    {
        var sandbox = new StubSandbox(Failed);
        await using var server = new AiServer(extra: s => s.AddSingleton<ISandboxClient>(sandbox));
        using var client = await server.CreatePairedClientAsync();

        var response = await client.PostAsJsonAsync("/v1/debug", new { source = "int z = 0; System.Console.WriteLine(1 / z);" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(1, sandbox.Calls);
        Assert.Contains("DivideByZeroException", body);
        Assert.Contains("DivideByZeroException", Assert.Single(server.Provider.Requests).Messages[^1].Content);
    }

    [Fact]
    public async Task Debug_DoesNotRun_WhenCodeDoesNotCompile()
    {
        var sandbox = new StubSandbox(Failed);
        await using var server = new AiServer(extra: s => s.AddSingleton<ISandboxClient>(sandbox));
        using var client = await server.CreatePairedClientAsync();
        await client.PostAsJsonAsync("/v1/debug", new { source = "int x = \"a\";" });
        Assert.Equal(0, sandbox.Calls);
    }

    [Fact]
    public async Task Debug_RunFalse_SkipsSandbox()
    {
        var sandbox = new StubSandbox(Failed);
        await using var server = new AiServer(extra: s => s.AddSingleton<ISandboxClient>(sandbox));
        using var client = await server.CreatePairedClientAsync();
        await client.PostAsJsonAsync("/v1/debug", new { source = "System.Console.WriteLine(1);", run = false });
        Assert.Equal(0, sandbox.Calls);
    }

    [Fact]
    public async Task Debug_SandboxDown_DegradesGracefully()
    {
        await using var server = new AiServer(extra: s => s.AddSingleton<ISandboxClient>(
            new StubSandbox(() => throw new InvalidOperationException("sandbox offline"))));
        using var client = await server.CreatePairedClientAsync();
        var response = await client.PostAsJsonAsync("/v1/debug", new { source = "System.Console.WriteLine(1);" });
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sandbox unavailable", body);
        Assert.Contains("event: done", body);
    }

    [Theory]
    [InlineData("/v1/review")]
    [InlineData("/v1/debug")]
    public async Task Workflows_RequireAuth_AndValidateInput(string path)
    {
        await using var server = new AiServer();
        using var anonymous = server.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(path, new { source = "int x;" })).StatusCode);

        using var client = await server.CreatePairedClientAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new { source = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            (await client.PostAsJsonAsync(path, new { source = new string('x', RoslynAnalyzer.MaxSourceChars + 1) })).StatusCode);
        Assert.Empty(server.Provider.Requests);
    }

    [Fact]
    public async Task Review_LargeSource_IsTruncatedAndReportedAsSuch()
    {
        await using var server = new AiServer();
        using var client = await server.CreatePairedClientAsync();
        var big = string.Join('\n', Enumerable.Range(0, 2500).Select(i => $"var value{i} = {i};")) + "\nwhile (true) { }";
        var body = await (await client.PostAsJsonAsync("/v1/review", new { source = big })).Content.ReadAsStringAsync();

        Assert.Contains("\"sourceTruncated\":true", body);
        var prompt = Assert.Single(server.Provider.Requests).Messages[^1].Content;
        Assert.Contains("TRUNCATED", prompt);
        Assert.Contains("CDN004", prompt);
        var profile = server.Services.GetRequiredService<ResourceProfile>();
        Assert.True(prompt.Length < (profile.NumCtx - profile.NumPredict) * SourceWindow.CharsPerToken);
    }
}
