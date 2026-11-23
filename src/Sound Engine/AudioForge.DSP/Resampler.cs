using System;
using System.Collections.Generic;

namespace AudioForge.DSP
{
    /// <summary>
    /// Streaming per-channel windowed-sinc resampler with anti-aliasing.
    ///
    /// Replaces the original naive resampler which treated interleaved
    /// multichannel audio as a single stream and had no anti-alias filter.
    /// This implementation:
    /// - processes each channel independently (correct for interleaved PCM),
    /// - uses a Blackman-windowed sinc kernel scaled to the lower of the two
    ///   Nyquist frequencies (proper anti-alias on downsample, clean
    ///   interpolation on upsample),
    /// - carries fractional phase and unconsumed input across blocks, so it
    ///   is a true streaming resampler suitable for real-time boundaries.
    ///
    /// Not an <see cref="IAudioNode"/>: resampling changes the frame count,
    /// which the in-place node contract cannot express. Use it at stream
    /// boundaries (device rate conversion, broadcast, offline render).
    /// </summary>
    public sealed class Resampler
    {
        /// <summary>Sinc kernel radius in input samples (2*Radius+1 taps).</summary>
        public const int Radius = 32;

        private readonly int _inputRate;
        private readonly int _outputRate;
        private readonly int _channels;
        private readonly double _ratio;   // input samples per output sample
        private readonly double _beta;    // 2 * cutoff, in input-sample units

        private sealed class ChannelState
        {
            /// <summary>Stream samples; Samples[i] is absolute index Base+i.</summary>
            public readonly List<float> Samples = new(4096);
            public long Base;
            public double NextPos;         // absolute input position of next output
        }

        private readonly ChannelState[] _states;
        private float[][] _inScratch = Array.Empty<float[]>();
        private float[][] _outScratch = Array.Empty<float[]>();

        /// <param name="inputRate">Source sample rate in Hz.</param>
        /// <param name="outputRate">Target sample rate in Hz.</param>
        /// <param name="channels">Channel count of the interleaved streams.</param>
        public Resampler(int inputRate, int outputRate, int channels)
        {
            if (inputRate <= 0) throw new ArgumentException("Input rate must be positive.", nameof(inputRate));
            if (outputRate <= 0) throw new ArgumentException("Output rate must be positive.", nameof(outputRate));
            if (channels <= 0) throw new ArgumentException("Channels must be positive.", nameof(channels));

            _inputRate = inputRate;
            _outputRate = outputRate;
            _channels = channels;
            _ratio = (double)inputRate / outputRate;
            // Cutoff = min(inputNyq, outputNyq) expressed per input sample.
            _beta = 2.0 * Math.Min(inputRate, outputRate) / (2.0 * inputRate);

            _states = new ChannelState[channels];
            for (int c = 0; c < channels; c++) _states[c] = new ChannelState();
        }

        public int InputRate => _inputRate;
        public int OutputRate => _outputRate;
        public int Channels => _channels;

        /// <summary>Input samples consumed per output sample.</summary>
        public double Ratio => _ratio;

        /// <summary>Algorithmic delay in input samples (~kernel radius).</summary>
        public int LatencyInputSamples => Radius;

