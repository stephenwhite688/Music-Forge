using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AudioForge.Backend.Wasapi;

/// <summary>
/// Captures audio from a WASAPI capture endpoint (microphone, line-in, USB
/// interface, mixer input tap...) as interleaved 32-bit float PCM.
///
/// Wraps NAudio's WasapiCapture (MIT licensed). Accepts IEEE float and
/// 16-bit PCM source formats; anything else is rejected up front rather
/// than silently mis-converted.
/// </summary>
public sealed class WasapiInputCapture : IDisposable
{
    private WasapiCapture? _capture;
    private bool _disposed;

    /// <summary>Interleaved float PCM blocks. Args: buffer, frames.</summary>
    public event Action<float[], int>? AudioData;

    /// <summary>Raised when capture stops unexpectedly (device removed, etc.).</summary>
    public event EventHandler<string>? Stopped;

    public bool IsRunning => _capture is not null;

    public int SampleRate { get; private set; }
    public int Channels { get; private set; }

    /// <summary>Friendly name of the captured endpoint, for UI display.</summary>
    public string DeviceName { get; private set; } = "";

    /// <summary>
    /// Starts capture on <paramref name="deviceId"/> (NAudio MMDevice ID or
    /// friendly name), or the default capture endpoint when null.
    /// </summary>
    /// <param name="exclusive">Use exclusive-mode WASAPI (AUDCLNT_SHAREMODE_EXCLUSIVE)
    /// at the device's native format — lower, stable latency for pro recording.
    /// Other apps cannot use the device while exclusive capture runs.</param>
    public void Start(string? deviceId = null, bool exclusive = false)
    {
        Stop();
        MMDevice? device = deviceId is null ? null : FindDevice(deviceId);
        if (exclusive && device is null)
        {
            using var en = new MMDeviceEnumerator();
            device = en.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
        }
        var capture = device is null
            ? new WasapiCapture()
            : new WasapiCapture(device);
        if (exclusive)
        {
            capture.ShareMode = AudioClientShareMode.Exclusive;
            // Force the device's native mix format - exclusive mode does no
            // automatic PCM conversion, so the format must match exactly.
            capture.WaveFormat = device!.AudioClient.MixFormat;
        }

        var fmt = capture.WaveFormat;
        bool isFloat = fmt.Encoding == WaveFormatEncoding.IeeeFloat && fmt.BitsPerSample == 32;
        bool isPcm16 = fmt.Encoding == WaveFormatEncoding.Pcm && fmt.BitsPerSample == 16;
        if (!isFloat && !isPcm16)
        {
            capture.Dispose();
            throw new InvalidOperationException(
                $"WASAPI capture requires IEEE float or 16-bit PCM, got {fmt.Encoding}/{fmt.BitsPerSample}-bit.");
        }

        SampleRate = fmt.SampleRate;
        Channels = fmt.Channels;
        DeviceName = deviceId ?? "Default recording device";

        capture.DataAvailable += OnDataAvailable;
        capture.RecordingStopped += (_, e) =>
        {
            if (e.Exception is not null) Stopped?.Invoke(this, e.Exception.Message);
        };
        _capture = capture;
        capture.StartRecording();
    }

    public void Stop()
    {
        var c = _capture;
        _capture = null;
        if (c is null) return;
        c.DataAvailable -= OnDataAvailable;
        try { c.StopRecording(); } catch { }
        c.Dispose();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0 || AudioData is null) return;

        var fmt = _capture?.WaveFormat;
        if (fmt is null) return;
        int channels = Math.Max(1, fmt.Channels);

        float[] floats;
        if (fmt.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            floats = new float[e.BytesRecorded / 4];
            Buffer.BlockCopy(e.Buffer, 0, floats, 0, e.BytesRecorded);
        }
        else // 16-bit PCM
        {
            int count = e.BytesRecorded / 2;
            floats = new float[count];
            for (int i = 0; i < count; i++)
                floats[i] = BitConverter.ToInt16(e.Buffer, i * 2) / 32768f;
        }

        int frames = floats.Length / channels;
        if (frames > 0) AudioData(floats, frames);
    }

    /// <summary>All active capture endpoints Windows can record from.</summary>
    public static IReadOnlyList<(string Id, string Name)> EnumerateInputDevices()
    {
        var list = new List<(string, string)>();
        try
        {
            using var en = new MMDeviceEnumerator();
            foreach (var d in en.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                list.Add((d.ID, d.FriendlyName));
        }
        catch { }
        return list;
    }

    private static MMDevice FindDevice(string id)
    {
        using var en = new MMDeviceEnumerator();
        foreach (var d in en.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            if (d.ID == id || d.FriendlyName == id) return d;
        throw new ArgumentException($"Capture device not found: {id}");
    }

    public void Dispose() { if (_disposed) return; _disposed = true; Stop(); }
}
