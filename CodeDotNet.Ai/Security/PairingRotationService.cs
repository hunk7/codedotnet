namespace CodeDotNet.Ai.Security;

/// <summary>
/// Rotates the pairing code (and logs the new one) when it expires unused, so the code shown in the
/// service console never goes stale while the service keeps running.
/// </summary>
public sealed class PairingRotationService(PairingService pairing, TimeProvider time) : BackgroundService
{
    public static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval, time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                pairing.RotateIfExpired();
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }
}
