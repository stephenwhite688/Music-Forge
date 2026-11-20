using System;
using System.Runtime.InteropServices;
using System.Text;

namespace HelixAudio.Backend.Wasapi
{
    public static class WasapiInterop
    {
        public static readonly Guid IID_IAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
        public static readonly Guid IID_IAudioRenderClient = new Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2");
        public static readonly Guid IID_IAudioCaptureClient = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
        public static readonly Guid CLSID_MMDeviceEnumerator = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");

        [StructLayout(LayoutKind.Sequential)]
        public struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMMDeviceEnumerator
        {
            public IntPtr VTable;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMMDevice
        {
            public IntPtr VTable;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IAudioClient
        {
            public IntPtr VTable;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IAudioRenderClient
        {
            public IntPtr VTable;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IAudioCaptureClient
        {
            public IntPtr VTable;
        }

        public static WAVEFORMATEX CreateFloatWaveFormat(int sampleRate, int channels)
        {
            return new WAVEFORMATEX
            {
                wFormatTag = 3,
                nChannels = (ushort)channels,
                nSamplesPerSec = (uint)sampleRate,
                wBitsPerSample = 32,
                nBlockAlign = (ushort)(channels * 4),
                nAvgBytesPerSec = (uint)(sampleRate * channels * 4),
                cbSize = 0
            };
        }

        public static int FramesToBytes(int frames, int channels)
        {
            return frames * channels * 4;
        }

        public static int BytesToFrames(int bytes, int channels)
        {
            return bytes / (channels * 4);
        }

        public static string[] EnumerateOutputDevices()
        {
            return new string[] { "default" };
        }

        public static string[] EnumerateInputDevices()
        {
            return new string[] { "default" };
        }
    }
}
