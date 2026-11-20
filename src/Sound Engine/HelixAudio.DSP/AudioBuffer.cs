namespace HelixAudio.DSP;
using System;
public class AudioBuffer
{
    private float[] _data; private int _len;
    public int Length => _len;
    public Span<float> Data => _data.AsSpan(0, _len);
    public AudioBuffer(int cap) { _data = new float[cap]; _len = 0; }
    public void Write(float[] samples, int count) { Array.Copy(samples, 0, _data, 0, count); _len = count; }
    public void Write(Span<float> samples) { samples.CopyTo(_data); _len = samples.Length; }
    public void Clear() { Array.Clear(_data, 0, _data.Length); _len = 0; }
}
