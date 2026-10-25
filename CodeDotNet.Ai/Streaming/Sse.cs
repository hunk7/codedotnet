using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CodeDotNet.Ai.Streaming;

public sealed record SseEvent(string Name, object Data);

public static class Sse
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task WriteAsync(HttpResponse response, IAsyncEnumerable<SseEvent> events, CancellationToken cancellationToken)
    {
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await foreach (var e in events.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await response.WriteAsync(Format(e), cancellationToken).ConfigureAwait(false);
                await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public static string Format(SseEvent e) =>
        $"event: {e.Name}\ndata: {JsonSerializer.Serialize(e.Data, Json)}\n\n";

    public static async IAsyncEnumerable<SseEvent> Single(SseEvent e, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield return e;
    }
}
