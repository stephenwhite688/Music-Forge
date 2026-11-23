using AudioForge.Analysis;
using AudioForge.Core;
using AudioForge.IO.Codecs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AudioForge.Engine;

/// <summary>
/// Measures a file offline: decodes through CodecManager and runs the full
/// <see cref="LoudnessMeter"/> (BS.1770/EBU R128) over the entire stream.
/// Produces integrated LUFS, short-term max, LRA, true peak, and sample peak.
/// </summary>
public sealed class OfflineMeter
{
    /// <summary>
    /// Result of measuring an audio file.
    /// </summary>
    public sealed class MeterResult
    {
        /// <summary>Path of the measured file.</summary>
        public required string InputPath { get; init; }

        /// <summary>Format of the decoded audio.</summary>
        public required AudioFormat Format { get; init; }

        /// <summary>Loudness measurement report.</summary>
        public required LoudnessMeter.Report Report { get; init; }
    }

    /// <summary>
    /// Measures <paramref name="inputPath"/> and returns the loudness report.
    /// </summary>
    /// <param name="inputPath">Audio file to measure.</param>
    /// <param name="channelWeights">Optional BS.1770 channel weights (see <see cref="LoudnessMeter"/>).</param>
    /// <param name="blockFrames">Decode block size in frames.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<MeterResult> MeasureAsync(
        string inputPath,
        float[]? channelWeights = null,
        int blockFrames = 8192,
        CancellationToken cancellationToken = default)
    {
        var stream = await CodecManager.ReadAsync(inputPath, cancellationToken);
        try
        {
            int rate = stream.SampleRate;
            int channels = stream.Channels;
            using var meter = new LoudnessMeter(rate, channels, channelWeights);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                float[] frames = stream.ReadFrames(blockFrames);
                if (frames.Length == 0) break;
                meter.Process(frames);
            }

            var report = meter.Finish();
            return new MeterResult
            {
                InputPath = inputPath,
                Format = new AudioFormat(rate, channels, isFloat: true, bitsPerSample: 32),
                Report = report
            };
        }
        finally
        {
            stream.Dispose();
        }
    }
}
