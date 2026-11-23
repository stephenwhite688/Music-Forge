using AudioForge.Core;
using AudioForge.IO;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace AudioForge.IO.Codecs;

/// <summary>
/// AAC-LC encoder via Media Foundation SinkWriter (.m4a/.mp4 container).
/// Feeds interleaved float32 PCM from an <see cref="AudioStream"/>, converts to
/// PCM16 (the only input format the MF AAC encoder accepts) and writes
/// compressed AAC. Bitrate is quantized to the encoder's supported set.
/// </summary>
public static class MediaFoundationAacEncoder
{
    private const int SampleFramesPerWrite = 4096; // ~85ms @48k

    // MF AAC encoder supported output bitrates, bytes/sec.
    private static readonly int[] SupportedBitrates = { 12000, 16000, 20000, 24000 };

    /// <summary>
    /// Encodes <paramref name="source"/> PCM to an AAC file at
    /// <paramref name="path"/>. Container is chosen by extension (.m4a/.mp4).
    /// </summary>
    /// <param name="bitrate">Target bitrate in bits/sec; quantized to the
    /// nearest supported value (96/128/160/192 kbps).</param>
    public static async Task EncodeAsync(string path, AudioStream source,
        int bitrate = 192000, CancellationToken cancellationToken = default)
    {
        int sampleRate = source.SampleRate;
        int channels = Math.Max(1, source.Channels);
        if (sampleRate != 44100 && sampleRate != 48000)
            throw new NotSupportedException(
                $"MF AAC encoder supports 44100/48000 Hz only (got {sampleRate}).");
        if (channels < 1 || channels > 2)
            throw new NotSupportedException(
                $"MF AAC encoder supports mono/stereo only (got {channels} ch).");

        int bytesPerSec = SupportedBitrates[0];
        foreach (int b in SupportedBitrates)
            if (Math.Abs(b * 8 - bitrate) < Math.Abs(bytesPerSec * 8 - bitrate))
                bytesPerSec = b;

        int hr = MfNative.MFStartup(MfNative.MF_VERSION, MfNative.MF_STARTUP_FULL);
        if (hr < 0) throw new InvalidOperationException($"MFStartup failed: 0x{hr:X8}");

        IntPtr writer = IntPtr.Zero, attrs = IntPtr.Zero;
        IntPtr inputType = IntPtr.Zero, outputType = IntPtr.Zero;
        try
        {
            hr = MfNative.MFCreateAttributes(out attrs, 1);
            Marshal.ThrowExceptionForHR(hr);
            MfNative.AttrSetUINT32(attrs, MfNative.MF_SINK_WRITER_DISABLE_THROTTLING, 1);
            hr = MfNative.MFCreateSinkWriterFromURL(path, IntPtr.Zero, attrs, out writer);
            Marshal.ThrowExceptionForHR(hr);

            // Output media type: AAC-LC in MP4/M4A container.
            hr = MfNative.MFCreateMediaType(out outputType);
            Marshal.ThrowExceptionForHR(hr);
            MfNative.AttrSetGUID(outputType, MfNative.MF_MT_MAJOR_TYPE, MfNative.MFMediaType_Audio);
            MfNative.AttrSetGUID(outputType, MfNative.MF_MT_SUBTYPE, MfNative.MFMediaType_AAC);
            MfNative.AttrSetUINT32(outputType, MfNative.MF_MT_COMPRESSED, 1);
            MfNative.AttrSetUINT32(outputType, MfNative.MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
            MfNative.AttrSetUINT32(outputType, MfNative.MF_MT_AUDIO_SAMPLES_PER_SECOND, (uint)sampleRate);
            MfNative.AttrSetUINT32(outputType, MfNative.MF_MT_AUDIO_NUM_CHANNELS, (uint)channels);
            MfNative.AttrSetUINT32(outputType, MfNative.MF_MT_AUDIO_AVG_BYTES_PER_SECOND, (uint)bytesPerSec);
            MfNative.AttrSetUINT32(outputType, MfNative.MF_MT_AUDIO_BLOCK_ALIGNMENT, 1);
            MfNative.SinkAddStream(writer, outputType);

            // Input media type: 16-bit PCM interleaved.
            hr = MfNative.MFCreateMediaType(out inputType);
            Marshal.ThrowExceptionForHR(hr);
            SetPcmInputType(inputType, sampleRate, channels);
            MfNative.SinkSetInputMediaType(writer, 0, inputType);

            MfNative.SinkBeginWriting(writer);

            // Stream PCM -> WriteSample loop.
            var pcm = new float[SampleFramesPerWrite * channels];
            long sampleTime = 0; // 100ns units
            long ticksPerFrame = 10_000_000L / sampleRate;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int frames = await Task.Run(() => source.ReadFramesInto(pcm, SampleFramesPerWrite), cancellationToken);
                if (frames <= 0) break;
                WritePcmSample(writer, pcm, frames, channels, sampleTime, ticksPerFrame);
                sampleTime += frames * ticksPerFrame;
            }
        }
        finally
        {
            // Finalize drains the encoder and writes the container trailer.
            if (writer != IntPtr.Zero) MfNative.SinkFinalize(writer);
            if (outputType != IntPtr.Zero) MfNative.Release(outputType);
            if (inputType != IntPtr.Zero) MfNative.Release(inputType);
            if (writer != IntPtr.Zero) MfNative.Release(writer);
            if (attrs != IntPtr.Zero) MfNative.Release(attrs);
            MfNative.MFShutdown();
        }
    }

