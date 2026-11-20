#pragma warning disable 1591

using System;
using HelixAudio.Analysis;
using HelixAudio.DSP.Analysis;

namespace HelixAudio.Engine
{
    public class AnalysisNode : EngineNode
    {
        private readonly AnalysisBase _analyzer;
        private float _lastMetric;
        private readonly SpectrumProcessor _spectrumProcessor;

        public AnalysisBase Analyzer => _analyzer;
        public float LastMetric => _lastMetric;
        public SpectrumProcessor SpectrumProcessor => _spectrumProcessor;

        public event EventHandler<float> MetricComputed = delegate { };

        public AnalysisNode(int sampleRate, int channels, AnalysisBase analyzer, string? name = null)
            : base(sampleRate, channels, NodeType.Analyzer, name ?? "")
        {
            _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
            _lastMetric = 0.0f;
            _spectrumProcessor = new SpectrumProcessor();
        }

        public override float[] Process(float[] input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (input.Length == 0)
                throw new ArgumentException("Buffer cannot be empty.", nameof(input));
            if (input.Length % Channels != 0)
                throw new ArgumentException($"Buffer length must be a multiple of channel count ({Channels}).", nameof(input));

            try
            {
                _lastMetric = _analyzer.Analyze(input);
                MetricComputed?.Invoke(this, _lastMetric);

                VisualizerDspHub.CheckForUpdates(out var currentSettings);

                _spectrumProcessor.ProcessActiveStream(
                    input,
                    currentSettings.FftSize,
                    currentSettings.WindowProfile,
                    currentSettings.ChannelMode,
                    currentSettings.OverlapPercentage
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Analysis error: {ex.Message}");
            }

            return input;
        }

        /// <summary>
        /// Safely transfers computed real-time spectral data to an outside buffer array.
        /// Call this method from your UI visual render timer loop.
        /// </summary>
        public void GetLatestSpectrumData(Span<float> destination)
        {
            float[] currentMags = _spectrumProcessor.Magnitudes;
            if (currentMags == null) return;

            int copyLength = Math.Min(currentMags.Length, destination.Length);
            new ReadOnlySpan<float>(currentMags, 0, copyLength).CopyTo(destination);
        }

        public string GetAnalysisString()
        {
            return $"Analysis ({_analyzer.GetType().Name}): {_lastMetric:F4}";
        }
    }
}
