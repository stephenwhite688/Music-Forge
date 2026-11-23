using System;

namespace AudioForge.Analysis
{
    /// <summary>
    /// Generates waveform "peak files": min/max amplitude pairs per block for
    /// each channel. This is the compact overview representation audio editors
    /// use to draw waveforms of long files instantly without decoding the
    /// whole file on the UI thread.
    /// </summary>
    public static class PeakFileGenerator
    {
        /// <summary>A generated peak overview.</summary>
        public sealed class PeakFile
        {
            public int SampleRate { get; init; }
            public int Channels { get; init; }
            /// <summary>Frames covered by each min/max pair.</summary>
            public int FramesPerBlock { get; init; }
            /// <summary>Interleaved [min,max] pairs per channel block.</summary>
            public float[] Peaks { get; init; } = Array.Empty<float>();
            public int BlocksPerChannel => Channels > 0 ? Peaks.Length / (Channels * 2) : 0;
        }

        /// <summary>
        /// Builds a peak file from interleaved PCM. Each block covers
        /// <paramref name="framesPerBlock"/> frames per channel.
        /// </summary>
        public static PeakFile Generate(float[] interleaved, int channels, int sampleRate, int framesPerBlock = 512)
        {
            if (interleaved is null) throw new ArgumentNullException(nameof(interleaved));
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (framesPerBlock <= 0) throw new ArgumentOutOfRangeException(nameof(framesPerBlock));
            if (interleaved.Length % channels != 0)
                throw new ArgumentException("Buffer length must be a multiple of channel count.", nameof(interleaved));

            long frames = interleaved.Length / channels;
            int blocks = (int)((frames + framesPerBlock - 1) / framesPerBlock);
            var peaks = new float[blocks * channels * 2];

            for (long block = 0; block < blocks; block++)
            {
                long start = block * framesPerBlock;
                long end = Math.Min(start + framesPerBlock, frames);
                for (int ch = 0; ch < channels; ch++)
                {
                    float min = float.MaxValue, max = float.MinValue;
                    for (long f = start; f < end; f++)
                    {
                        float v = interleaved[f * channels + ch];
                        if (v < min) min = v;
                        if (v > max) max = v;
                    }
                    if (end == start) { min = 0f; max = 0f; }
                    int idx = (int)(block * channels * 2 + ch * 2);
                    peaks[idx] = min;
                    peaks[idx + 1] = max;
                }
            }

            return new PeakFile
            {
                SampleRate = sampleRate,
                Channels = channels,
                FramesPerBlock = framesPerBlock,
                Peaks = peaks
            };
        }
    }
}
