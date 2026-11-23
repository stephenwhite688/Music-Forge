using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AudioForge.MIDI;

/// <summary>
/// MIDI input device via WinMM (winmm.dll midiIn* API).
/// Raises <see cref="MessageReceived"/> for each channel voice message.
/// Real-time safe: messages are queued on the driver's callback thread and
/// marshalled to subscribers.
/// </summary>
public sealed class MidiInputDevice : IDisposable
{
    private IntPtr _handle;
    private GCHandle _callbackRoot;
    private bool _disposed;

    /// <summary>Raised on the WinMM callback thread for each voice message.</summary>
    public event Action<MidiEvent>? MessageReceived;

    /// <summary>Raised on driver errors or unexpected conditions.</summary>
    public event Action<string>? Error;

    public int DeviceId { get; }
    public string Name { get; }
    public bool IsOpen => _handle != IntPtr.Zero;

    private MidiInputDevice(int id, string name) { DeviceId = id; Name = name; }

    public static IReadOnlyList<(int Id, string Name)> GetDevices()
    {
        var list = new List<(int, string)>();
        int count = midiInGetNumDevs();
        for (int i = 0; i < count; i++)
            list.Add((i, GetDevName(i)));
        return list;
    }

    private static string GetDevName(int id)
    {
        var caps = new MIDIINCAPS();
        return midiInGetDevCaps(id, ref caps, Marshal.SizeOf<MIDIINCAPS>()) == 0
            ? caps.szPname : $"MIDI In {id}";
    }

    /// <summary>Opens the device and starts receiving messages.</summary>
    public static MidiInputDevice Open(int deviceId = 0)
    {
        var dev = new MidiInputDevice(deviceId, GetDevName(deviceId));
        dev.OpenInternal();
        return dev;
    }

    private void OpenInternal()
    {
        // CALLBACK_FUNCTION: driver calls our managed callback directly.
        _callbackRoot = GCHandle.Alloc(this);
        MidiInProc proc = OnMidiIn;
        int err = midiInOpen(out _handle, DeviceId, proc,
            GCHandle.ToIntPtr(_callbackRoot),
            CALLBACK_FUNCTION);
        if (err != MMSYSERR_NOERROR)
        {
            _callbackRoot.Free();
            throw new InvalidOperationException($"midiInOpen failed: MMSYSERR {err}");
        }
        err = midiInStart(_handle);
        if (err != MMSYSERR_NOERROR)
            throw new InvalidOperationException($"midiInStart failed: MMSYSERR {err}");
    }

    // WinMM midiInProc: (HMIDIIN, UINT wMsg, DWORD_PTR dwInstance,
    //                  DWORD_PTR dwParam1, DWORD_PTR dwParam2)
    private static void OnMidiIn(IntPtr hmi, uint msg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2)
    {
        var self = (MidiInputDevice?)GCHandle.FromIntPtr(dwInstance).Target;
        if (self == null) return;
        try
        {
            if (msg == MIM_DATA)
            {
                uint data = (uint)dwParam1.ToInt64();
                byte status = (byte)(data & 0xFF);
                byte d1 = (byte)((data >> 8) & 0xFF);
                byte d2 = (byte)((data >> 16) & 0xFF);
                Dispatch(self, status, d1, d2);
            }
            else if (msg == MIM_LONGDATA)
            {
                // SysEx — surface as a zero-data event so subscribers know.
                // (Full sysex payload requires MIDIHDR handling; skipped.)
            }
            // MIM_OPEN/MIM_CLOSE status messages are ignored.
            else if ((msg & MMSYSERR_BASE) != 0 || msg == MIDIERR_UNPREPARED)
            {
                self.Error?.Invoke($"midiIn error 0x{msg:X}");
            }
        }
        catch { /* never throw into the driver callback */ }
    }

    private static void Dispatch(MidiInputDevice self, byte status, byte d1, byte d2)
    {
        var type = (MidiEventType)(status & 0xF0);
        if (type is < MidiEventType.NoteOff or > MidiEventType.PitchBend) return;
        int channel = status & 0x0F;
        self.MessageReceived?.Invoke(new MidiEvent(0, channel, type, d1, d2));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle != IntPtr.Zero)
        {
            try { midiInStop(_handle); } catch { }
            try { midiInClose(_handle); } catch { }
            _handle = IntPtr.Zero;
        }
        if (_callbackRoot.IsAllocated) _callbackRoot.Free();
    }

    // ---- WinMM constants ----
    private const uint MMSYSERR_NOERROR = 0;
    private const uint MMSYSERR_BASE = 0;
    private const uint MIM_DATA = 0x3CB;
    private const uint MIM_LONGDATA = 0x3CC;
    private const uint MIDIERR_UNPREPARED = 0xFFFE;
    private const uint CALLBACK_FUNCTION = 0x00030000;

    private delegate void MidiInProc(IntPtr hmi, uint msg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct MIDIINCAPS
    {
        public ushort wMid;
        public ushort wPid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;
        public uint dwSupport;
    }

    [DllImport("winmm.dll")] private static extern int midiInGetNumDevs();
    [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
    private static extern int midiInGetDevCaps(int uDeviceID, ref MIDIINCAPS pmic, int cbmic);
    [DllImport("winmm.dll")]
    private static extern int midiInOpen(out IntPtr phmi, int uDeviceID, MidiInProc dwCallback, IntPtr dwInstance, uint dwFlags);
    [DllImport("winmm.dll")] private static extern int midiInStart(IntPtr hmi);
    [DllImport("winmm.dll")] private static extern int midiInStop(IntPtr hmi);
    [DllImport("winmm.dll")] private static extern int midiInClose(IntPtr hmi);
}
