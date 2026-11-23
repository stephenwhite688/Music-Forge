using AudioForge.Core;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace AudioForge.Effects
{
    /// <summary>
    /// Freeverb-style reverb as an <see cref="IAudioNode"/>: four parallel
    /// feedback combs feed two series allpasses, mixed with the dry signal.
    /// Processes in place with preallocated scratch - no per-block allocation.
    /// </summary>
    public class ReverbEffect : AudioNode
    {
        private readonly CombFilter[] _combs;
        private readonly AllpassFilter[] _allpasses;
        private float _mix;

        // Scratch buffers, grown lazily to the largest block seen.
        private float[] _src = Array.Empty<float>();
        private float[] _wet = Array.Empty<float>();
        private float[] _tmp = Array.Empty<float>();

        public float Mix
        {
            get => _mix;
            set => _mix = Math.Clamp(value, 0.0f, 1.0f);
        }

        public ReverbEffect(int sampleRate, int channels, float mix = 0.5f, string name = "Reverb")
            : base(sampleRate, channels, name)
        {
            _mix = Math.Clamp(mix, 0.0f, 1.0f);

            int[] combDelays =
            {
                (int)(29.7f * sampleRate / 1000.0f),
                (int)(37.1f * sampleRate / 1000.0f),
                (int)(41.1f * sampleRate / 1000.0f),
                (int)(43.7f * sampleRate / 1000.0f)
            };

            float[] combGains = { 0.805f, 0.770f, 0.730f, 0.700f };

            _combs = new CombFilter[4];
            for (int i = 0; i < 4; i++)
                _combs[i] = new CombFilter(channels, combDelays[i], combGains[i]);

            int[] allpassDelays =
            {
                (int)(5.0f * sampleRate / 1000.0f),
                (int)(1.7f * sampleRate / 1000.0f)
            };
            const float allpassGain = 0.5f;

            _allpasses = new AllpassFilter[2];
            for (int i = 0; i < 2; i++)
                _allpasses[i] = new AllpassFilter(channels, allpassDelays[i], allpassGain);
        }

        private void EnsureScratch(int length)
        {
            if (_src.Length < length)
            {
                _src = new float[length];
                _wet = new float[length];
                _tmp = new float[length];
            }
        }

        public override void Process(Span<float> buffer)
        {
            if (buffer.Length == 0) return;
            EnsureScratch(buffer.Length);

            int channels = Math.Max(1, Format?.Channels ?? Channels);
            if (buffer.Length % channels != 0) return;

            // Preserve the dry signal; the combs read from the copy so the
            // input span stays intact for the final mix.
            buffer.CopyTo(_src);
            Array.Clear(_wet, 0, buffer.Length);

            var wet = _wet.AsSpan(0, buffer.Length);
            var tmp = _tmp.AsSpan(0, buffer.Length);
            var src = _src.AsSpan(0, buffer.Length);

            foreach (var comb in _combs)
            {
                comb.Process(src, tmp);
                for (int i = 0; i < wet.Length; i++)
                    wet[i] += tmp[i];
            }
            float invCombs = 1f / _combs.Length;
            for (int i = 0; i < wet.Length; i++)
                wet[i] *= invCombs;

            foreach (var allpass in _allpasses)
                allpass.Process(wet, wet);

            float mix = _mix;
            float oneMinusMix = 1f - mix;
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = Math.Clamp(src[i] * oneMinusMix + wet[i] * mix, -1f, 1f);
        }

        public override IReadOnlyList<AudioParameterInfo> GetParameterInfo() => _params;

        private static readonly AudioParameterInfo[] _params =
        {
            new("mix", 0f, 1f, 0.5f),
        };

        public override bool TryGetParameter(string name, out float value)
        {
            value = name == "mix" ? Mix : float.NaN;
            return !float.IsNaN(value);
        }

        public override bool TrySetParameter(string name, float value)
        {
            if (name != "mix") return false;
            Mix = value;
            return true;
        }

        public override JsonObject GetState() => new() { ["mix"] = Mix };

        public override void SetState(JsonObject state)
        {
            if (state?["mix"] is JsonValue m) Mix = m.GetValue<float>();
        }

        public override void Reset()
        {
            foreach (var comb in _combs) comb.Reset();
            foreach (var allpass in _allpasses) allpass.Reset();
        }
    }
}
