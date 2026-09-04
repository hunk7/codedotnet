using System.Text;
using System.Runtime.Versioning;

namespace CodeDotNet.Compiler;

/// <summary>
/// Loads an in-memory emitted assembly and executes its entry point inside the worker,
/// capturing console output, stdin, exceptions and timings (FR-060 through FR-074).
/// </summary>
[SupportedOSPlatform("browser")]
internal static class ExecutionHost
{
    private const int MaxOutputCharacters = 100_000;

    public static ExecutionResult Run(byte[] assemblyBytes, string stdin)
    {
        var outputBuilder = new StringBuilder();
        var truncated = false;

        void AppendOutput(string text)
        {
            if (truncated || string.IsNullOrEmpty(text))
            {
                return;
            }

            var remaining = MaxOutputCharacters - outputBuilder.Length;

            if (remaining <= 0)
            {
                truncated = true;
                return;
            }

            if (text.Length > remaining)
            {
                outputBuilder.Append(text, 0, remaining);
                truncated = true;
                return;
            }

            outputBuilder.Append(text);
        }

        var originalOut = Console.Out;
        var originalErr = Console.Error;

        // NOTE: on browser-wasm, the Console.In *getter* throws PlatformNotSupportedException
        // (Arg_PlatformNotSupported) because it tries to lazily create a real stdin stream,
        // which does not exist in the browser. Console.SetIn(...) itself is safe (it just
        // stores a field), so we never read the original reader back - there is nothing
        // meaningful to restore to on this platform.
        TextReader? originalIn = null;
        try
        {
            originalIn = Console.In;
        }
        catch (PlatformNotSupportedException)
        {
            originalIn = null;
        }

        var writer = new CallbackTextWriter(AppendOutput);
        var reader = new StringReader(stdin ?? string.Empty);

        string? exceptionType = null;
        string? exceptionMessage = null;
        string? exceptionStack = null;
        var exitCode = 0;

        try
        {
            Console.SetOut(writer);
            Console.SetError(writer);
            Console.SetIn(reader);

            var assembly = System.Reflection.Assembly.Load(assemblyBytes);
#pragma warning disable IL2026 // Roslyn-emitted user program assemblies cannot be annotated for trimming.
            var entryPoint = assembly.EntryPoint
                ?? throw new InvalidOperationException("Compiled program has no entry point.");

            var parameters = entryPoint.GetParameters();
            var args = parameters.Length > 0 ? new object?[] { Array.Empty<string>() } : null;

            var result = entryPoint.Invoke(null, args);
#pragma warning restore IL2026

            if (result is int intResult)
            {
                exitCode = intResult;
            }
        }
        catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException is not null)
        {
            var inner = tie.InnerException;
            exceptionType = inner.GetType().FullName ?? inner.GetType().Name;
            exceptionMessage = inner.Message;
            exceptionStack = inner.StackTrace;
            exitCode = 1;
        }
        catch (Exception ex)
        {
            exceptionType = ex.GetType().FullName ?? ex.GetType().Name;
            exceptionMessage = ex.Message;
            exceptionStack = ex.StackTrace;
            exitCode = 1;
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);

            if (originalIn is not null)
            {
                Console.SetIn(originalIn);
            }
        }

        return new ExecutionResult(
            Output: outputBuilder.ToString(),
            OutputTruncated: truncated,
            ExitCode: exitCode,
            ExceptionType: exceptionType,
            ExceptionMessage: exceptionMessage,
            ExceptionStackTrace: exceptionStack);
    }

    private sealed class CallbackTextWriter(Action<string> onWrite) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => onWrite(value.ToString());

        public override void Write(string? value)
        {
            if (value is not null)
            {
                onWrite(value);
            }
        }

        public override void WriteLine(string? value) => onWrite((value ?? string.Empty) + Environment.NewLine);
    }
}

internal sealed record ExecutionResult(
    string Output,
    bool OutputTruncated,
    int ExitCode,
    string? ExceptionType,
    string? ExceptionMessage,
    string? ExceptionStackTrace);