    private static void SetPcmInputType(IntPtr type, int sampleRate, int channels)
    {
        MfNative.AttrSetGUID(type, MfNative.MF_MT_MAJOR_TYPE, MfNative.MFMediaType_Audio);
        MfNative.AttrSetGUID(type, MfNative.MF_MT_SUBTYPE, MfNative.MFMediaType_PCM);
        MfNative.AttrSetUINT32(type, MfNative.MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
        MfNative.AttrSetUINT32(type, MfNative.MF_MT_AUDIO_SAMPLES_PER_SECOND, (uint)sampleRate);
        MfNative.AttrSetUINT32(type, MfNative.MF_MT_AUDIO_NUM_CHANNELS, (uint)channels);
        MfNative.AttrSetUINT32(type, MfNative.MF_MT_ALL_SAMPLES_INDEPENDENT, 1);
    }

    private static void WritePcmSample(IntPtr writer, float[] pcm,
        int frames, int channels, long startTime, long ticksPerFrame)
    {
        int sampleCount = frames * channels;
        var pcm16 = new short[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float s = Math.Clamp(pcm[i], -1f, 1f);
            pcm16[i] = (short)(s * 32767f);
        }

        int hr = MfNative.MFCreateSample(out IntPtr sample);
        Marshal.ThrowExceptionForHR(hr);
        IntPtr buffer = IntPtr.Zero;
        try
        {
            uint byteLen = (uint)(sampleCount * sizeof(short));
            hr = MfNative.MFCreateMemoryBuffer(byteLen, out buffer);
            Marshal.ThrowExceptionForHR(hr);

            IntPtr data = MfNative.BufferLock(buffer, out _);
            for (int i = 0; i < sampleCount; i++)
                Marshal.WriteInt16(data, i * sizeof(short), pcm16[i]);
            MfNative.BufferUnlock(buffer);
            MfNative.BufferSetLength(buffer, byteLen);

            MfNative.SampleAddBuffer(sample, buffer);
            MfNative.Release(buffer);
            buffer = IntPtr.Zero;

            MfNative.SampleSetTime(sample, startTime);
            MfNative.SampleSetDuration(sample, frames * ticksPerFrame);
            MfNative.SampleSetFlags(sample, MfNative.MFSampleFlags_CleanPoint | MfNative.MFSampleFlags_Sync);
            MfNative.SinkWriteSample(writer, 0, sample);
        }
        finally
        {
            if (buffer != IntPtr.Zero) MfNative.Release(buffer);
            MfNative.Release(sample);
        }
    }
}
