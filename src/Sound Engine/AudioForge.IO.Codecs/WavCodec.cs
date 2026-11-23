using AudioForge.Core;
using AudioForge.IO;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AudioForge.IO.Codecs;

/// <summary>
/// Codec implementation for reading/writing WAV (Waveform Audio Format) files (.wav, .wave).
/// </summary>
public class WavCodec : AudioCodecBase
{
    /// <summary>
    /// Gets the name of this codec ("WAV").
    /// </summary>
    public override string Name => "WAV";

    /// <summary>
    /// Gets the file extensions supported by this codec (".wav", ".wave").
    /// </summary>
    public override string DecoderBackend => "PCM parser (managed)";
    public override bool IsVerifiedDecoder => true;
    public override string[] SupportedExtensions => new[] { ".wav", ".wave" };

    /// <summary>
    /// Gets whether this codec can write audio files.
    /// </summary>
    public override bool CanWrite => true;

    /// <summary>
    /// Reads a WAV audio file asynchronously and returns its stream.
    /// </summary>
    /// <param name="path">Path to the WAV audio file.</param>
    /// <param name="cancellationToken">A token to allow cancellation of the operation.</param>
    /// <returns>The decoded audio stream with sample rate, channels, and bit depth extracted from the header.</returns>
    public override Task<AudioStream?> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        // Chunked decode: keep the WAV file open and pull frames on demand
        // instead of buffering the whole file as float PCM in memory.
        var reader = new WavReader(path);
        var stream = AudioStream.CreateStreaming(
            reader.SampleRate, reader.Channels, reader.TotalFrames,
            (dest, count) => reader.ReadFramesInto(dest, count),
            frame => { reader.Seek(frame); return true; },
            reader.Dispose);
        return Task.FromResult<AudioStream?>(stream);
    }

    /// <summary>
    /// Writes an audio stream asynchronously to a WAV file.
    /// </summary>
    /// <param name="path">Path to save the WAV file.</param>
    /// <param name="stream">The audio stream to write.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public override async Task WriteAsync(string path, AudioStream stream, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var writer = new WavWriter(path, stream.SampleRate, stream.Channels, stream.BitsPerSample);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                float[] frames = stream.ReadFrames(8192 / stream.Channels);
                if (frames.Length == 0) break;
                writer.WriteFrames(frames);
            }
            writer.Complete();
        }, cancellationToken);
    }
}
