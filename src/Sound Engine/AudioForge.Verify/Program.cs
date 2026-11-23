using AudioForge.Core;
using AudioForge.DSP;
using AudioForge.DSP.GraphicEQ;
using AudioForge.Effects;
using AudioForge.Mixing;
using System.Text.Json.Nodes;

// AudioForge DSP verification harness.
// Renders known signals through engine nodes and asserts correctness.
// Exit code 0 = all checks pass.

const int SR = 48000;
const int CH = 2;
int failures = 0;

void Check(string name, bool ok, string detail = "")
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL"),-4} {name}{(detail.Length > 0 ? $"  ({detail})" : "")}");
    if (!ok) failures++;
}

float[] Sine(double freq, int frames, double amp = 0.5)
{
    var b = new float[frames * CH];
    for (int i = 0; i < frames; i++)
        for (int c = 0; c < CH; c++)
            b[i * CH + c] = (float)(amp * Math.Sin(2 * Math.PI * freq * i / SR));
    return b;
}

AudioFormat NodeFmt = new(SR, CH, true, 32);

void Prep(IAudioNode n) { n.Reset(); n.Prepare(NodeFmt); }

double Rms(float[] b) { double s = 0; foreach (var v in b) s += v * v; return Math.Sqrt(s / b.Length); }

// ---------- 1. Mixer unity gain ----------
{
    var sig = Sine(1000, SR);
    var m = new StereoMixerNode(SR, CH);
    Prep(m);
    m.Process(sig);
    double ratio = Rms(sig) / Rms(Sine(1000, SR));
    Check("Mixer unity gain", Math.Abs(ratio - 1.0) < 0.01, $"ratio={ratio:F4}");
}

// ---------- 2. Mixer strip gain 0.5 ----------
{
    var input = Sine(1000, SR);
    var m = new StereoMixerNode(SR, CH);
    Prep(m);
    m.Strips[0].Gain = 0.5f; m.Strips[1].Gain = 0.5f;
    m.Process(input);
    double expected = 0.5, got = Rms(input);
    Check("Mixer gain 0.5", Math.Abs(got - expected * Rms(Sine(1000, SR))) / (expected * Rms(Sine(1000, SR))) < 0.02,
        $"rms={got:F4}");
}

// ---------- 3. Mixer mute ----------
{
    var input = Sine(1000, SR);
    var m = new StereoMixerNode(SR, CH);
    Prep(m);
    m.Strips[0].Mute = true; m.Strips[1].Mute = true;
    m.Process(input);
    Check("Mixer mute -> silence", Rms(input) < 1e-4, $"rms={Rms(input):E2}");
}

// ---------- 4. Graphic EQ: flat = transparent, boost = louder ----------
{
    var flat = new GraphicEqNode(SR, CH);
    Prep(flat);
    var sig = Sine(1000, SR / 4);
    var a = (float[])sig.Clone();
    flat.Process(a);
    double flatRatio = Rms(a) / Rms(sig);
    Check("EQ flat transparent", Math.Abs(flatRatio - 1.0) < 0.05, $"ratio={flatRatio:F4}");

    var boosted = new GraphicEqNode(SR, CH);
    Prep(boosted);
    int band1k = 0;
    for (int i = 0; i < GraphicEqNode.Iso31Frequencies.Length; i++)
        if (Math.Abs(GraphicEqNode.Iso31Frequencies[i] - 1000) < 1) band1k = i;
    boosted.SetBandGain(band1k, 12f);
    var b = (float[])sig.Clone();
    boosted.Process(b);
    double boostRatio = Rms(b) / Rms(sig);
    Check("EQ +12dB@1kHz boosts", boostRatio > 1.5, $"ratio={boostRatio:F3} (band {band1k} = {GraphicEqNode.Iso31Frequencies[band1k]}Hz)");
}

// ---------- 5. EQ state roundtrip ----------
{
    var eq = new GraphicEqNode(SR, CH);
    Prep(eq);
    eq.SetBandGain(10, -7.5f);
    eq.TrySetParameter("output.db", -3f);
    var state = eq.GetState();

    var eq2 = new GraphicEqNode(SR, CH);
    Prep(eq2);
    eq2.SetState(state);
    float v;
    bool got = eq2.TryGetParameter("band.gain[10]", out v) || eq2.TryGetParameter("band.gain.10", out v);
    Check("EQ state roundtrip", got && Math.Abs(v - (-7.5f)) < 0.01, $"band10={v}");
}

