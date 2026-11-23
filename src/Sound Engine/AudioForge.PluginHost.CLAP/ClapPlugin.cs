using AudioForge.PluginHost.CLAP.Native;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace AudioForge.PluginHost.CLAP;

/// <summary>
/// A loaded CLAP plugin instance. Owns the native library handle, the
/// clap_entry/factory/plugin chain, the clap_host callbacks, audio buffers
/// and event lists. Not realtime-safe for concurrent use - drive it from
/// one thread (the audio node's Process caller).
/// </summary>
public sealed class ClapPlugin : IDisposable
{
    private readonly IntPtr _libHandle;
    private readonly ClapAbi.ClapPluginEntry _entry;
    private readonly IntPtr _factoryPtr;
    private readonly IntPtr _pluginPtr;
    private readonly ClapAbi.ClapPlugin _pluginVtable;

    // Host-side managed callbacks - MUST stay alive for plugin lifetime.
    private readonly ClapAbi.HostGetExtensionFn _hostGetExtension;
    private readonly ClapAbi.HostVoidFn _hostRestart, _hostProcess, _hostCallback;
    private readonly IntPtr _hostPtr;

    // Audio buffers (unmanaged, stable addresses).
    private IntPtr[] _inChannels = Array.Empty<IntPtr>();
    private IntPtr[] _outChannels = Array.Empty<IntPtr>();
    private IntPtr _channelPtrArray;   // float** for data32
    private IntPtr _inBufferPtr, _outBufferPtr, _processPtr;
    private long _steadyTime;
    private int _channelsIn, _channelsOut;

    // Event lists.
    private IntPtr _inEventsPtr, _outEventsPtr;
    private int _paramEventsCount;   // queued, not yet delivered
    private int _deliverCount;       // count the plugin sees for the current call
    private IntPtr _paramEventsBuffer;

    private bool _activated, _processing, _disposed;

    public string Id { get; }
    public string Name { get; }
    public string Vendor { get; }
    public string Version { get; }
    public string Description { get; }
    public int InputChannels => _channelsIn;
    public int OutputChannels => _channelsOut;
    public int LatencyFrames { get; private set; }

    internal ClapPlugin(IntPtr libHandle, ClapAbi.ClapPluginEntry entry, IntPtr factoryPtr,
        IntPtr pluginPtr, ClapAbi.ClapPlugin vtable, string id, string name, string vendor,
        string version, string description)
    {
        _libHandle = libHandle;
        _entry = entry;
        _factoryPtr = factoryPtr;
        _pluginPtr = pluginPtr;
        _pluginVtable = vtable;
        Id = id; Name = name; Vendor = vendor; Version = version; Description = description;

        _hostGetExtension = (h, eid) => IntPtr.Zero;
        _hostRestart = _ => { };
        _hostProcess = _ => { };
        _hostCallback = _ => { };

        var host = new ClapAbi.ClapHost
        {
            ClapVersion = ClapAbi.ClapVersion.Current,
            HostData = IntPtr.Zero,
            Name = Marshal.StringToHGlobalAnsi("AudioForge"),
            Vendor = Marshal.StringToHGlobalAnsi("AudioForge"),
            Url = Marshal.StringToHGlobalAnsi(""),
            Version = Marshal.StringToHGlobalAnsi("1.0"),
            GetExtension = Marshal.GetFunctionPointerForDelegate(_hostGetExtension),
            RequestRestart = Marshal.GetFunctionPointerForDelegate(_hostRestart),
            RequestProcess = Marshal.GetFunctionPointerForDelegate(_hostProcess),
            RequestCallback = Marshal.GetFunctionPointerForDelegate(_hostCallback),
        };
        _hostPtr = Marshal.AllocHGlobal(Marshal.SizeOf<ClapAbi.ClapHost>());
        Marshal.StructureToPtr(host, _hostPtr, false);
    }

    private static string Ansi(byte[]? bytes)
    {
        if (bytes is null) return "";
        int n = Array.IndexOf(bytes, (byte)0);
        if (n < 0) n = bytes.Length;
        return Encoding.ASCII.GetString(bytes, 0, n);
    }

