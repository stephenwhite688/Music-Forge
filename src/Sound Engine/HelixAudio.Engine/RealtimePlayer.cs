using HelixAudio.Backend.Wasapi;
using HelixAudio.Core;
using HelixAudio.IO;
using HelixAudio.IO.Codecs;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HelixAudio.Engine;

/// <summary>
/// Realtime file playback: decodes an audio file through CodecManager, runs it
/// through an optional <see cref="IAudioNode"/> chain, and streams it to the
/// WASAPI output device via <see cref="WasapiDeviceManager"/>.
///
/// The feed loop is paced by the output buffer level so the 5-second provider
/// buffer never overflows; Pause/Resume/Stop map onto the device manager.
/// </summary>
public sealed class RealtimePlayer : IDisposable
{
    private readonly WasapiDeviceManager _device = new();
    private readonly object _gate = new();

    private AudioStream? _stream;
    private MediaFoundationReader? _streamReader;
    private int _playbackId; // incremented per play/stop - stale feed threads check this
    private List<IAudioNode> _nodes = new();
    private Thread? _feedThread;
    private volatile bool _stopRequested;
    private volatile bool _paused;
    private long _framesWritten;
    private int _sampleRate;
    private int _channels;
    private bool _disposed;

    /// <summary>Raised on the feed thread when playback reaches end of stream.</summary>
    public event EventHandler? PlaybackCompleted;

    /// <summary>Raised on the feed thread on fatal playback errors.</summary>
    public event EventHandler<Exception>? PlaybackError;

    /// <summary>True while a file is loaded and the device is running.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>True while paused.</summary>
    public bool IsPaused => _paused;

