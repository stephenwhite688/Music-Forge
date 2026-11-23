using AudioForge.Core;
using System;

namespace AudioForge.DSP;

/// <summary>
/// Transparent <see cref="IAudioNode"/> that keeps a rolling history of recent
/// PCM in a per-channel ring buffer for waveform display and transient
/// inspection. Allocates nothing per block after construction; snapshot reads
/// allocate on the caller's thread only.
/// </summary>
public sealed class WaveformTapNode : AudioNode
{
    private readonly int _capacityFrames;
    private int _channels;
    private float[] _data;          // channels * capacityFrames, interleaved by channel-major layout
    private int _writePos;
    private long _totalFrames;
    private int _activeChannels;

    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Number of channels to capture.</param>
    /// <param name="historySeconds">Seconds of audio retained (default 2 s).</param>
    public WaveformTapNode(int sampleRate, int channels, double historySeconds = 2.0, string name = "WaveformTap")
        : base(sampleRate, channels, name)
    {
        if (historySeconds <= 0) throw new ArgumentOutOfRangeException(nameof(historySeconds));
        _channels = Math.Max(1, channels);
        _capacityFrames = Math.Max(64, (int)(sampleRate * historySeconds));
        _data = new float[_channels * _capacityFrames];
        _activeChannels = _channels;
    }

    /// <summary>Frames of history retained.</summary>
    public int CapacityFrames => _capacityFrames;

    /// <summary>Total frames ever written since construction/reset.</summary>
    public long TotalFrames { get { lock (_data) return _totalFrames; } }

    /// <summary>Seconds of history currently retained.</summary>
    public double HistorySeconds => (double)_capacityFrames / Math.Max(1, SampleRate);

    public override void Prepare(AudioFormat format)
    {
        base.Prepare(format);
        int ch = Math.Max(1, format.Channels);
        if (ch != _activeChannels)
        {
            // Reallocate for the real channel count; history is reset.
            _activeChannels = ch;
            _channels = ch;
            _data = new float[ch * _capacityFrames];
            _writePos = 0;
            _totalFrames = 0;
        }
    }

    public override void Reset()
    {
        Array.Clear(_data);
        _writePos = 0;
        _totalFrames = 0;
    }

    public override void Process(Span<float> buffer)
    {
        int ch = Math.Max(1, Format?.Channels ?? _activeChannels);
        int frames = buffer.Length / ch;
        if (frames <= 0) return;

        for (int i = 0; i < frames; i++)
        {
            int src = i * ch;
            int dst = _writePos * ch;
            for (int c = 0; c < ch && c < _channels; c++)
                _data[dst + c] = buffer[src + c];
            _writePos = (_writePos + 1) % _capacityFrames;
        }
        _totalFrames += frames;
    }

    /// <summary>
    /// Returns the most recent <paramref name="count"/> frames of one channel
    /// in chronological order (oldest first). If fewer frames exist, the
    /// available history is returned.
    /// </summary>
    /// <param name="channel">Zero-based channel index.</param>
    /// <param name="count">Requested frames (clamped to capacity).</param>
    public float[] SnapshotChannel(int channel, int count)
    {
        if (channel < 0 || channel >= _channels)
            throw new ArgumentOutOfRangeException(nameof(channel));
        count = Math.Clamp(count, 1, _capacityFrames);

        lock (_data)
        {
            long available = Math.Min(_totalFrames, _capacityFrames);
            int n = (int)Math.Min(count, available);
            var result = new float[n];
            // _writePos is the next write slot = oldest frame when full
            int oldest = (int)((_writePos - n + _capacityFrames) % _capacityFrames);
            for (int i = 0; i < n; i++)
            {
                int pos = (oldest + i) % _capacityFrames;
                result[i] = _data[pos * _channels + channel];
            }
            return result;
        }
    }

    /// <summary>
    /// Min/max peak pairs for one channel over the most recent
    /// <paramref name="blocks"/> divisions of the retained history - the same
    /// shape used by waveform-overview "peak files" in audio editors.
    /// </summary>
    /// <returns>Interleaved min,max float pairs, length = blocks*2.</returns>
    public float[] SnapshotPeaks(int channel, int blocks)
    {
        if (channel < 0 || channel >= _channels)
            throw new ArgumentOutOfRangeException(nameof(channel));
        blocks = Math.Clamp(blocks, 1, _capacityFrames);

        lock (_data)
        {
            long available = Math.Min(_totalFrames, _capacityFrames);
            int usable = (int)Math.Min(available, blocks * 16); // at least ~16 frames per block
            var result = new float[blocks * 2];
            if (usable == 0) return result;

            int perBlock = Math.Max(1, usable / blocks);
            int start = (int)((_writePos - usable + _capacityFrames) % _capacityFrames);
            for (int b = 0; b < blocks; b++)
            {
                float min = float.MaxValue, max = float.MinValue;
                int count = 0;
                for (int i = 0; i < perBlock; i++)
                {
                    int pos = (start + b * perBlock + i) % _capacityFrames;
                    // Skip blocks beyond usable range on the last iteration edge
                    float v = _data[pos * _channels + channel];
                    if (v < min) min = v;
                    if (v > max) max = v;
                    count++;
                }
                if (count > 0) { result[b * 2] = min; result[b * 2 + 1] = max; }
                else { result[b * 2] = 0f; result[b * 2 + 1] = 0f; }
            }
            return result;
        }
    }
}
