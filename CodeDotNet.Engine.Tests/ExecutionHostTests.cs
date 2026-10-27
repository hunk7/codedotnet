using CodeDotNet.Engine;
using Xunit;

namespace CodeDotNet.Engine.Tests;

/// <summary>
/// Verifies <see cref="ExecutionHost"/> behavior (FR-060 through FR-074): console output
/// capture, unhandled exceptions, and the 100,000-character output
/// truncation limit.
/// </summary>
public class ExecutionHostTests
{
    private static byte[] CompileOrThrow(string source)
    {
        var result = CompilerHost.Compile(source);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        return result.AssemblyBytes!;
    }

    [Fact]
    public void Run_SimpleProgram_CapturesConsoleOutput()
    {
        const string source = """
            using System;
            Console.WriteLine("Hello from codedotnet!");
            """;

        var assembly = CompileOrThrow(source);
        var result = ExecutionHost.Run(assembly);

        Assert.Contains("Hello from codedotnet!", result.Output);
        Assert.Null(result.ExceptionType);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Run_UnhandledException_IsCapturedNotThrown()
    {
        const string source = """
            using System;
            throw new InvalidOperationException("boom");
            """;

        var assembly = CompileOrThrow(source);
        var result = ExecutionHost.Run(assembly);

        Assert.NotNull(result.ExceptionType);
        Assert.Contains("InvalidOperationException", result.ExceptionType);
        Assert.Equal("boom", result.ExceptionMessage);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void Run_OutputExceedingLimit_IsTruncated()
    {
        const string source = """
            using System;
            for (int i = 0; i < 20000; i++)
            {
                Console.Write("0123456789");
            }
            """;

        var assembly = CompileOrThrow(source);
        var result = ExecutionHost.Run(assembly);

        Assert.True(result.OutputTruncated);
        Assert.Equal(100_000, result.Output.Length);
    }

    [Fact]
    public void Run_OutputWithinLimit_IsNotTruncated()
    {
        const string source = """
            using System;
            Console.Write("short output");
            """;

        var assembly = CompileOrThrow(source);
        var result = ExecutionHost.Run(assembly);

        Assert.False(result.OutputTruncated);
        Assert.Equal("short output", result.Output);
    }
}
