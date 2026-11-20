using System;

namespace HelixAudio.Effects
{
    public class ReverbEffect : EffectBase
    {
        private readonly CombFilter[] _combs;
        private readonly AllpassFilter[] _allpasses;
        private float _mix;

        public float Mix
        {
            get => _mix;
            set => _mix = Math.Clamp(value, 0.0f, 1.0f);
        }

        public ReverbEffect(int sampleRate, int channels, float mix = 0.5f)
            : base(sampleRate, channels)
        {
            _mix = Math.Clamp(mix, 0.0f, 1.0f);

            int[] combDelays = {
                (int)(29.7f * sampleRate / 1000.0f),
                (int)(37.1f * sampleRate / 1000.0f),
                (int)(41.1f * sampleRate / 1000.0f),
                (int)(43.7f * sampleRate / 1000.0f)
            };

            float[] combGains = { 0.805f, 0.770f, 0.730f, 0.700f };

            _combs = new CombFilter[4];
            for (int i = 0; i < 4; i++)
                _combs[i] = new CombFilter(sampleRate, channels, combDelays[i], combGains[i]);

            int[] allpassDelays = {
                (int)(5.0f * sampleRate / 1000.0f),
                (int)(1.7f * sampleRate / 1000.0f)
            };
            float allpassGain = 0.5f;

            _allpasses = new AllpassFilter[2];
            for (int i = 0; i < 2; i++)
                _allpasses[i] = new AllpassFilter(sampleRate, channels, allpassDelays[i], allpassGain);
        }

        public override float[] Process(float[] input)
        {
            ValidateInput(input);

            float[] output = new float[input.Length];
            float[] wet = new float[input.Length];
            float mix = _mix;
            float oneMinusMix = 1.0f - mix;

            for (int i = 0; i < _combs.Length; i++)
            {
                float[] combOutput = _combs[i].Process(input);
                for (int j = 0; j < wet.Length; j++)
                    wet[j] += combOutput[j];
            }

            for (int i = 0; i < wet.Length; i++)
                wet[i] /= _combs.Length;

            foreach (var allpass in _allpasses)
                wet = allpass.Process(wet);

            for (int i = 0; i < input.Length; i++)
            {
                float sample = input[i] * oneMinusMix + wet[i] * mix;
                output[i] = ClampSample(sample);
            }

            return output;
        }

        public override void Reset()
        {
            foreach (var comb in _combs)
                comb.Reset();
            foreach (var allpass in _allpasses)
                allpass.Reset();
        }
    }
}
