using System;

namespace HelixAudio.Engine
{
    public struct EngineDspSettings
    {
        public int DisplayMode { get; set; }
        public int ProbeSource { get; set; }
        public int FftSize { get; set; }
        public int WindowProfile { get; set; }
        public double OverlapPercentage { get; set; }
        public bool IsLinearScale { get; set; }
        public int WeightingCurve { get; set; }
        public int BandCount { get; set; }
        public int ChannelMode { get; set; }
        public bool VectorscopePhaseSync { get; set; }
    }

    public static class VisualizerDspHub
    {
        private static readonly object _lock = new object();
        private static EngineDspSettings _currentSettings;
        private static bool _isDirty;

        static VisualizerDspHub()
        {
            _currentSettings = new EngineDspSettings
            {
                FftSize = 2048,
                WindowProfile = 1, // Hann
                ChannelMode = 0,   // Stereo
                OverlapPercentage = 0.5
            };
        }

        public static void UpdateSettings(EngineDspSettings settings)
        {
            lock (_lock)
            {
                _currentSettings = settings;
                _isDirty = true;
            }
        }

        public static bool CheckForUpdates(out EngineDspSettings settings)
        {
            lock (_lock)
            {
                settings = _currentSettings;
                bool wasDirty = _isDirty;
                _isDirty = false;
                return wasDirty;
            }
        }
    }
}