    // ---- extension access ----

    private IntPtr GetExtension(string id)
    {
        var fn = Marshal.GetDelegateForFunctionPointer<ClapAbi.PluginGetExtensionFn>(_pluginVtable.GetExtension);
        byte[] utf8 = Encoding.UTF8.GetBytes(id + '\0');
        var h = GCHandle.Alloc(utf8, GCHandleType.Pinned);
        try { return fn(_pluginPtr, h.AddrOfPinnedObject()); }
        finally { h.Free(); }
    }

    private ClapAbi.ClapAudioPorts? AudioPortsExt { get; set; }
    private ClapAbi.ClapPluginParams? ParamsExt { get; set; }
    private ClapAbi.ClapPluginState? StateExt { get; set; }

    private T? GetExtStruct<T>(string extId) where T : struct
    {
        IntPtr p = GetExtension(extId);
        if (p == IntPtr.Zero) return null;
        return Marshal.PtrToStructure<T>(p);
    }

    /// <summary>Initializes plugin, reads port config. Call once.</summary>
    public void Initialize()
    {
        var init = Marshal.GetDelegateForFunctionPointer<ClapAbi.PluginInitFn>(_pluginVtable.Init);
        if (!init(_pluginPtr)) throw new InvalidOperationException($"CLAP plugin '{Name}' failed init().");
        AudioPortsExt = GetExtStruct<ClapAbi.ClapAudioPorts>(ClapAbi.EXT_AUDIO_PORTS);
        ParamsExt = GetExtStruct<ClapAbi.ClapPluginParams>(ClapAbi.EXT_PARAMS);
        StateExt = GetExtStruct<ClapAbi.ClapPluginState>(ClapAbi.EXT_STATE);
        var (inCh, outCh) = GetMainPortChannels();
        _channelsIn = inCh;
        _channelsOut = outCh;
    }