        /// <summary>
        /// Resamples one interleaved block. Returns the number of output frames
        /// written to <paramref name="output"/> (may be less than capacity when
        /// the internal phase needs more input; call Flush() at end of stream).
        /// </summary>
        /// <param name="input">Interleaved input, inputFrames*Channels samples.</param>
        /// <param name="inputFrames">Frames in <paramref name="input"/>.</param>
        /// <param name="output">Interleaved output buffer.</param>
        /// <param name="maxOutputFrames">Capacity of <paramref name="output"/> in frames.</param>
        public int ProcessBlock(float[] input, int inputFrames, float[] output, int maxOutputFrames)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));
            if (output is null) throw new ArgumentNullException(nameof(output));
            if (inputFrames < 0) throw new ArgumentOutOfRangeException(nameof(inputFrames));
            if (maxOutputFrames < 0) throw new ArgumentOutOfRangeException(nameof(maxOutputFrames));
            if (inputFrames * _channels > input.Length)
                throw new ArgumentException("Input buffer too small for declared frames.", nameof(input));
            if (maxOutputFrames * _channels > output.Length)
                throw new ArgumentException("Output buffer too small for declared frames.", nameof(output));

            EnsureScratch(inputFrames, maxOutputFrames);
            Deinterleave(input, inputFrames);

            // All channels share phase and availability, so they produce the
            // same frame count; channel 0 decides.
            int produced = ProcessChannel(_states[0], _inScratch[0], inputFrames,
                _outScratch[0], maxOutputFrames);

            for (int c = 1; c < _channels; c++)
                ProcessChannel(_states[c], _inScratch[c], inputFrames,
                    _outScratch[c], produced);

            Interleave(output, produced);
            return produced;
        }

        private bool _flushStarted;

        /// <summary>
        /// Feeds a tail of silence so filter-ring contents are flushed.
        /// Call after the last ProcessBlock until it returns 0.
        /// </summary>
        public int Flush(float[] output, int maxOutputFrames)
        {
            EnsureScratch(0, maxOutputFrames);
            if (!_flushStarted)
            {
                // Append enough zeros for the kernel's remaining lookahead so
                // the tail rings down instead of edge-fading.
                _flushStarted = true;
                int tail = Radius + (int)Math.Ceiling(_ratio) + 8;
                foreach (var st in _states)
                    for (int i = 0; i < tail; i++)
                        st.Samples.Add(0f);
            }
            for (int c = 0; c < _channels; c++)
                Array.Clear(_inScratch[c], 0, _inScratch[c].Length);

            int produced = ProcessChannel(_states[0], _inScratch[0], 0,
                _outScratch[0], maxOutputFrames);
            for (int c = 1; c < _channels; c++)
                ProcessChannel(_states[c], _inScratch[c], 0,
                    _outScratch[c], produced);

            Interleave(output, produced);
            return produced;
        }

        /// <summary>Frames of output expected for a given input frame count (approximate).</summary>
        public int EstimateOutputFrames(int inputFrames)
            => (int)Math.Ceiling(inputFrames / _ratio) + 2;

        public void Reset()
        {
            _flushStarted = false;
            foreach (var s in _states)
            {
                s.Samples.Clear();
                s.Base = 0;
                s.NextPos = 0;
            }
        }

        // ---- internals ----

        private void EnsureScratch(int inFrames, int outFrames)
        {
            if (_inScratch.Length == 0 || _inScratch[0].Length < inFrames)
            {
                int cap = Math.Max(1024, inFrames);
                _inScratch = NewChannelBuffers(cap);
                _outScratch = NewChannelBuffers(Math.Max(1024, outFrames));
            }
            else if (_outScratch[0].Length < outFrames)
            {
                _outScratch = NewChannelBuffers(Math.Max(1024, outFrames));
            }
        }

        private float[][] NewChannelBuffers(int frames)
        {
            var bufs = new float[_channels][];
            for (int c = 0; c < _channels; c++) bufs[c] = new float[frames];
            return bufs;
        }

        private void Deinterleave(float[] input, int frames)
        {
            for (int f = 0; f < frames; f++)
            {
                int src = f * _channels;
                for (int c = 0; c < _channels; c++)
                    _inScratch[c][f] = input[src + c];
            }
        }

        private void Interleave(float[] output, int frames)
        {
            for (int f = 0; f < frames; f++)
            {
                int dst = f * _channels;
                for (int c = 0; c < _channels; c++)
                    output[dst + c] = _outScratch[c][f];
            }
        }

        /// <summary>
        /// Produces up to maxOut output samples for one channel. Appends the
        /// input block to the channel's stream, evaluates the windowed-sinc
        /// kernel at each output position, then trims consumed history.
        /// </summary>
        private int ProcessChannel(ChannelState st, float[] input, int inputFrames,
            float[] output, int maxOut)
        {
            // Append this block's samples to the channel stream.
            for (int i = 0; i < inputFrames; i++)
                st.Samples.Add(input[i]);

            long count = st.Samples.Count;
            long availEnd = st.Base + count; // one past last valid absolute index
            int produced = 0;

            while (produced < maxOut)
            {
                double pos = st.NextPos;
                int center = (int)Math.Floor(pos);
                // Highest input index the kernel touches.
                long needIdx = (long)center + Radius + 1;
                if (availEnd <= needIdx) break; // not enough lookahead yet

                double sum = 0.0, wsum = 0.0;
                int firstTap = center - Radius;
                for (int n = firstTap; n <= center + Radius; n++)
                {
                    long idx = n - st.Base;
                    if (idx < 0 || idx >= count) continue;

                    double d = pos - n;
                    double x = d * _beta;
                    double sinc = Math.Abs(x) < 1e-9 ? 1.0 : Math.Sin(Math.PI * x) / (Math.PI * x);
                    // Blackman window across the tap span.
                    double t = (double)(n - firstTap) / (2.0 * Radius);
                    double w = 0.42 - 0.5 * Math.Cos(2.0 * Math.PI * t) + 0.08 * Math.Cos(4.0 * Math.PI * t);
                    double weight = sinc * w;
                    sum += st.Samples[(int)idx] * weight;
                    wsum += weight;
                }

                output[produced] = wsum > 1e-9 ? (float)(sum / wsum) : 0f;
                st.NextPos += _ratio;
                produced++;
            }

            // Trim history that can no longer be needed: anything before
            // floor(NextPos) - Radius - slack.
            long keepFrom = (long)Math.Floor(st.NextPos) - Radius - 16;
            if (keepFrom > st.Base)
            {
                int drop = (int)Math.Min(keepFrom - st.Base, count);
                if (drop > 0)
                {
                    st.Samples.RemoveRange(0, drop);
                    st.Base += drop;
                }
            }

            return produced;
        }
    }
}
