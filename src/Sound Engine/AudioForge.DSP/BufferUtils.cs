namespace AudioForge.DSP;
using System;
public static class BufferUtils
{
    public static void Mix(float[] s, float[] d, int c, float g = 1f) { for (int i = 0; i < c && i < s.Length && i < d.Length; i++) d[i] += s[i] * g; }
    public static void Copy(float[] s, float[] d, int c) { Array.Copy(s, d, Math.Min(c, Math.Min(s.Length, d.Length))); }
    public static void Clear(float[] b, int c) { Array.Clear(b, 0, Math.Min(c, b.Length)); }
    public static void ApplyGain(float[] b, int c, float g) { for (int i = 0; i < c && i < b.Length; i++) b[i] *= g; }
    public static float[] Interleave(float[][] ch, int f) { if (ch == null || ch.Length == 0) return new float[0]; int c = ch.Length; float[] o = new float[f * c]; for (int fr = 0; fr < f; fr++) for (int chn = 0; chn < c; chn++) if (ch[chn] != null && fr < ch[chn].Length) o[fr * c + chn] = ch[chn][fr]; return o; }
    public static float[][] Deinterleave(float[] input, int c) { if (input == null || input.Length == 0) return new float[c][]; int f = input.Length / c; float[][] o = new float[c][]; for (int chn = 0; chn < c; chn++) { o[chn] = new float[f]; for (int fr = 0; fr < f; fr++) o[chn][fr] = input[fr * c + chn]; } return o; }
}
