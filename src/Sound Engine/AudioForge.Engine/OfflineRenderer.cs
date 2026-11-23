using AudioForge.Core;
using AudioForge.IO;
using AudioForge.IO.Codecs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AudioForge.Engine;

/// <summary>
/// Renders audio offline (non-realtime): decodes an input file through the codec
/// layer, passes it through a chain of <see cref="IAudioNode"/> processors in
/// fixed-size blocks, and writes the result to a WAV file. Deterministic - the
/// same input and node chain always produce the same output.
/// </summary>
public sealed class OfflineRenderer
{
    /// <summary>
    /// Default block size in frames per processing pass.
    /// </summary>
    public const int DefaultBlockFrames = 8192;

    /// <summary>
    /// Describes the result of a completed offline render.
    /// </summary>
    public sealed class RenderResult
    {
        /// <summary>Path of the rendered output file.</summary>
        public required string OutputPath { get; init; }

        /// <summary>Audio format of the rendered file.</summary>
        public required AudioFormat Format { get; init; }

        /// <summary>Total frames written.</summary>
        public long FramesRendered { get; init; }

        /// <summary>Duration of the rendered audio.</summary>
        public TimeSpan Duration => Format.SampleRate > 0
            ? TimeSpan.FromSeconds((double)FramesRendered / Format.SampleRate)
            : TimeSpan.Zero;
    }

    /// <summary>
    /// Renders <paramref name="inputPath"/> to <paramref name="outputPath"/>,
    /// optionally passing audio through <paramref name="nodes"/> in order.
    /// </summary>
    /// <param name="inputPath">Source audio file (any codec registered with CodecManager).</param>
    /// <param name="outputPath">Destination WAV file path.</param>
    /// <param name="nodes">Optional ordered chain of audio nodes to process the signal.</param>
    /// <param name="bitsPerSample">Output bit depth (8, 16, 24 or 32). Default 24.</param>
    /// <param name="blockFrames">Frames per processing block. Default <see cref="DefaultBlockFrames"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<RenderResult> RenderAsync(
        string inputPath,
        string outputPath,
        IEnumerable<IAudioNode>? nodes = null,
        int bitsPerSample = 24,
        int blockFrames = DefaultBlockFrames,
        CancellationToken cancellationToken = default)
    {
        AudioStream decoded = await CodecManager.ReadAsync(inputPath, cancellationToken);
        int sampleRate = decoded.SampleRate;
        int channels = decoded.Channels;

        var nodeList = nodes is null ? new List<IAudioNode>() : new List<IAudioNode>(nodes);
        var format = new AudioFormat(sampleRate, channels, isFloat: true, bitsPerSample: 32);
        foreach (var node in nodeList)
        {
            node.Reset();
            node.Prepare(format);
        }

        long framesRendered = 0;
        using (decoded)
        using (var writer = new WavWriter(outputPath, sampleRate, channels, bitsPerSample))
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                float[] buffer = decoded.ReadFrames(blockFrames);
                if (buffer.Length == 0) break;

                foreach (var node in nodeList)
                    node.Process(buffer);

                writer.WriteFrames(buffer);
                framesRendered += buffer.Length / channels;
            }
            writer.Complete();
        }

        return new RenderResult
        {
            OutputPath = outputPath,
            Format = new AudioFormat(sampleRate, channels, isFloat: false, bitsPerSample: bitsPerSample),
            FramesRendered = framesRendered
        };
    }
}
