using AudioForge.Analysis;
using AudioForge.IO;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AudioForge.IO.Codecs;

/// <summary>
/// File-level ReplayGain: decodes an audio file, measures integrated LUFS
/// and true peak via <see cref="ReplayGainAnalyzer"/>, and writes
/// REPLAYGAIN_* tags through <see cref="ReplayGainTagIO"/>.
/// </summary>
public static class ReplayGainCalculator
{
    private const int ChunkFrames = 8192;

    /// <summary>
    /// Computes ReplayGain values for <paramref name="path"/> and writes them
    /// as tags. Returns the computed values.
    /// </summary>
    /// <param name="writeTags">Whether to persist the tags to the file.</param>
    public static async Task<ReplayGainTags> ComputeAsync(string path,
        bool writeTags = true, CancellationToken cancellationToken = default)
    {
        var stream = await CodecManager.ReadAsync(path, cancellationToken);
        ReplayGainTags tags;
        using (stream)
        {
            using var analyzer = new ReplayGainAnalyzer(stream.SampleRate, stream.Channels);
            var buf = new float[ChunkFrames * stream.Channels];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int frames = await Task.Run(() => stream.ReadFramesInto(buf, ChunkFrames), cancellationToken);
                if (frames <= 0) break;
                analyzer.Process(buf.AsSpan(0, frames * stream.Channels));
            }
            var result = analyzer.Finish();
            tags = new ReplayGainTags(
                result.TrackGainDb, result.TrackPeakDb, result.AlbumGainDb, result.TrackPeakDb);
        }

        if (writeTags)
            ReplayGainTagIO.Write(path, tags);
        return tags;
    }
}
