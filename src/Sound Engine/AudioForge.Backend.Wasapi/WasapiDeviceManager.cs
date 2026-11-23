namespace AudioForge.Backend.Wasapi;
using AudioForge.Core;
using System;
using NAudio.Wave;
using NAudio.CoreAudioApi;

public class WasapiDeviceManager : IAudioDeviceManager
{
    private int _sr, _ch;
    private bool _init, _run, _exclusive;
    private string _outId, _inId;
    private WasapiOut? _wasapiOut;
    private BufferedWaveProvider? _bufferedWaveProvider;
    private float _volume = 1.0f;
    private byte[] _byteBuffer = Array.Empty<byte>();
    private int _writeCount;

    public int SampleRate => _sr;
    public int Channels => _ch;
    public string CurrentOutputDeviceId { get => _outId; set => _outId = value; }
    public string CurrentInputDeviceId { get => _inId; set => _inId = value; }

    /// <summary>
    /// Exclusive-mode WASAPI output. When true, Start() opens the endpoint in
    /// AUDCLNT_SHAREMODE_EXCLUSIVE with a ~10ms buffer and float32 at the
    /// device's native mix format — required for pro audio (low stable latency,
    /// no other app can steal the device). The engine must feed audio at the
    /// device's native sample rate; resample upstream via StreamingResampler.
    /// </summary>
    public bool ExclusiveMode
    {
        get => _exclusive;
        set => _exclusive = value;
    }
    
    // Expose buffered wave provider for buffer level checking
    public BufferedWaveProvider? BufferedWaveProvider => _bufferedWaveProvider;
    
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = value;
            if (_wasapiOut != null)
            {
                _wasapiOut.Volume = _volume;
            }
        }
    }

    public void Initialize(int sr, int ch)
    {
        _sr = sr;
        _ch = ch;
        _init = true;
    }

    public void Start()
    {
        if (!_init) throw new InvalidOperationException("Not initialized.");
        
        // Resolve the selected output endpoint; empty/null = Windows default.
        MMDevice? device = null;
        if (!string.IsNullOrEmpty(_outId))
        {
            using var en = new MMDeviceEnumerator();
            foreach (var d in en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                if (d.ID == _outId) { device = d; break; }
            if (device is null)
                AudioDiagnostics.Log("WASAPI", $"Output device '{_outId}' not found - using default");
        }
        var shareMode = _exclusive
            ? NAudio.CoreAudioApi.AudioClientShareMode.Exclusive
            : NAudio.CoreAudioApi.AudioClientShareMode.Shared;
        _wasapiOut = device is null
            ? new WasapiOut(shareMode, _exclusive ? 10 : 100)
            : new WasapiOut(device, shareMode, false, _exclusive ? 10 : 100);
        
        // Create a buffered wave provider with the actual stream format.
        // Exclusive mode must use the device's native mix format exactly.
        WaveFormat waveFormat;
        if (_exclusive && device is not null)
        {
            var mix = device.AudioClient.MixFormat;
            _sr = mix.SampleRate;
            _ch = mix.Channels;
            waveFormat = mix;
        }
        else
        {
            waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_sr, _ch);
        }
        _bufferedWaveProvider = new BufferedWaveProvider(waveFormat, TimeSpan.FromSeconds(5.0))
        {
            DiscardOnBufferOverflow = true
        };
        
        _wasapiOut.Init(_bufferedWaveProvider);
        _wasapiOut.Volume = _volume; // Set initial volume
        _wasapiOut.Play();
        _run = true;
        _writeCount = 0;
        AudioDiagnostics.Log("WASAPI", $"Started; sampleRate={_sr}; channels={_ch}; bufferSeconds={_bufferedWaveProvider.BufferDuration.TotalSeconds:F2}");
    }

    public void Stop()
    {
        _run = false;
        _wasapiOut?.Stop();
        _wasapiOut?.Dispose();
        _wasapiOut = null;
        _bufferedWaveProvider = null;
        AudioDiagnostics.Log("WASAPI", "Stopped");
    }

    public void Pause()
    {
        _wasapiOut?.Pause();
    }

    public void Resume()
    {
        _wasapiOut?.Play();
    }

    public void Write(float[] buffer)
    {
        Write(buffer, buffer.Length);
    }

    /// <summary>Writes the first <paramref name="sampleCount"/> floats.
    /// Reuses a preallocated byte buffer - realtime-safe (no per-block alloc).</summary>
    public void Write(float[] buffer, int sampleCount)
    {
        if (!_run || _bufferedWaveProvider == null)
            throw new InvalidOperationException("Not running.");

        int bytesNeeded = Math.Min(sampleCount, buffer.Length) * 4;
        if (_byteBuffer.Length < bytesNeeded)
            _byteBuffer = new byte[bytesNeeded];
        Buffer.BlockCopy(buffer, 0, _byteBuffer, 0, bytesNeeded);
        
        _bufferedWaveProvider.AddSamples(_byteBuffer, 0, bytesNeeded);
        if (++_writeCount % 200 == 0)
            AudioDiagnostics.LogRateLimited(
                "wasapi-buffer",
                "WASAPI",
                $"Buffer duration={_bufferedWaveProvider.BufferedDuration.TotalMilliseconds:F0}ms; samples={sampleCount}",
                TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// The mix-format sample rate Windows will actually render at for the
    /// selected output endpoint. Returns <paramref name="fallback"/> when the
    /// endpoint can't be queried (e.g. no device). Used to decide whether the
    /// engine must resample source audio before handing it to WASAPI.
    /// </summary>
    public int GetOutputSampleRate(int fallback)
    {
        try
        {
            using var en = new MMDeviceEnumerator();
            MMDevice? dev = null;
            if (!string.IsNullOrEmpty(_outId))
            {
                foreach (var d in en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                    if (d.ID == _outId) { dev = d; break; }
            }
            dev ??= en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return dev.AudioClient.MixFormat.SampleRate;
        }
        catch { return fallback; }
    }

    /// <summary>All active render endpoints Windows can output to
    /// (speakers, headphones, Bluetooth, USB DACs, HDMI...).</summary>
    public static IReadOnlyList<(string Id, string Name)> EnumerateOutputDevices()
    {
        var list = new List<(string, string)>();
        try
        {
            using var en = new MMDeviceEnumerator();
            foreach (var d in en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                list.Add((d.ID, d.FriendlyName));
        }
        catch { }
        return list;
    }

    public float[] Read(int sampleCount)
    {
        if (!_run) throw new InvalidOperationException("Not running.");
        // Input not implemented for now
        return new float[sampleCount];
    }
}