    /// <summary>Main input/output channel counts from the audio-ports extension.</summary>
    public (int In, int Out) GetMainPortChannels()
    {
        if (AudioPortsExt is null) return (2, 2); // spec default: stereo in/out
        var ports = AudioPortsExt.Value;
        var countFn = Marshal.GetDelegateForFunctionPointer<ClapAbi.PortsCountFn>(ports.Count);
        var getFn = Marshal.GetDelegateForFunctionPointer<ClapAbi.PortsGetFn>(ports.Get);

        int FindMain(bool isInput)
        {
            uint n = countFn(_pluginPtr, isInput);
            for (uint i = 0; i < n; i++)
            {
                int size = Marshal.SizeOf<ClapAbi.ClapAudioPortInfo>();
                IntPtr buf = Marshal.AllocHGlobal(size);
                try
                {
                    if (getFn(_pluginPtr, i, isInput, buf) &&
                        (Marshal.PtrToStructure<ClapAbi.ClapAudioPortInfo>(buf).Flags & ClapAbi.CLAP_AUDIO_PORT_IS_MAIN) != 0)
                        return (int)Marshal.PtrToStructure<ClapAbi.ClapAudioPortInfo>(buf).ChannelCount;
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            return 0;
        }

        return (FindMain(true), FindMain(false));
    }

    /// <summary>Activates for a sample rate and frame range, allocates buffers.</summary>
    public void Activate(double sampleRate, uint minFrames, uint maxFrames, int inChannels, int outChannels)
    {
        var act = Marshal.GetDelegateForFunctionPointer<ClapAbi.PluginActivateFn>(_pluginVtable.Activate);
        if (!act(_pluginPtr, sampleRate, minFrames, maxFrames))
            throw new InvalidOperationException($"CLAP plugin '{Name}' failed activate().");
        _activated = true;
        _channelsIn = inChannels;
        _channelsOut = outChannels;
        MaxFrames = (int)maxFrames;
        AllocateBuffers(maxFrames);
    }

    private void AllocateBuffers(uint frames)
    {
        _inChannels = AllocChannelSet(_channelsIn, frames);
        _outChannels = AllocChannelSet(_channelsOut, frames);
        _channelPtrArray = Marshal.AllocHGlobal(IntPtr.Size * Math.Max(_channelsIn, _channelsOut));
        _inBufferPtr = Marshal.AllocHGlobal(Marshal.SizeOf<ClapAbi.ClapAudioBuffer>());
        _outBufferPtr = Marshal.AllocHGlobal(Marshal.SizeOf<ClapAbi.ClapAudioBuffer>());
        _processPtr = Marshal.AllocHGlobal(Marshal.SizeOf<ClapAbi.ClapProcess>());
        _inEventsPtr = CreateInputEvents();
        _outEventsPtr = CreateOutputEvents();
        _paramEventsBuffer = Marshal.AllocHGlobal(ParamEventCapacity);
    }

    private IntPtr[] AllocChannelSet(int channels, uint frames)
    {
        var arr = new IntPtr[channels];
        for (int c = 0; c < channels; c++)
            arr[c] = Marshal.AllocHGlobal((int)frames * 4);
        return arr;
    }

    public void Deactivate()
    {
        if (!_activated) return;
        CallVoid(_pluginVtable.Deactivate);
        _activated = false;
        FreeBuffers();
    }

    private void FreeBuffers()
    {
        foreach (var p in _inChannels) if (p != IntPtr.Zero) Marshal.FreeHGlobal(p);
        foreach (var p in _outChannels) if (p != IntPtr.Zero) Marshal.FreeHGlobal(p);
        if (_channelPtrArray != IntPtr.Zero) Marshal.FreeHGlobal(_channelPtrArray);
        if (_inBufferPtr != IntPtr.Zero) Marshal.FreeHGlobal(_inBufferPtr);
        if (_outBufferPtr != IntPtr.Zero) Marshal.FreeHGlobal(_outBufferPtr);
        if (_processPtr != IntPtr.Zero) Marshal.FreeHGlobal(_processPtr);
        if (_inEventsPtr != IntPtr.Zero) { Marshal.FreeHGlobal(_inEventsPtr); _inEventsPtr = IntPtr.Zero; }
        if (_outEventsPtr != IntPtr.Zero) { Marshal.FreeHGlobal(_outEventsPtr); _outEventsPtr = IntPtr.Zero; }
        if (_paramEventsBuffer != IntPtr.Zero) { Marshal.FreeHGlobal(_paramEventsBuffer); _paramEventsBuffer = IntPtr.Zero; }
        _inChannels = _outChannels = Array.Empty<IntPtr>();
        _channelPtrArray = _inBufferPtr = _outBufferPtr = _processPtr = IntPtr.Zero;
        _paramEventsCount = _deliverCount = 0;
    }

    public void StartProcessing()
    {
        var fn = Marshal.GetDelegateForFunctionPointer<ClapAbi.PluginInitFn>(_pluginVtable.StartProcessing);
        if (!fn(_pluginPtr)) throw new InvalidOperationException($"CLAP plugin '{Name}' failed start_processing().");
        _processing = true;
    }

    public void StopProcessing()
    {
        if (!_processing) return;
        CallVoid(_pluginVtable.StopProcessing);
        _processing = false;
    }

    public void Reset() => CallVoid(_pluginVtable.Reset);

    private void CallVoid(IntPtr fnPtr)
        => Marshal.GetDelegateForFunctionPointer<ClapAbi.PluginVoidFn>(fnPtr)(_pluginPtr);

    // ---- process ----

    /// <summary>
    /// Processes one block. Input/output are per-channel unmanaged float
    /// buffers sized to the activated frame count.
    /// </summary>
    public int ProcessBlock(ReadOnlySpan<float> inInterleaved, Span<float> outInterleaved, int frames)
    {
        if (!_processing) throw new InvalidOperationException("Plugin not processing.");
        if (frames > MaxFrames) throw new ArgumentOutOfRangeException(nameof(frames), $"Block exceeds plugin max {MaxFrames}.");

        // Deinterleave input.
        for (int f = 0; f < frames; f++)
        {
            int src = f * _channelsIn;
            for (int c = 0; c < _channelsIn; c++)
                Marshal.WriteInt32(_inChannels[c], f * 4, BitConverter.SingleToInt32Bits(inInterleaved[src + c]));
        }

        // Write channel pointer arrays.
        for (int c = 0; c < _channelsIn; c++)
            Marshal.WriteIntPtr(_channelPtrArray, c * IntPtr.Size, _inChannels[c]);
        for (int c = 0; c < _channelsOut; c++)
            Marshal.WriteIntPtr(_channelPtrArray + _channelsIn * IntPtr.Size, c * IntPtr.Size, _outChannels[c]);

        var inBuf = new ClapAbi.ClapAudioBuffer
        {
            Data32 = _channelPtrArray,
            ChannelCount = (uint)_channelsIn,
            Latency = 0,
            ConstantMask = 0,
        };
        var outBuf = new ClapAbi.ClapAudioBuffer
        {
            Data32 = _channelPtrArray + _channelsIn * IntPtr.Size,
            ChannelCount = (uint)_channelsOut,
            Latency = 0,
            ConstantMask = 0,
        };
        Marshal.StructureToPtr(inBuf, _inBufferPtr, false);
        Marshal.StructureToPtr(outBuf, _outBufferPtr, false);

        var proc = new ClapAbi.ClapProcess
        {
            SteadyTime = _steadyTime,
            FramesCount = (uint)frames,
            Transport = IntPtr.Zero,
            AudioInputs = _inBufferPtr,
            AudioOutputs = _outBufferPtr,
            AudioInputsCount = _channelsIn > 0 ? 1u : 0u,
            AudioOutputsCount = _channelsOut > 0 ? 1u : 0u,
            InEvents = _inEventsPtr,
            OutEvents = _outEventsPtr,
        };
        Marshal.StructureToPtr(proc, _processPtr, false);

        // Hand queued param events to this call exactly once.
        _deliverCount = _paramEventsCount;
        _paramEventsCount = 0;
        var processFn = Marshal.GetDelegateForFunctionPointer<ClapAbi.PluginProcessFn>(_pluginVtable.Process);
        int status = processFn(_pluginPtr, _processPtr);
        _deliverCount = 0;
        _steadyTime += frames;
        if (status == 0) throw new InvalidOperationException($"CLAP plugin '{Name}' process() returned ERROR.");

        // Deinterleave output.
        for (int f = 0; f < frames; f++)
        {
            int dst = f * _channelsOut;
            for (int c = 0; c < _channelsOut; c++)
                outInterleaved[dst + c] = Int32BitsToSingle(Marshal.ReadInt32(_outChannels[c], f * 4));
        }
        return status;
    }

    private int MaxFrames { get; set; } = 8192;

    // ---- parameters ----

    public IReadOnlyList<ClapParamInfo> GetParameters()
    {
        var list = new List<ClapParamInfo>();
        if (ParamsExt is null) return list;
        var p = ParamsExt.Value;
        var countFn = Marshal.GetDelegateForFunctionPointer<ClapAbi.ParamsCountFn>(p.Count);
        var getInfoFn = Marshal.GetDelegateForFunctionPointer<ClapAbi.ParamsGetInfoFn>(p.GetInfo);
        uint n = countFn(_pluginPtr);
        int size = Marshal.SizeOf<ClapAbi.ClapParamInfo>();
        for (uint i = 0; i < n; i++)
        {
            IntPtr buf = Marshal.AllocHGlobal(size);
            try
            {
                if (getInfoFn(_pluginPtr, i, buf))
                {
                    var raw = Marshal.PtrToStructure<ClapAbi.ClapParamInfo>(buf);
                    list.Add(new ClapParamInfo
                    {
                        Id = raw.Id,
                        Name = Ansi(raw.Name),
                        Module = Ansi(raw.Module),
                        MinValue = raw.MinValue,
                        MaxValue = raw.MaxValue,
                        DefaultValue = raw.DefaultValue,
                        Flags = raw.Flags,
                    });
                }
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        return list;
    }

    public bool TryGetParamValue(uint paramId, out double value)
    {
        value = 0;
        if (ParamsExt is null) return false;
        var p = ParamsExt.Value;
        var fn = Marshal.GetDelegateForFunctionPointer<ClapAbi.ParamsGetValueFn>(p.GetValue);
        IntPtr buf = Marshal.AllocHGlobal(8);
        try
        {
            if (!fn(_pluginPtr, paramId, buf)) return false;
            value = Marshal.PtrToStructure<double>(buf);
            return true;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    /// <summary>Queues a parameter value change via the params flush event path.</summary>
    public void SetParamValue(uint paramId, double value)
    {
        if (ParamsExt is null) return;
        EnsureParamEventsBuffer();
        var ev = new ClapAbi.ClapEventParamValue
        {
            Header = new ClapAbi.ClapEventHeader
            {
                Size = (uint)Marshal.SizeOf<ClapAbi.ClapEventParamValue>(),
                Time = 0,
                SpaceId = 0,
                Type = ClapAbi.CLAP_EVENT_PARAM_VALUE,
                Flags = 0,
            },
            ParamId = paramId,
            Cookie = IntPtr.Zero,
            NoteId = -1,
            PortIndex = -1,
            Channel = -1,
            Key = -1,
            Value = value,
        };
        int size = Marshal.SizeOf<ClapAbi.ClapEventParamValue>();
        if ((_paramEventsCount + 1) * size > ParamEventCapacity)
            throw new InvalidOperationException("Too many pending param events.");
        Marshal.StructureToPtr(ev, EventPtr(_paramEventsCount), false);
        _paramEventsCount++;
    }

    /// <summary>Flushes queued parameter changes into the plugin.</summary>
    public void FlushParams()
    {
        if (ParamsExt is null || _paramEventsCount == 0) return;
        var p = ParamsExt.Value;
        var flushFn = Marshal.GetDelegateForFunctionPointer<ClapAbi.ParamsFlushFn>(p.Flush);
        _deliverCount = _paramEventsCount;
        _paramEventsCount = 0;
        flushFn(_pluginPtr, _inEventsPtr, _outEventsPtr);
        _deliverCount = 0;
    }

    private const int ParamEventCapacity = 64 * 1024;

    private IntPtr EventPtr(int index)
        => (IntPtr)((long)_paramEventsBuffer + (long)index * Marshal.SizeOf<ClapAbi.ClapEventParamValue>());

    private void EnsureParamEventsBuffer()
    {
        if (_paramEventsBuffer == IntPtr.Zero)
            _paramEventsBuffer = Marshal.AllocHGlobal(ParamEventCapacity);
    }

    // ---- state ----

    /// <summary>Saves plugin state (clap.state) to a byte array, or null if unsupported.</summary>
    public byte[]? SaveState()
    {
        if (StateExt is null) return null;
        var s = StateExt.Value;
        var saveFn = Marshal.GetDelegateForFunctionPointer<ClapAbi.StateSaveFn>(s.Save);

        var ms = new System.IO.MemoryStream();
        ClapAbi.StreamWriteFn writeFn = (stream, buffer, size) =>
        {
            byte[] tmp = new byte[size];
            Marshal.Copy(buffer, tmp, 0, (int)size);
            ms.Write(tmp, 0, tmp.Length);
            return (long)size;
        };
        var ostream = new ClapAbi.ClapOStream { Ctx = IntPtr.Zero, Write = Marshal.GetFunctionPointerForDelegate(writeFn) };
        IntPtr sp = Marshal.AllocHGlobal(Marshal.SizeOf<ClapAbi.ClapOStream>());
        try
        {
            Marshal.StructureToPtr(ostream, sp, false);
            return saveFn(_pluginPtr, sp) ? ms.ToArray() : null;
        }
        finally { Marshal.FreeHGlobal(sp); }
    }

    public bool LoadState(byte[] data)
    {
        if (StateExt is null || data is null) return false;
        var s = StateExt.Value;
        var loadFn = Marshal.GetDelegateForFunctionPointer<ClapAbi.StateLoadFn>(s.Load);
        int pos = 0;
        ClapAbi.StreamReadFn readFn = (stream, buffer, size) =>
        {
            int want = (int)Math.Min(size, (ulong)(data.Length - pos));
            if (want <= 0) return 0;
            Marshal.Copy(data, pos, buffer, want);
            pos += want;
            return want;
        };
        var istream = new ClapAbi.ClapIStream { Ctx = IntPtr.Zero, Read = Marshal.GetFunctionPointerForDelegate(readFn) };
        IntPtr sp = Marshal.AllocHGlobal(Marshal.SizeOf<ClapAbi.ClapIStream>());
        try
        {
            Marshal.StructureToPtr(istream, sp, false);
            return loadFn(_pluginPtr, sp);
        }
        finally { Marshal.FreeHGlobal(sp); }
    }

    // ---- event list plumbing ----

    // Input events: reads the param-event buffer we fill in SetParamValue/FlushParams.
    private ClapAbi.InputEventsSizeFn? _inSizeFn;
    private ClapAbi.InputEventsGetFn? _inGetFn;
    private ClapAbi.OutputEventsTryPushFn? _outPushFn;

    private IntPtr CreateInputEvents()
    {
        _inSizeFn = _ => (uint)_deliverCount;
        _inGetFn = (_, i) => EventPtr((int)i);
        var evs = new ClapAbi.ClapInputEvents
        {
            Ctx = IntPtr.Zero,
            Size = Marshal.GetFunctionPointerForDelegate(_inSizeFn),
            Get = Marshal.GetFunctionPointerForDelegate(_inGetFn),
        };
        IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf<ClapAbi.ClapInputEvents>());
        Marshal.StructureToPtr(evs, p, false);
        return p;
    }

    private IntPtr CreateOutputEvents()
    {
        _outPushFn = (_, _) => true; // discard plugin->host events for now
        var evs = new ClapAbi.ClapOutputEvents
        {
            Ctx = IntPtr.Zero,
            TryPush = Marshal.GetFunctionPointerForDelegate(_outPushFn),
        };
        IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf<ClapAbi.ClapOutputEvents>());
        Marshal.StructureToPtr(evs, p, false);
        return p;
    }

    // ---- misc ----

    public IntPtr PluginPtr => _pluginPtr;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { if (_processing) StopProcessing(); } catch { }
        try { if (_activated) Deactivate(); } catch { }
        try
        {
            var destroy = Marshal.GetDelegateForFunctionPointer<ClapAbi.PluginVoidFn>(_pluginVtable.Destroy);
            destroy(_pluginPtr);
        }
        catch { }
        if (_inEventsPtr != IntPtr.Zero) Marshal.FreeHGlobal(_inEventsPtr);
        if (_outEventsPtr != IntPtr.Zero) Marshal.FreeHGlobal(_outEventsPtr);
        if (_hostPtr != IntPtr.Zero) Marshal.FreeHGlobal(_hostPtr);
        FreeLibrary(_libHandle);
    }

    private static void FreeLibrary(IntPtr h) => NativeMethods.FreeLibrary(h);

    private static float Int32BitsToSingle(int bits) => BitConverter.Int32BitsToSingle(bits);
    private static int SingleToInt32Bits(float f) => BitConverter.SingleToInt32Bits(f);

    /// <summary>Descriptor info for a CLAP parameter.</summary>
    public sealed class ClapParamInfo
    {
        public uint Id;
        public string Name = "";
        public string Module = "";
        public double MinValue, MaxValue, DefaultValue;
        public uint Flags;
        public bool IsHidden => (Flags & ClapAbi.CLAP_PARAM_IS_HIDDEN) != 0;
        public bool IsReadOnly => (Flags & ClapAbi.CLAP_PARAM_IS_READONLY) != 0;
        public bool IsBypass => (Flags & ClapAbi.CLAP_PARAM_IS_BYPASS) != 0;
    }
}
