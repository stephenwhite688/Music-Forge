namespace AudioForge.Core;

/// <summary>
/// Describes a single automatable parameter exposed by an
/// <see cref="IAudioNode"/>. The descriptor is metadata only - the current
/// value is read/written through the node's TryGet/TrySetParameter methods.
/// </summary>
/// <param name="Name">Stable parameter identifier (e.g. "gain.db", "band.12.gain").</param>
/// <param name="Min">Minimum allowed value.</param>
/// <param name="Max">Maximum allowed value.</param>
/// <param name="Default">Factory default value.</param>
/// <param name="Unit">Display unit suffix ("dB", "ms", "Hz", "", ...). Empty for dimensionless.</param>
/// <param name="ReadOnly">True for metered/output-only parameters that cannot be set.</param>
public sealed record AudioParameterInfo(
    string Name,
    float Min,
    float Max,
    float Default,
    string Unit = "",
    bool ReadOnly = false);
