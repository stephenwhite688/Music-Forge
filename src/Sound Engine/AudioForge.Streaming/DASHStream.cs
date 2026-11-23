using AudioForge.IO;
using AudioForge.IO.Codecs;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AudioForge.Streaming;

/// <summary>
/// DASH (MPEG-DASH) streaming client. Parses the MPD manifest, downloads the
/// CMAF/fMP4 init segment and media segments, concatenates them into a
/// temporary fragmented MP4, and decodes through the codec manager.
///
/// Scope: VOD playback and bounded live windows. Live manifests are fetched
/// once per open; there is no mid-playback sliding-window refresh.
/// </summary>
public static class DashStream
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Maximum segments downloaded for one manifest (safety cap).</summary>
    public const int MaxSegments = 2000;

    /// <summary>
    /// Opens a DASH manifest URL and returns a decoded AudioStream. The
    /// temporary MP4 is deleted when the returned stream is disposed.
    /// </summary>
    public static async Task<AudioStream> OpenAsync(string manifestUrl, CancellationToken ct = default)
    {
        var manifest = await DashManifest.ParseAsync(manifestUrl, Http, ct)
            ?? throw new InvalidDataException($"No playable audio representation in '{manifestUrl}'.");

        var rep = manifest.Representation;
        if (rep.Segments.Count > MaxSegments)
            throw new InvalidDataException(
                $"Manifest lists {rep.Segments.Count} segments (cap {MaxSegments}).");

        // .m4a so AacCodec claims the audio-only MP4 container.
        string temp = Path.Combine(Path.GetTempPath(), $"audiodash_{Guid.NewGuid():N}.m4a");
        try
        {
            using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write))
            {
                // Init segment first (ftyp/moov for CMAF).
                if (rep.InitializationUrl != null)
                {
                    byte[] init = await Http.GetByteArrayAsync(rep.InitializationUrl, ct);
                    await fs.WriteAsync(init, ct);
                }
                foreach (var seg in rep.Segments)
                {
                    ct.ThrowIfCancellationRequested();
                    byte[] data = await Http.GetByteArrayAsync(seg.Url, ct);
                    await fs.WriteAsync(data, ct);
                }
            }

            var inner = await CodecManager.ReadAsync(temp, ct);
            return AudioStream.CreateStreaming(
                inner.SampleRate, inner.Channels, inner.TotalFrames,
                (dest, count) => inner.ReadFramesInto(dest, count),
                frame => inner.TrySeek(frame),
                () =>
                {
                    inner.Dispose();
                    try { File.Delete(temp); } catch { }
                });
        }
        catch
        {
            try { File.Delete(temp); } catch { }
            throw;
        }
    }
}
