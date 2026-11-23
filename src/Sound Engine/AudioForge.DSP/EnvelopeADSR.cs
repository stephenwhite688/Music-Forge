using System;

namespace AudioForge.DSP
{
    public class EnvelopeADSR
    {
        private enum State
        {
            Idle,
            Attack,
            Decay,
            Sustain,
            Release
        }

        private readonly int _sampleRate;
        private State _state;
        private float _level;
        private float _attackTime;
        private float _decayTime;
        private float _sustainLevel;
        private float _releaseTime;
        private float _attackRate;
        private float _decayRate;
        private float _releaseRate;
        private bool _gate;

        public float AttackTime
        {
            get => _attackTime;
            set
            {
                _attackTime = Math.Max(0.001f, value);
                UpdateAttackRate();
            }
        }

        public float DecayTime
        {
            get => _decayTime;
            set
            {
                _decayTime = Math.Max(0.001f, value);
                UpdateDecayRate();
            }
        }

        public float SustainLevel
        {
            get => _sustainLevel;
            set => _sustainLevel = Math.Clamp(value, 0.0f, 1.0f);
        }

        public float ReleaseTime
        {
            get => _releaseTime;
            set
            {
                _releaseTime = Math.Max(0.001f, value);
                UpdateReleaseRate();
            }
        }

        public float Level => _level;

        public bool Gate
        {
            get => _gate;
            set
            {
                if (value && !_gate)
                    NoteOn();
                else if (!value && _gate)
                    NoteOff();
            }
        }

        public EnvelopeADSR(int sampleRate)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));

            _sampleRate = sampleRate;
            _state = State.Idle;
            _level = 0.0f;
            _gate = false;
            _attackTime = 0.01f;
            _decayTime = 0.1f;
            _sustainLevel = 0.7f;
            _releaseTime = 0.1f;
            UpdateAttackRate();
            UpdateDecayRate();
            UpdateReleaseRate();
        }

        public void NoteOn()
        {
            _state = State.Attack;
            _gate = true;
        }

        public void NoteOff()
        {
            if (_state != State.Idle)
            {
                _state = State.Release;
                _gate = false;
            }
        }

        public float NextSample()
        {
            switch (_state)
            {
                case State.Attack:
                    _level += _attackRate;
                    if (_level >= 1.0f)
                    {
                        _level = 1.0f;
                        _state = State.Decay;
                    }
                    break;

                case State.Decay:
                    _level -= _decayRate;
                    if (_level <= _sustainLevel)
                    {
                        _level = _sustainLevel;
                        _state = State.Sustain;
                    }
                    break;

                case State.Sustain:
                    break;

                case State.Release:
                    _level -= _releaseRate;
                    if (_level <= 0.0f)
                    {
                        _level = 0.0f;
                        _state = State.Idle;
                    }
                    break;

                case State.Idle:
                    _level = 0.0f;
                    break;
            }

            return _level;
        }

        public float[] Apply(float[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            float[] output = new float[buffer.Length];
            for (int i = 0; i < buffer.Length; i++)
            {
                float env = NextSample();
                output[i] = buffer[i] * env;
            }
            return output;
        }

        public void Reset()
        {
            _state = State.Idle;
            _level = 0.0f;
            _gate = false;
        }

        private void UpdateAttackRate()
        {
            _attackRate = 1.0f / (_attackTime * _sampleRate);
        }

        private void UpdateDecayRate()
        {
            _decayRate = 1.0f / (_decayTime * _sampleRate);
        }

        private void UpdateReleaseRate()
        {
            _releaseRate = 1.0f / (_releaseTime * _sampleRate);
        }
    }
}
