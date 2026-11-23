using System;

namespace AudioForge.Analysis;

/// <summary>
/// Computes ReplayGain-style track/album gain values from PCM audio using
/// the EBU R128 <see cref="LoudnessMeter"/>. ReplayGain reference level is
/// -18 LUFS; the stored peak is the true peak in dBTP (negative dB).
/// </summary>
public sealed class ReplayGainAnalyzer : IDisposable
{
    /// <summary>EBU R128 / ReplayGain reference loudness in LUFS.</summary>
    public const double ReferenceLufs = -18.0;

    private readonly LoudnessMeter _meter;
    private bool _disposed;

    public ReplayGainAnalyzer(int sampleRate, int channels)
    {
        _meter = new LoudnessMeter(sampleRate, channels);
    }

    /// <summary>Result of ReplayGain analysis.</summary>
    public sealed record Result(
        double TrackGainDb,
        double TrackPeakDb,
        double AlbumGainDb,
        double IntegratedLufs,
        double TruePeakDbTp);

    /// <summary>Feeds interleaved float PCM through the meter.</summary>
    public void Process(ReadOnlySpan<float> interleavedPcm) => _meter.Process(interleavedPcm);

    /// <summary>
    /// Finalizes analysis. <paramref name="albumLufs"/> is the integrated
    /// loudness of the whole album (for album gain); pass NaN to skip.
    /// </summary>
    public Result Finish(double albumLufs = double.NaN)
    {
        var report = _meter.Finish();
        double trackGain = ReferenceLufs - report.IntegratedLufs;
        // Peak stored as negative dB relative to full scale (ReplayGain spec
        // uses sample peak in dB; true peak is the safer bound).
        double peakDb = Math.Min(report.TruePeakDbTp, report.SamplePeakDbfs);
        double albumGain = double.IsNaN(albumLufs) ? trackGain : ReferenceLufs - albumLufs;
        return new Result(trackGain, peakDb, albumGain, report.IntegratedLufs, report.TruePeakDbTp);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _meter.Dispose();
    }
}
