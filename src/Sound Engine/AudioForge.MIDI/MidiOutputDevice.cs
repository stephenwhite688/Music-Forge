using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AudioForge.MIDI;

/// <summary>
/// MIDI output device via WinMM (winmm.dll midiOut* API).
/// Sends short messages (note on/off, CC, program change, pitch bend) and
/// SysEx via <see cref="SendLongData"/>.
/// </summary>
public sealed class MidiOutputDevice : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    public int DeviceId { get; }
    public string Name { get; }
    public bool IsOpen => _handle != IntPtr.Zero;

    private MidiOutputDevice(int id, string name) { DeviceId = id; Name = name; }

    public static IReadOnlyList<(int Id, string Name)> GetDevices()
    {
        var list = new List<(int, string)>();
        int count = midiOutGetNumDevs();
        for (int i = 0; i < count; i++)
            list.Add((i, GetDevName(i)));
        return list;
    }

    private static string GetDevName(int id)
    {
        var caps = new MIDIOUTCAPS();
        return midiOutGetDevCaps(id, ref caps, Marshal.SizeOf<MIDIOUTCAPS>()) == 0
            ? caps.szPname : $"MIDI Out {id}";
    }

    /// <summary>Opens the device for sending.</summary>
    public static MidiOutputDevice Open(int deviceId = 0)
    {
        var dev = new MidiOutputDevice(deviceId, GetDevName(deviceId));
        dev.OpenInternal();
        return dev;
    }

    private void OpenInternal()
    {
        int err = midiOutOpen(out _handle, DeviceId, IntPtr.Zero, IntPtr.Zero, 0);
        if (err != MMSYSERR_NOERROR)
            throw new InvalidOperationException($"midiOutOpen failed: MMSYSERR {err}");
    }

    /// <summary>Sends a raw short MIDI message (status + 2 data bytes).</summary>
    public void SendShortMessage(byte status, byte data1, byte data2)
    {
        if (_handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(MidiOutputDevice));
        uint msg = (uint)status | ((uint)data1 << 8) | ((uint)data2 << 16);
        int err = midiOutShortMsg(_handle, msg);
        if (err != MMSYSERR_NOERROR)
            throw new InvalidOperationException($"midiOutShortMsg failed: MMSYSERR {err}");
    }

    /// <summary>Sends a one-data-byte message (program change, channel pressure).</summary>
    public void SendShortMessage(byte status, byte data)
    {
        if (_handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(MidiOutputDevice));
        uint msg = (uint)status | ((uint)data << 8);
        int err = midiOutShortMsg(_handle, msg);
        if (err != MMSYSERR_NOERROR)
            throw new InvalidOperationException($"midiOutShortMsg failed: MMSYSERR {err}");
    }

    /// <summary>Sends a channel voice event.</summary>
    public void SendEvent(in MidiEvent evt)
    {
        byte status = (byte)((byte)evt.Type | (byte)(evt.Channel & 0x0F));
        switch (evt.Type)
        {
            case MidiEventType.ProgramChange:
            case MidiEventType.ChannelPressure:
                SendShortMessage(status, (byte)evt.Data1);
                break;
            default:
                SendShortMessage(status, (byte)evt.Data1, (byte)evt.Data2);
                break;
        }
    }

    /// <summary>Sends a SysEx message (starts with 0xF0, ends with 0xF7).</summary>
    public void SendSysEx(byte[] data)
    {
        if (_handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(MidiOutputDevice));
        var hdr = new MIDIHDR
        {
            lpData = Marshal.AllocHGlobal(data.Length),
            dwBufferLength = (uint)data.Length,
            dwBytesRecorded = 0,
            dwUser = UIntPtr.Zero,
            dwFlags = 0
        };
        try
        {
            Marshal.Copy(data, 0, hdr.lpData, data.Length);
            int err = midiOutPrepareHeader(_handle, ref hdr, Marshal.SizeOf<MIDIHDR>());
            if (err != MMSYSERR_NOERROR)
                throw new InvalidOperationException($"midiOutPrepareHeader failed: MMSYSERR {err}");
            err = midiOutLongMsg(_handle, ref hdr, Marshal.SizeOf<MIDIHDR>());
            if (err != MMSYSERR_NOERROR)
                throw new InvalidOperationException($"midiOutLongMsg failed: MMSYSERR {err}");
        }
        finally
        {
            try { midiOutUnprepareHeader(_handle, ref hdr, Marshal.SizeOf<MIDIHDR>()); } catch { }
            Marshal.FreeHGlobal(hdr.lpData);
        }
    }

    /// <summary>Panics all notes / resets the device.</summary>
    public void Reset() => midiOutReset(_handle);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle != IntPtr.Zero)
        {
            try { midiOutReset(_handle); } catch { }
            try { midiOutClose(_handle); } catch { }
            _handle = IntPtr.Zero;
        }
    }

    private const uint MMSYSERR_NOERROR = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct MIDIOUTCAPS
    {
        public ushort wMid;
        public ushort wPid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;
        public uint dwSupport;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIDIHDR
    {
        public IntPtr lpData;
        public uint dwBufferLength;
        public uint dwBytesRecorded;
        public UIntPtr dwUser;
        public uint dwFlags;
        public UIntPtr dwLoopCount;
        public IntPtr reserved;
        public int dwInstance;
    }

    [DllImport("winmm.dll")] private static extern int midiOutGetNumDevs();
    [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
    private static extern int midiOutGetDevCaps(int uDeviceID, ref MIDIOUTCAPS pmoc, int cbmoc);
    [DllImport("winmm.dll")]
    private static extern int midiOutOpen(out IntPtr phmo, int uDeviceID, IntPtr dwCallback, IntPtr dwInstance, uint dwFlags);
    [DllImport("winmm.dll")] private static extern int midiOutShortMsg(IntPtr hmo, uint dwMsg);
    [DllImport("winmm.dll")]
    private static extern int midiOutPrepareHeader(IntPtr hmo, ref MIDIHDR pmh, int cbmh);
    [DllImport("winmm.dll")]
    private static extern int midiOutUnprepareHeader(IntPtr hmo, ref MIDIHDR pmh, int cbmh);
    [DllImport("winmm.dll")]
    private static extern int midiOutLongMsg(IntPtr hmo, ref MIDIHDR pmh, int cbmh);
    [DllImport("winmm.dll")] private static extern int midiOutReset(IntPtr hmo);
    [DllImport("winmm.dll")] private static extern int midiOutClose(IntPtr hmo);
}
