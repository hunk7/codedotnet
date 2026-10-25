using CodeDotNet.Ai.Abstractions;

namespace CodeDotNet.Ai.Resources;

/// <summary>
/// Chooses a profile from the probed hardware. The RAM thresholds are provisional
/// configuration (not benchmark results); a user override always wins.
/// </summary>
public sealed class ProfileResolver : IProfileResolver
{
    public const long RecommendedMinRamBytes = 24L * 1024 * 1024 * 1024;

    public ResourceProfile Resolve(HardwareInfo hardware, ProfileOverrides overrides)
    {
        var forced = Profiles.ByName(overrides.ProfileName);
        if (forced is not null)
        {
            return forced;
        }

        return hardware.TotalRamBytes >= RecommendedMinRamBytes && hardware.LogicalCores >= 8
            ? Profiles.Standard
            : Profiles.Low;
    }
}
