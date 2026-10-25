using System.Runtime.InteropServices;
using CodeDotNet.Ai.Abstractions;

namespace CodeDotNet.Ai.Resources;

/// <summary>Reports facts available from the runtime. GPU detection is not attempted (A-03).</summary>
public sealed class HardwareProbe : IHardwareProbe
{
    public Task<HardwareInfo> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var totalRam = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        long freeDisk = 0;
        try
        {
            var root = Path.GetPathRoot(AppContext.BaseDirectory);
            if (!string.IsNullOrEmpty(root))
            {
                freeDisk = new DriveInfo(root).AvailableFreeSpace;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
        }

        return Task.FromResult(new HardwareInfo(
            RuntimeInformation.OSDescription,
            Environment.ProcessorCount,
            totalRam,
            freeDisk,
            GpuName: null));
    }
}
