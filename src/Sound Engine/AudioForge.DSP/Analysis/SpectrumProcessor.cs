#pragma warning disable 1591

using System;

namespace AudioForge.DSP.Analysis
{
    public class SpectrumProcessor
    {
        private float[]? _windowProfile;
        private float[]? _midBuffer;
        private float[]? _sideBuffer;
        private float[]? _leftUnwrapped;
        private float[]? _rightUnwrapped;

        private int _currentSize;
        private int _currentWindowProfileType;
        private float[] _magnitudes;

        public int Size => _currentSize;
        public float[] Magnitudes => _magnitudes;

        public SpectrumProcessor()
        {
            _currentSize = 2048;
            _currentWindowProfileType = 1; // Hann
            _magnitudes = new float[_currentSize / 2];
            RebuildWindowProfile(_currentWindowProfileType);
        }

        /// <summary>
        /// Real-time entry point called by the processing node to execute the STFT with explicit parameter configurations.
        /// </summary>
        public void ProcessActiveStream(float[] interleavedStereoBuffer, int fftSize, int windowProfile, int channelMode, double overlapPercentage)
        {
            if (interleavedStereoBuffer == null || interleavedStereoBuffer.Length == 0)
                return;

            // 1. Check if the parameters passed down from the engine thread have changed
            if (_currentSize != fftSize || _currentWindowProfileType != windowProfile)
            {
                _currentSize = fftSize;
                _currentWindowProfileType = windowProfile;
                _magnitudes = new float[_currentSize / 2];
                RebuildWindowProfile(_currentWindowProfileType);
            }

            // 2. Perform safe de-interleaving out to dedicated signal arrays
            int frameCount = interleavedStereoBuffer.Length / 2;
            EnsureBuffersAllocated(frameCount);

            for (int i = 0; i < frameCount; i++)
            {
                _leftUnwrapped![i] = interleavedStereoBuffer[i * 2];
                _rightUnwrapped![i] = interleavedStereoBuffer[i * 2 + 1];
            }

            // 3. Matrix Isolation Layer (Channel selections)
            float[] processingChannelTarget = _leftUnwrapped!;

            // channelMode maps: 0=Stereo, 1=LeftOnly, 2=RightOnly, 3=MidMonoSum, 4=SideStereoDifference
            if (channelMode == 1)
            {
                processingChannelTarget = _leftUnwrapped!;
            }
            else if (channelMode == 2)
            {
                processingChannelTarget = _rightUnwrapped!;
            }
            else if (channelMode == 3 || channelMode == 4 || channelMode == 0)
            {
                FFT.DeconstructMidSide(_leftUnwrapped!, _rightUnwrapped!, _midBuffer!, _sideBuffer!);
                processingChannelTarget = (channelMode == 4) ? _sideBuffer! : _midBuffer!;
            }

            // 4. Compute sliding window STFT with direct magnitude mapping calculations
            FFT.ComputeSTFT(
                processingChannelTarget,
                _currentSize,
                (float)overlapPercentage,
                _windowProfile!,
                (realFrame, imagFrame) =>
                {
                    for (int i = 0; i < _magnitudes.Length; i++)
                    {
                        float r = realFrame[i];
                        float im = imagFrame[i];
                        _magnitudes[i] = MathF.Sqrt(r * r + im * im) / _currentSize;
                    }
                });
        }

        private void RebuildWindowProfile(int windowProfileType)
        {
            WindowType targetType = windowProfileType switch
            {
                2 => WindowType.Hamming,
                3 => WindowType.BlackmanHarris,
                0 => WindowType.Rectangular,
                1 => WindowType.Hann,
                _ => WindowType.Hann
            };

            _windowProfile = WindowFunctions.GenerateProfile(targetType, _currentSize);
        }

        private void EnsureBuffersAllocated(int frameCount)
        {
            if (_midBuffer == null || _midBuffer.Length != frameCount)
            {
                _midBuffer = new float[frameCount];
                _sideBuffer = new float[frameCount];
                _leftUnwrapped = new float[frameCount];
                _rightUnwrapped = new float[frameCount];
            }
        }
    }
}
