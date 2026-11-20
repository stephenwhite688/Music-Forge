using System;

namespace HelixAudio.Core
{
    public class ReverbNode
    {
        private float _roomSize;
        private float _damp;
        private float _mix;
        private float _sampleRate;
        
        private readonly float[][] _combBuffers;
        private readonly int[] _combDelays;
        private readonly float[] _combGains;
        private readonly float[] _combDamp;
        private readonly float[] _combFilterState;
        private readonly int[] _combIndices;
        
        private readonly float[][] _apBuffers;
        private readonly int[] _apDelays;
        private readonly float[] _apGains;
        private readonly int[] _apIndices;
        
        private float _leftWet;
        private float _rightWet;

        public float RoomSize
        {
            get => _roomSize;
            set
            {
                _roomSize = Math.Clamp(value, 0.0f, 1.0f);
                UpdateRoomSize();
            }
        }

        public float Damp
        {
            get => _damp;
            set => _damp = Math.Clamp(value, 0.0f, 1.0f);
        }

        public float Mix
        {
            get => _mix;
            set => _mix = Math.Clamp(value, 0.0f, 1.0f);
        }

        public ReverbNode(float sampleRate, float roomSize = 0.5f, float damp = 0.5f, float mix = 0.5f)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));

            _sampleRate = sampleRate;
            _roomSize = Math.Clamp(roomSize, 0.0f, 1.0f);
            _damp = Math.Clamp(damp, 0.0f, 1.0f);
            _mix = Math.Clamp(mix, 0.0f, 1.0f);
            
            _leftWet = 0.0f;
            _rightWet = 0.0f;

            int[] combDelays = { 1117, 1187, 1277, 1331 };
            _combDelays = combDelays;
            
            int[] apDelays = { 225, 341 };
            _apDelays = apDelays;
            
            _combBuffers = new float[4][];
            _combGains = new float[4];
            _combDamp = new float[4];
            _combFilterState = new float[4];
            _combIndices = new int[4];
            
            for (int i = 0; i < 4; i++)
            {
                _combBuffers[i] = new float[combDelays[i]];
                _combIndices[i] = 0;
                _combFilterState[i] = 0.0f;
            }
            
            _apBuffers = new float[2][];
            _apGains = new float[2] { 0.5f, 0.5f };
            _apIndices = new int[2];
            
            for (int i = 0; i < 2; i++)
            {
                _apBuffers[i] = new float[apDelays[i]];
                _apIndices[i] = 0;
            }
            
            UpdateRoomSize();
        }

        public void Process(float[] buffer, int frames)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            
            if (frames <= 0)
                return;

            int sampleCount = frames * 2;
            if (buffer.Length < sampleCount)
                throw new ArgumentException("Buffer is too small for the specified frame count.");

            float damp = _damp;
            float mix = _mix;
            float oneMinusMix = 1.0f - mix;
            float leftWet = _leftWet;
            float rightWet = _rightWet;

            for (int i = 0; i < sampleCount; i += 2)
            {
                float leftInput = buffer[i];
                float rightInput = buffer[i + 1];
                
                float leftWetSample = ProcessChannel(leftInput, damp);
                float rightWetSample = ProcessChannel(rightInput, damp);
                
                float leftOutput = leftInput * oneMinusMix + leftWetSample * mix;
                float rightOutput = rightInput * oneMinusMix + rightWetSample * mix;
                
                buffer[i] = Math.Clamp(leftOutput, -1.0f, 1.0f);
                buffer[i + 1] = Math.Clamp(rightOutput, -1.0f, 1.0f);
                
                leftWet = leftWetSample;
                rightWet = rightWetSample;
            }
            
            _leftWet = leftWet;
            _rightWet = rightWet;
        }

        private float ProcessChannel(float input, float damp)
        {
            float wet = 0.0f;
            
            for (int c = 0; c < 4; c++)
            {
                int delay = _combDelays[c];
                float gain = _combGains[c];
                float[] buffer = _combBuffers[c];
                int index = _combIndices[c];
                float filterState = _combFilterState[c];
                
                float delayed = buffer[index];
                
                float filtered = delayed * (1.0f - damp) + filterState * damp;
                filterState = filtered;
                
                float newSample = input + filtered * gain;
                buffer[index] = newSample;
                
                index++;
                if (index >= delay)
                    index = 0;
                
                _combIndices[c] = index;
                _combFilterState[c] = filterState;
                
                wet += filtered;
            }
            
            for (int a = 0; a < 2; a++)
            {
                int delay = _apDelays[a];
                float gain = _apGains[a];
                float[] buffer = _apBuffers[a];
                int index = _apIndices[a];
                
                float delayed = buffer[index];
                
                float newSample = wet - gain * delayed;
                buffer[index] = wet + gain * delayed;
                wet = newSample;
                
                index++;
                if (index >= delay)
                    index = 0;
                
                _apIndices[a] = index;
            }
            
            return wet;
        }

        public void Reset()
        {
            foreach (var buffer in _combBuffers)
                Array.Clear(buffer, 0, buffer.Length);
            
            foreach (var buffer in _apBuffers)
                Array.Clear(buffer, 0, buffer.Length);
            
            Array.Clear(_combIndices, 0, _combIndices.Length);
            Array.Clear(_apIndices, 0, _apIndices.Length);
            Array.Clear(_combFilterState, 0, _combFilterState.Length);
            
            _leftWet = 0.0f;
            _rightWet = 0.0f;
        }

        private void UpdateRoomSize()
        {
            float scale = 0.5f + _roomSize * 0.5f;
            float[] baseGains = { 0.805f, 0.770f, 0.730f, 0.700f };
            
            for (int i = 0; i < 4; i++)
            {
                _combGains[i] = baseGains[i] * scale;
            }
        }
    }
}
