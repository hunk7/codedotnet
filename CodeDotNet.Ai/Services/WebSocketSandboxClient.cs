using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CodeDotNet.Ai.Abstractions;
using CodeDotNet.Ai.Configuration;
using CodeDotNet.Contracts;

namespace CodeDotNet.Ai.Services;

/// <summary>
/// Calls the existing sandbox MCP server's <c>compile_and_run</c> tool over WebSocket (JSON-RPC 2.0).
/// User code is never executed in this process.
/// </summary>
public sealed class WebSocketSandboxClient(AiOptions options) : ISandboxClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const int MaxMessageBytes = 2 * 1024 * 1024;

    public async Task<CompileAndRunResponse> CompileAndRunAsync(
        string source, string stdin, int timeoutMs, CancellationToken cancellationToken = default)
    {
        using var socket = new ClientWebSocket();
        using var overall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        overall.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs + 10_000));

        await socket.ConnectAsync(new Uri(options.SandboxUrl), overall.Token).ConfigureAwait(false);

        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new { name = "compile_and_run", arguments = new { source, stdin, timeoutMs } },
        });
        await socket.SendAsync(Encoding.UTF8.GetBytes(request), WebSocketMessageType.Text, true, overall.Token).ConfigureAwait(false);

        var text = await ReceiveTextAsync(socket, overall.Token).ConfigureAwait(false);
        try
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None).ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
        }

        return ParseResponse(text);
    }

    internal static CompileAndRunResponse ParseResponse(string rpcMessage)
    {
        using var doc = JsonDocument.Parse(rpcMessage);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var error))
        {
            var message = error.TryGetProperty("message", out var m) ? m.GetString() : "unknown error";
            throw new InvalidOperationException($"Sandbox error: {message}");
        }

        var inner = root.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()
            ?? throw new InvalidOperationException("Sandbox returned empty content.");
        return JsonSerializer.Deserialize<CompileAndRunResponse>(inner, Json)
            ?? throw new InvalidOperationException("Sandbox returned an unreadable response.");
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("Sandbox closed the connection before replying.");
            }

            ms.Write(buffer, 0, result.Count);
            if (ms.Length > MaxMessageBytes)
            {
                throw new InvalidOperationException("Sandbox response exceeded the size limit.");
            }

            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }
    }
}
