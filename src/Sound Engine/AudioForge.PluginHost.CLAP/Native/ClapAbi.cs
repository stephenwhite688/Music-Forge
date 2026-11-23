using System;
using System.Runtime.InteropServices;

namespace AudioForge.PluginHost.CLAP.Native;

/// <summary>
/// Raw CLAP ABI definitions mirroring include/clap/*.h (CLAP 1.2.x).
/// All structs use sequential layout matching the C declarations.
/// Function pointers are held as IntPtr and converted to delegates;
/// every delegate is stored in a field to keep it alive for the
/// lifetime of the native object.
/// </summary>
internal static class ClapAbi
{
    public const uint CLAP_VERSION_MAJOR = 1;
    public const uint CLAP_VERSION_MINOR = 2;
    public const uint CLAP_VERSION_REVISION = 10;
    public const string FACTORY_ID = "clap.plugin-factory";
    public const string EXT_AUDIO_PORTS = "clap.audio-ports";
    public const string EXT_PARAMS = "clap.params";
    public const string EXT_STATE = "clap.state";
    public const string EXT_TAIL = "clap.tail";

    public const int CLAP_EVENT_PARAM_VALUE = 5;
    public const int CLAP_CORE_EVENT_SPACE_ID = 0;

    public const uint CLAP_AUDIO_PORT_IS_MAIN = 1 << 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct ClapVersion
    {
        public uint Major, Minor, Revision;
        public static ClapVersion Current => new() { Major = CLAP_VERSION_MAJOR, Minor = CLAP_VERSION_MINOR, Revision = CLAP_VERSION_REVISION };
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool InitFn(IntPtr pluginPath);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VoidFn();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr GetFactoryFn(IntPtr factoryId);

    [StructLayout(LayoutKind.Sequential)]
    public struct ClapPluginEntry
    {
        public ClapVersion ClapVersion;
        public IntPtr Init, Deinit, GetFactory;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ClapPluginDescriptor
    {
        public ClapVersion ClapVersion;
        public IntPtr Id, Name, Vendor, Url, ManualUrl, SupportUrl, Version, Description, Features;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate uint FactoryCountFn(IntPtr factory);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr FactoryGetDescriptorFn(IntPtr factory, uint index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr FactoryCreatePluginFn(IntPtr factory, IntPtr host, IntPtr pluginId);

    [StructLayout(LayoutKind.Sequential)]
    public struct ClapPluginFactory
    {
        public IntPtr GetPluginCount, GetPluginDescriptor, CreatePlugin;
    }

    // ---- clap_plugin ----
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool PluginInitFn(IntPtr plugin);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void PluginVoidFn(IntPtr plugin);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool PluginActivateFn(IntPtr plugin, double sampleRate, uint minFrames, uint maxFrames);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int PluginProcessFn(IntPtr plugin, IntPtr process);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr PluginGetExtensionFn(IntPtr plugin, IntPtr extensionId);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void PluginOnMainThreadFn(IntPtr plugin);

    [StructLayout(LayoutKind.Sequential)]
    public struct ClapPlugin
    {
        public IntPtr Desc;
        public IntPtr PluginData;
        public IntPtr Init, Destroy, Activate, Deactivate, StartProcessing, StopProcessing, Reset, Process, GetExtension, OnMainThread;
    }

    // ---- clap_host ----
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr HostGetExtensionFn(IntPtr host, IntPtr extensionId);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void HostVoidFn(IntPtr host);

    [StructLayout(LayoutKind.Sequential)]
    public struct ClapHost
    {
        public ClapVersion ClapVersion;
        public IntPtr HostData;
        public IntPtr Name, Vendor, Url, Version;
        public IntPtr GetExtension, RequestRestart, RequestProcess, RequestCallback;
    }

    // ---- audio ----
    [StructLayout(LayoutKind.Sequential)]
    public struct ClapAudioBuffer
    {
        public IntPtr Data32;   // float** - pointer to array of channel pointers
        public IntPtr Data64;   // double**
        public uint ChannelCount;
        public uint Latency;
        public ulong ConstantMask;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate uint InputEventsSizeFn(IntPtr list);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr InputEventsGetFn(IntPtr list, uint index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool OutputEventsTryPushFn(IntPtr list, IntPtr eventPtr);

    [StructLayout(LayoutKind.Sequential)]
    public struct ClapInputEvents
    {
        public IntPtr Ctx;
        public IntPtr Size, Get;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ClapOutputEvents
    {
        public IntPtr Ctx;
        public IntPtr TryPush;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ClapProcess
    {
        public long SteadyTime;
        public uint FramesCount;
        public IntPtr Transport;
        public IntPtr AudioInputs;
        public IntPtr AudioOutputs;
        public uint AudioInputsCount;
        public uint AudioOutputsCount;
        public IntPtr InEvents;
        public IntPtr OutEvents;
    }

    // ---- audio ports ext ----
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate uint PortsCountFn(IntPtr plugin, bool isInput);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool PortsGetFn(IntPtr plugin, uint index, bool isInput, IntPtr info);

    [StructLayout(LayoutKind.Sequential)]
    public struct ClapAudioPorts
    {
        public IntPtr Count, Get;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct ClapAudioPortInfo
    {
        public uint Id;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] Name;
        public uint Flags;
        public uint ChannelCount;
        public IntPtr PortType;
        public uint InPlacePair;
    }

    // ---- params ext ----
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate uint ParamsCountFn(IntPtr plugin);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool ParamsGetInfoFn(IntPtr plugin, uint index, IntPtr paramInfo);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool ParamsGetValueFn(IntPtr plugin, uint paramId, IntPtr outValue);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool ParamsValueToTextFn(IntPtr plugin, uint paramId, double value, IntPtr outBuffer, uint capacity);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool ParamsTextToValueFn(IntPtr plugin, uint paramId, IntPtr text, IntPtr outValue);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ParamsFlushFn(IntPtr plugin, IntPtr inEvents, IntPtr outEvents);

    [StructLayout(LayoutKind.Sequential)]
    public struct ClapPluginParams
    {
        public IntPtr Count, GetInfo, GetValue, ValueToText, TextToValue, Flush;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ClapParamInfo
    {
        public uint Id;
        public uint Flags;
        public IntPtr Cookie;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] Name;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
        public byte[] Module;
        public double MinValue, MaxValue, DefaultValue;
    }

    // ---- events ----
    [StructLayout(LayoutKind.Sequential)]
    public struct ClapEventHeader
    {
        public uint Size;
        public uint Time;
        public ushort SpaceId;
        public ushort Type;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ClapEventParamValue
    {
        public ClapEventHeader Header;
        public uint ParamId;
        public IntPtr Cookie;
        public int NoteId;
        public short PortIndex;
        public short Channel;
        public short Key;
        public double Value;
    }

    // ---- state ext ----
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool StateSaveFn(IntPtr plugin, IntPtr stream);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool StateLoadFn(IntPtr plugin, IntPtr stream);

    [StructLayout(LayoutKind.Sequential)]
    public struct ClapPluginState
    {
        public IntPtr Save, Load;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate long StreamReadFn(IntPtr stream, IntPtr buffer, ulong size);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate long StreamWriteFn(IntPtr stream, IntPtr buffer, ulong size);

    [StructLayout(LayoutKind.Sequential)]
    public struct ClapIStream { public IntPtr Ctx; public IntPtr Read; }
    [StructLayout(LayoutKind.Sequential)]
    public struct ClapOStream { public IntPtr Ctx; public IntPtr Write; }

    // ---- misc ----
    public const uint CLAP_PARAM_IS_HIDDEN = 1 << 2;
    public const uint CLAP_PARAM_IS_READONLY = 1 << 3;
    public const uint CLAP_PARAM_IS_BYPASS = 1 << 4;
}