// ---------- 6. DelayEffect: impulse echoes at delay time ----------
{
    int delaySamples = SR / 10; // 100ms
    var d = new DelayEffect(SR, CH, delayMs: 100f, feedback: 0f, mix: 1f);
    Prep(d);
    var imp = new float[SR / 2 * CH];
    imp[0] = 1f; imp[1] = 1f;
    d.Process(imp);
    // find first non-zero after the delay region
    int firstEcho = -1;
    for (int i = delaySamples * CH - 4; i < imp.Length; i++)
        if (Math.Abs(imp[i]) > 0.01) { firstEcho = i / CH; break; }
    Check("Delay echoes at ~100ms", firstEcho >= delaySamples - 2 && firstEcho <= delaySamples + 8,
        $"echo at frame {firstEcho} (expect ~{delaySamples})");
}

// ---------- 7. Resampler: frequency + level preservation ----------
foreach (var (inRate, outRate) in new[] { (44100, 48000), (48000, 44100), (96000, 44100), (44100, 44100) })
{
    var rs = new Resampler(inRate, outRate, CH);
    int inFrames = inRate / 5; // 200ms - enough crossings for stable freq estimate
    var input = new float[inFrames * CH];
    for (int i = 0; i < inFrames; i++)
        for (int c = 0; c < CH; c++)
            input[i * CH + c] = (float)(0.5 * Math.Sin(2 * Math.PI * 1000 * i / inRate));

    // ProcessBlock writes each call's output at offset 0 - collect blocks.
    int capFrames = (int)(inFrames * (double)outRate / inRate * 1.2 + 256);
    var blockOut = new float[capFrames * CH];
    var collected = new List<float>(capFrames * CH);
    var inBuf = new float[512 * CH];
    int remaining = inFrames;
    while (remaining > 0)
    {
        int n = Math.Min(512, remaining);
        Array.Copy(input, (inFrames - remaining) * CH, inBuf, 0, n * CH);
        int got = rs.ProcessBlock(inBuf, n, blockOut, capFrames);
        for (int i = 0; i < got * CH; i++) collected.Add(blockOut[i]);
        remaining -= n;
    }
    int fg;
    while ((fg = rs.Flush(blockOut, capFrames)) > 0)
        for (int i = 0; i < fg * CH; i++) collected.Add(blockOut[i]);
    var output = collected.ToArray();
    int produced = output.Length / CH;

    // measure zero-crossing frequency on channel 0, middle 80% only
    // (kernel edges at both ends distort the first/last few crossings)
    int startF = produced / 10, endF = produced * 9 / 10;
    int crossings = 0;
    for (int f = startF + 1; f < endF; f++)
    {
        int i = f * CH;
        if (output[i - CH] <= 0 && output[i] > 0) crossings++;
    }
    double seconds = (double)(endF - startF) / outRate;
    double freq = crossings / seconds;
    double rms = 0;
    for (int i = 0; i < produced * CH; i++) rms += output[i] * output[i];
    rms = Math.Sqrt(rms / (produced * CH));
    bool okFreq = Math.Abs(freq - 1000) < 15;
    bool okRms = Math.Abs(rms - 0.3536) < 0.02;
    Check($"SRC {inRate}->{outRate}", okFreq && okRms, $"freq={freq:F1}Hz rms={rms:F4} frames={produced}");
}

// ---------- 8. Parameter get/set on nodes ----------
{
    var d = new DelayEffect(SR, CH);
    Prep(d);
    float dv = 0, gv = 0;
    Check("Delay param set/get", d.TrySetParameter("delay.ms", 250f) && d.TryGetParameter("delay.ms", out dv) && Math.Abs(dv - 250) < 0.1, $"delayMs={dv}");
    var eq = new GraphicEqNode(SR, CH);
    Prep(eq);
    Check("EQ band param", eq.TrySetParameter("band.gain[5]", -4f) && eq.TryGetParameter("band.gain[5]", out gv) && Math.Abs(gv - (-4)) < 0.01, $"gain={gv}");
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
return failures;
