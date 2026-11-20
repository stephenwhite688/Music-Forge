namespace HelixAudio.Core;

using System;

/// <summary>
/// Provides methods for converting between different audio formats.
/// </summary>
public static class AudioFormatConverter
{
    /// <summary>
    /// Converts an 8-bit unsigned PCM sample to a float sample.
    /// </summary>
    /// <param name="sample">8-bit unsigned PCM sample (0-255).</param>
    /// <returns>Float sample (-1.0 to 1.0).</returns>
    public static float Convert8BitToFloat(byte sample) => (sample / 127.5f) - 1.0f;

    /// <summary>
    /// Converts a float sample to an 8-bit unsigned PCM sample.
    /// </summary>
    /// <param name="sample">Float sample (-1.0 to 1.0).</param>
    /// <returns>8-bit unsigned PCM sample (0-255).</returns>
    public static byte ConvertFloatTo8Bit(float sample) { float c = Clamp(sample, -1f, 1f); return (byte)((c + 1f) * 127.5f); }

    /// <summary>
    /// Converts a 16-bit PCM sample to a float sample.
    /// </summary>
    /// <param name="sample">16-bit PCM sample (-32768 to 32767).</param>
    /// <returns>Float sample (-1.0 to 1.0).</returns>
    public static float Convert16BitToFloat(short sample) => sample / 32768.0f;

    /// <summary>
    /// Converts a float sample to a 16-bit PCM sample.
    /// </summary>
    /// <param name="sample">Float sample (-1.0 to 1.0).</param>
    /// <returns>16-bit PCM sample (-32768 to 32767).</returns>
    public static short ConvertFloatTo16Bit(float sample) { float c = Clamp(sample, -1f, 1f); return (short)(c * 32767f); }

    /// <summary>
    /// Converts a 32-bit PCM sample to a float sample.
    /// </summary>
    /// <param name="sample">32-bit PCM sample (-2147483648 to 2147483647).</param>
    /// <returns>Float sample (-1.0 to 1.0).</returns>
    public static float Convert32BitToFloat(int sample) => sample / 2147483648.0f;

    /// <summary>
    /// Converts a float sample to a 32-bit PCM sample.
    /// </summary>
    /// <param name="sample">Float sample (-1.0 to 1.0).</param>
    /// <returns>32-bit PCM sample (-2147483648 to 2147483647).</returns>
    public static int ConvertFloatTo32Bit(float sample) { float c = Clamp(sample, -1f, 1f); return (int)(c * 2147483647f); }

    /// <summary>
    /// Clamps a value between min and max.
    /// </summary>
    private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

    /// <summary>
    /// Converts a byte buffer to float samples.
    /// </summary>
    /// <param name="input">Input byte buffer.</param>
    /// <param name="bits">Bits per sample (8, 16, or 32).</param>
    /// <returns>Float array.</returns>
    public static float[] ConvertToFloat(byte[] input, int bits)
    {
        int bps = bits / 8; int count = input.Length / bps; float[] output = new float[count];
        for (int i = 0; i < count; i++)
        {
            if (bits == 8) output[i] = Convert8BitToFloat(input[i]);
            else if (bits == 16) output[i] = Convert16BitToFloat(BitConverter.ToInt16(input, i * 2));
            else output[i] = Convert32BitToFloat(BitConverter.ToInt32(input, i * 4));
        }
        return output;
    }

    /// <summary>
    /// Converts float samples to a byte buffer.
    /// </summary>
    /// <param name="input">Input float array.</param>
    /// <param name="bits">Bits per sample (8, 16, or 32).</param>
    /// <returns>Byte buffer.</returns>
    public static byte[] ConvertFromFloat(float[] input, int bits)
    {
        int bps = bits / 8; byte[] output = new byte[input.Length * bps];
        for (int i = 0; i < input.Length; i++)
        {
            if (bits == 8) output[i] = ConvertFloatTo8Bit(input[i]);
            else if (bits == 16) BitConverter.TryWriteBytes(new Span<byte>(output, i * 2, 2), ConvertFloatTo16Bit(input[i]));
            else BitConverter.TryWriteBytes(new Span<byte>(output, i * 4, 4), ConvertFloatTo32Bit(input[i]));
        }
        return output;
    }
}
