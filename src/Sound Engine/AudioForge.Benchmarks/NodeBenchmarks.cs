using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using AudioForge.DSP;
using AudioForge.DSP.GraphicEQ;
using AudioForge.Effects;
using AudioForge.Mixing;

namespace AudioForge.Benchmarks;

public static class Program
{
    public static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}

/// <summary>Shared block: 512 frames stereo float (1024 samples) - typical callback size.</summary>
public class NodeBenchmarks
{
    private const int BlockFrames = 512;
    private const int Channels = 2;
    private float[] _block = null!;
    private GraphicEqNode _eq = null!;
    private StereoMixerNode _mixer = null!;
    private ReverbEffect _reverb = null!;
    private DelayEffect _delay = null!;
    private Resampler _srcUp = null!;
    private Resampler _srcDown = null!;
    private float[] _srcOut = null!;

    [GlobalSetup]
    public void Setup()
    {
        _block = new float[BlockFrames * Channels];
        var rng = new Random(42);
        for (int i = 0; i < _block.Length; i++) _block[i] = (float)(rng.NextDouble() * 2 - 1) * 0.5f;

        _eq = new GraphicEqNode(48000, Channels);
        for (int b = 0; b < 31; b++) _eq.SetBandGain(b, 6f); // worst case: all bands active

        _mixer = new StereoMixerNode(48000, Channels);

        _reverb = new ReverbEffect(48000, Channels, mix: 0.5f);

        _delay = new DelayEffect(48000, Channels, delayMs: 250f, feedback: 0.3f, mix: 0.4f);

        _srcUp = new Resampler(44100, 48000, Channels);
        _srcDown = new Resampler(48000, 44100, Channels);
        _srcOut = new float[BlockFrames * 2 * Channels];
    }

    [Benchmark(Description = "31-band GraphicEQ.Process (512fr stereo)")]
    public void GraphicEq() => _eq.Process(_block);

    [Benchmark(Description = "StereoMixer.Process (512fr stereo)")]
    public void StereoMixer() => _mixer.Process(_block);

    [Benchmark(Description = "ReverbEffect.Process (512fr stereo)")]
    public void Reverb() => _reverb.Process(_block);

    [Benchmark(Description = "DelayEffect.Process (512fr stereo)")]
    public void Delay() => _delay.Process(_block);

    [Benchmark(Description = "SRC 44.1k->48k (512fr in)")]
    public int SrcUp()
        => _srcUp.ProcessBlock(_block, BlockFrames, _srcOut, _srcOut.Length / Channels);

    [Benchmark(Description = "SRC 48k->44.1k (512fr in)")]
    public int SrcDown()
        => _srcDown.ProcessBlock(_block, BlockFrames, _srcOut, _srcOut.Length / Channels);
}