    /// <summary>Output volume 0..1 (maps to WASAPI volume).</summary>
    public float Volume
    {
        get => _device.Volume;
        set => _device.Volume = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Current playback position - frames actually rendered minus what is
    /// still queued in the output buffer.
    /// </summary>
    public TimeSpan Position
    {
        get
        {
            if (_sampleRate <= 0) return TimeSpan.Zero;
            double buffered = _device.BufferedWaveProvider?.BufferedDuration.TotalSeconds ?? 0.0;
            double played = Math.Max(0.0, (double)_framesWritten / _sampleRate - buffered);
            return TimeSpan.FromSeconds(played);
        }
    }

    /// <summary>Total duration of the loaded file.</summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>Sample rate of the loaded file.</summary>
    public int SampleRate => _sampleRate;

    /// <summary>Channel count of the loaded file.</summary>
    public int Channels => _channels;

    /// <summary>
    /// WASAPI output endpoint ID to play through (see
    /// <see cref="WasapiDeviceManager.EnumerateOutputDevices"/>). Empty/null =
    /// Windows default device. Applies to the NEXT play call.
    /// </summary>
    public string OutputDeviceId
    {
        get => _device.CurrentOutputDeviceId;
        set => _device.CurrentOutputDeviceId = value;
    }

    /// <summary>
    /// Loads and starts playback of <paramref name="path"/>.
    /// </summary>
    /// <param name="path">Audio file to play.</param>
    /// <param name="nodes">Optional ordered chain of <see cref="IAudioNode"/> processors.</param>
    /// <param name="blockFrames">Frames per feed block (default 512 ≈ 10.7 ms at 48 kHz).</param>
    public async Task PlayAsync(
        string path,
        IEnumerable<IAudioNode>? nodes = null,
        int blockFrames = 512)
    {
        Stop();
        _stream?.Dispose();
        _stream = await CodecManager.ReadAsync(path);
        _sampleRate = _stream.SampleRate;
        _channels = _stream.Channels;
        Duration = TimeSpan.FromSeconds((double)_stream.TotalFrames / _sampleRate);
        _nodes = nodes is null ? new List<IAudioNode>() : new List<IAudioNode>(nodes);

        var format = new AudioFormat(_sampleRate, _channels, isFloat: true, bitsPerSample: 32);
        foreach (var node in _nodes)
        {
            node.Reset();
            node.Prepare(format);
        }

        _framesWritten = 0;
        _stopRequested = false;
        _paused = false;
        int id = ++_playbackId;

        _device.Initialize(_sampleRate, _channels);
        _device.Start();
        IsPlaying = true;

        var source = _stream;
        int ch = _channels;
        _feedThread = new Thread(s => FeedLoop(s, id, blockFrames, ch, source, null)) { IsBackground = true, Name = "HelixAudio.Feed" };
        _feedThread.Start();
    }

    /// <summary>
    /// Starts playback of a live HTTP/HTTPS audio stream (internet radio)
    /// through Media Foundation. Blocks while connecting to the stream.
    /// </summary>
    /// <param name="url">Stream URL.</param>
    /// <param name="blockFrames">Frames per feed block (default 512 ≈ 10.7 ms at 48 kHz).</param>
    public void PlayStream(string url, int blockFrames = 512, IEnumerable<IAudioNode>? nodes = null)
    {
        Stop();
        _stream?.Dispose();
        _stream = null;
        _streamReader?.Dispose();
        var reader = new MediaFoundationReader(url);
        _streamReader = reader;

        var wf = reader.WaveFormat;
        _sampleRate = wf.SampleRate;
        _channels = wf.Channels;
        Duration = TimeSpan.Zero; // live stream - no fixed duration
        _nodes = nodes is null ? new List<IAudioNode>() : new List<IAudioNode>(nodes);
        var streamFormat = new AudioFormat(_sampleRate, _channels, isFloat: true, bitsPerSample: 32);
        foreach (var node in _nodes)
        {
            node.Reset();
            node.Prepare(streamFormat);
        }

        _framesWritten = 0;
        _stopRequested = false;
        _paused = false;
        int id = ++_playbackId;

        _device.Initialize(_sampleRate, _channels);
        _device.Start();
        IsPlaying = true;

        int ch = _channels;
        _feedThread = new Thread(s => FeedLoop(s, id, blockFrames, ch, null, reader)) { IsBackground = true, Name = "HelixAudio.StreamFeed" };
        _feedThread.Start();
    }

    /// <summary>Pauses playback (device paused, feed loop idles).</summary>
    public void Pause()
    {
        if (!IsPlaying || _paused) return;
        _paused = true;
        _device.Pause();
    }

    /// <summary>Resumes playback.</summary>
    public void Resume()
    {
        if (!IsPlaying || !_paused) return;
        _paused = false;
        _device.Resume();
    }

    /// <summary>Stops playback and releases the device.</summary>
    public void Stop()
    {
        _stopRequested = true;
        _playbackId++; // invalidate any in-flight feed thread
        _paused = false;
        IsPlaying = false;
        try { _feedThread?.Join(500); } catch { }
        _feedThread = null;
        try { _device.Stop(); } catch { }
        _stream?.Dispose();
        _stream = null;
        _streamReader?.Dispose();
        _streamReader = null;
    }

    private void FeedLoop(object? state, int id, int blockFrames, int ch, AudioStream? file, MediaFoundationReader? reader)
    {
        try
        {
            // Realtime-safe: buffers allocated once, sized to a full block.
            // The only resize happens on the final partial block at EOS.
            var buffer = new float[blockFrames * ch];
            var byteScratch = reader is not null ? new byte[blockFrames * reader.WaveFormat.BlockAlign] : null;
            while (!_stopRequested && id == _playbackId)
            {
                if (_paused)
                {
                    Thread.Sleep(20);
                    continue;
                }

                // Pace against the output buffer: keep ~2s of audio queued,
                // never let the 5s provider buffer overflow.
                var provider = _device.BufferedWaveProvider;
                if (provider != null && provider.BufferedDuration > TimeSpan.FromSeconds(2.0))
                {
                    Thread.Sleep(25);
                    continue;
                }

                int frames = reader is not null
                    ? ReadStreamFrames(reader, ch, buffer, blockFrames, byteScratch!)
                    : ReadFramesInto(file!, buffer, blockFrames);
                if (frames == 0) break; // end of stream

                int validSamples = frames * ch;
                float[] proc = buffer;
                if (validSamples < buffer.Length)
                {
                    // Final partial block - shrink once (cold path, once per track)
                    Array.Resize(ref buffer, validSamples);
                    proc = buffer;
                }
                foreach (var node in _nodes)
                    node.Process(proc);

                _device.Write(proc, validSamples);
                _framesWritten += frames;
            }

            // Drain: let the buffer play out before signalling completion
            var drain = _device.BufferedWaveProvider;
            while (!_stopRequested && id == _playbackId && drain != null && drain.BufferedDuration > TimeSpan.FromMilliseconds(100))
                Thread.Sleep(50);

            // Stale thread (superseded by a newer play/stop) - exit silently
            // without touching shared device state or raising events.
            if (id != _playbackId) return;

            IsPlaying = false;
            try { _device.Stop(); } catch { }
            // Only signal natural end-of-stream - an intentional Stop() must not
            // look like "track finished" to playlist controllers.
            if (!_stopRequested)
                PlaybackCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            if (id != _playbackId) return; // stale thread - swallow the error
            IsPlaying = false;
            try { _device.Stop(); } catch { }
            PlaybackError?.Invoke(this, ex);
        }
    }

    private static int ReadFramesInto(AudioStream stream, float[] buffer, int frames)
    {
        // Zero the tail first so a short read cannot leak stale samples
        int clearSamples = Math.Min(buffer.Length, frames * stream.Channels);
        Array.Clear(buffer, 0, clearSamples);
        return stream.ReadFramesInto(buffer, frames);
    }

    /// <summary>Reads a block of frames from the live stream reader into <paramref name="buffer"/>.</summary>
    /// <returns>Frames actually read (0 on stream end/error).</returns>
    private static int ReadStreamFrames(MediaFoundationReader reader, int channels, float[] buffer, int frames, byte[] byteBuffer)
    {
        var fmt = reader.WaveFormat;
        int blockAlign = Math.Max(1, fmt.BlockAlign);
        int wanted = Math.Min(byteBuffer.Length, frames * blockAlign);

        int total = 0, got;
        while (total < wanted && (got = reader.Read(byteBuffer, total, wanted - total)) > 0)
            total += got;
        if (total < blockAlign) return 0;

        int framesRead = total / blockAlign;
        ConvertToFloat(byteBuffer, total, fmt, buffer, framesRead * channels);
        return framesRead;
    }

    /// <summary>Converts interleaved PCM/float bytes into the float buffer used by the output device.</summary>
    private static void ConvertToFloat(byte[] bytes, int length, WaveFormat fmt, float[] buffer, int sampleCount)
    {
        int pos = 0, idx = 0;
        switch (fmt.Encoding)
        {
            case WaveFormatEncoding.IeeeFloat:
                while (pos + 4 <= length && idx < sampleCount)
                {
                    buffer[idx++] = BitConverter.ToSingle(bytes, pos);
                    pos += 4;
                }
                break;
            case WaveFormatEncoding.Pcm:
                if (fmt.BitsPerSample == 16)
                {
                    while (pos + 2 <= length && idx < sampleCount)
                    {
                        buffer[idx++] = BitConverter.ToInt16(bytes, pos) / 32768f;
                        pos += 2;
                    }
                }
                else if (fmt.BitsPerSample == 24)
                {
                    while (pos + 3 <= length && idx < sampleCount)
                    {
                        int v = bytes[pos] | (bytes[pos + 1] << 8) | (bytes[pos + 2] << 16);
                        if ((v & 0x800000) != 0) v |= unchecked((int)0xFF000000);
                        buffer[idx++] = v / 8388608f;
                        pos += 3;
                    }
                }
                else if (fmt.BitsPerSample == 32)
                {
                    while (pos + 4 <= length && idx < sampleCount)
                    {
                        buffer[idx++] = BitConverter.ToInt32(bytes, pos) / 2147483648f;
                        pos += 4;
                    }
                }
                else if (fmt.BitsPerSample == 8)
                {
                    while (pos + 1 <= length && idx < sampleCount)
                    {
                        buffer[idx++] = (bytes[pos] - 128) / 128f;
                        pos += 1;
                    }
                }
                break;
            default:
                // Unsupported encoding - write silence rather than crash
                break;
        }
        // Zero-fill any tail (partial frame at stream end)
        while (idx < sampleCount && idx < buffer.Length) buffer[idx++] = 0f;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        try { _device.Stop(); } catch { }
    }
}
