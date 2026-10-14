using CodeDotNet.Engine;
using Xunit;

namespace CodeDotNet.Engine.Tests;

/// <summary>
/// Verifies <see cref="CompileAndRunOrchestrator"/> end-to-end behavior shared by every
/// host (browser worker and MCP sandbox worker).
/// </summary>
public class CompileAndRunOrchestratorTests
{
    [Fact]
    public void CompileAndRun_SuccessfulProgram_ReturnsExecutionCompleted()
    {
        const string source = """
            using System;
            Console.WriteLine("Hello from orchestrator!");
            """;

        var response = CompileAndRunOrchestrator.CompileAndRun(source, string.Empty);

        Assert.Equal("ExecutionCompleted", response.Status);
        Assert.Contains("Hello from orchestrator!", response.Output);
        Assert.Equal(0, response.ExitCode);
        Assert.Empty(response.Diagnostics);
    }

    [Fact]
    public void CompileAndRun_SyntaxError_ReturnsBuildFailed()
    {
        const string source = """
            using System;
            Console.WriteLine("missing semicolon")
            """;

        var response = CompileAndRunOrchestrator.CompileAndRun(source, string.Empty);

        Assert.Equal("BuildFailed", response.Status);
        Assert.Contains(response.Diagnostics, d => d.Severity == "Error");
        Assert.Null(response.ExitCode);
    }

    [Fact]
    public void CompileAndRun_UnhandledException_ReturnsExecutionFailed()
    {
        const string source = """
            using System;
            throw new InvalidOperationException("boom");
            """;

        var response = CompileAndRunOrchestrator.CompileAndRun(source, string.Empty);

        Assert.Equal("ExecutionFailed", response.Status);
        Assert.Equal("boom", response.ExceptionMessage);
    }
}
