using System;
using System.Runtime.InteropServices;

namespace AudioForge.IO.Codecs;

/// <summary>
/// Media Foundation COM interop via raw vtable calls. RCW-based interface
/// marshaling proved unreliable for these MF interfaces, so every call goes
/// through the object's vtable directly — deterministic and allocation-free.
/// Vtable slot indices verified against mfobjects.idl / mfreadwrite.idl.
/// </summary>
public static class MfNative
{
    public const int MF_VERSION = 0x00020070;
    public const uint MF_STARTUP_FULL = 0;

    // GUIDs verified against Windows SDK mfapi.h / mfreadwrite.idl.
    public static readonly Guid MF_MT_MAJOR_TYPE = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    public static readonly Guid MF_MT_SUBTYPE = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    public static readonly Guid MF_MT_AUDIO_NUM_CHANNELS = new("37e48bf5-645e-4c5b-89de-ada9e29b696a");
    public static readonly Guid MF_MT_AUDIO_SAMPLES_PER_SECOND = new("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
    public static readonly Guid MF_MT_AUDIO_BITS_PER_SAMPLE = new("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");
    public static readonly Guid MF_MT_AUDIO_BLOCK_ALIGNMENT = new("322de230-9eeb-43bd-ab7a-ff412251541d");
    public static readonly Guid MF_MT_AUDIO_AVG_BYTES_PER_SECOND = new("1aab75c8-cfef-451c-ab95-ac034b8e1731");
    public static readonly Guid MF_MT_ALL_SAMPLES_INDEPENDENT = new("c9173739-5e56-461c-b713-46fb995cb95f");
    public static readonly Guid MF_MT_AAC_AUDIO_PROFILE = new("c65530e8-58ca-4afb-a8d1-54998f6a8400");
    public static readonly Guid MF_MT_FRAME_SIZE = new("1652c33d-d6b2-4012-b834-72030849a37d");
    public static readonly Guid MFMediaType_Audio = new("73647561-0000-0010-8000-00aa00389b71");
    public static readonly Guid MFMediaType_AAC = new("00001610-0000-0010-8000-00aa00389b71");
    public static readonly Guid MFMediaType_Float = new("00000003-0000-0010-8000-00aa00389b71");
    public static readonly Guid MFMediaType_PCM = new("00000001-0000-0010-8000-00aa00389b71");

    public static readonly Guid MF_SINK_WRITER_DISABLE_THROTTLING = new("08b845d8-2b74-4afe-9d53-be16d2d5ae4f");
    public static readonly Guid MF_MT_COMPRESSED = new("3afd0cee-18f2-4ba5-a110-8bea502e1f92");

    public const uint MFSampleFlags_CleanPoint = 0x1;
    public const uint MFSampleFlags_Sync = 0x2;

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFStartup(uint version, uint flags);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFShutdown();

    [DllImport("mfreadwrite.dll", ExactSpelling = true)]
    public static extern int MFCreateSinkWriterFromURL(
        [MarshalAs(UnmanagedType.LPWStr)] string outputUrl,
        IntPtr byteStream, IntPtr attributes, out IntPtr writer);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFCreateAttributes(out IntPtr attributes, uint count);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFCreateMediaType(out IntPtr mediaType);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFCreateMemoryBuffer(uint maxLength, out IntPtr buffer);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFCreateSample(out IntPtr sample);

    // ---- vtable slot helpers ----
    // IMFAttributes : IUnknown. Slots 3..32 per mfobjects.idl order:
    // GetItem=3, GetItemType=4, CompareItem=5, Compare=6, GetUINT32=7,
    // GetUINT64=8, GetDouble=9, GetGUID=10, GetStringLength=11, GetString=12,
    // GetAllocatedString=13, GetBlobSize=14, GetBlob=15, GetAllocatedBlob=16,
    // GetUnknown=17, SetItem=18, DeleteItem=19, DeleteAllItems=20, SetUINT32=21,
    // SetUINT64=22, SetDouble=23, SetGUID=24, SetString=25, SetBlob=26,
    // SetUnknown=27, LockStore=28, UnlockStore=29, GetCount=30,
    // GetItemByIndex=31, CopyAllItems=32.
    private static IntPtr Vtbl(IntPtr obj) => Marshal.ReadIntPtr(obj);

    private static T Fn<T>(IntPtr obj, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Vtbl(obj), slot * IntPtr.Size));

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DSetU32(IntPtr self, in Guid key, uint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DSetGuid(IntPtr self, in Guid key, in Guid value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DSetStr(IntPtr self, in Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DRelease(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DQueryInterface(IntPtr self, in Guid riid, out IntPtr ppv);

    public static void AttrSetUINT32(IntPtr attrs, in Guid key, uint value) =>
        Hr(Fn<DSetU32>(attrs, 21)(attrs, key, value));
    public static void AttrSetGUID(IntPtr attrs, in Guid key, in Guid value) =>
        Hr(Fn<DSetGuid>(attrs, 24)(attrs, key, value));
    public static void AttrSetString(IntPtr attrs, in Guid key, string value) =>
        Hr(Fn<DSetStr>(attrs, 25)(attrs, key, value));
    public static void Release(IntPtr obj) => Fn<DRelease>(obj, 2)(obj);
    public static IntPtr QueryInterface(IntPtr obj, in Guid iid)
    {
        Fn<DQueryInterface>(obj, 0)(obj, iid, out var p);
        return p;
    }

    // IMFSinkWriter : IUnknown (mfreadwrite.idl). Slots: IUnknown(0-2),
    // AddStream=3, SetInputMediaType=4, BeginWriting=5, WriteSample=6,
    // SendStreamTick=7, PlaceMarker=8, NotifyEndOfSegment=9, Flush=10,
    // Finalize=11, GetServiceForStream=12, GetStatistics=13.
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DAddStream(IntPtr self, IntPtr mediaType, out uint index);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DSetInputMT(IntPtr self, uint idx, IntPtr mediaType, IntPtr attrs);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DBeginWriting(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DWriteSample(IntPtr self, uint idx, IntPtr sample);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DShutdownWriter(IntPtr self);

    public static void SinkAddStream(IntPtr writer, IntPtr mediaType) =>
        Hr(Fn<DAddStream>(writer, 3)(writer, mediaType, out _));
    public static void SinkSetInputMediaType(IntPtr writer, uint idx, IntPtr mediaType) =>
        Hr(Fn<DSetInputMT>(writer, 4)(writer, idx, mediaType, IntPtr.Zero));
    public static void SinkBeginWriting(IntPtr writer) =>
        Hr(Fn<DBeginWriting>(writer, 5)(writer));
    public static void SinkWriteSample(IntPtr writer, uint idx, IntPtr sample) =>
        Hr(Fn<DWriteSample>(writer, 6)(writer, idx, sample));
    public static void SinkFinalize(IntPtr writer) => Fn<DShutdownWriter>(writer, 11)(writer);

    // IMFSample : IMFAttributes (slots 0-32), then GetSampleFlags=33,
    // SetSampleFlags=34, GetSampleTime=35, SetSampleTime=36,
    // GetSampleDuration=37, SetSampleDuration=38, GetBufferCount=39,
    // GetBufferByIndex=40, ConvertToContiguousBuffer=41, AddBuffer=42,
    // RemoveBufferByIndex=43, RemoveAllBuffers=44, GetTotalLength=45,
    // CopyToBuffer=46.
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DSetSampleTime(IntPtr self, long time);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DSetSampleDuration(IntPtr self, long dur);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DSetSampleFlags(IntPtr self, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DAddBuffer(IntPtr self, IntPtr buffer);

    public static void SampleSetTime(IntPtr sample, long time) =>
        Hr(Fn<DSetSampleTime>(sample, 36)(sample, time));
    public static void SampleSetDuration(IntPtr sample, long dur) =>
        Hr(Fn<DSetSampleDuration>(sample, 38)(sample, dur));
    public static void SampleSetFlags(IntPtr sample, uint flags) =>
        Hr(Fn<DSetSampleFlags>(sample, 34)(sample, flags));
    public static void SampleAddBuffer(IntPtr sample, IntPtr buffer) =>
        Hr(Fn<DAddBuffer>(sample, 42)(sample, buffer));

    // IMFMediaBuffer slots: IUnknown(0-2), Lock=3, Unlock=4,
    // GetCurrentLength=5, SetCurrentLength=6, GetMaxLength=7.
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DLock(IntPtr self, out IntPtr data, out uint maxLen, out uint curLen);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DUnlock(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int DSetCurLen(IntPtr self, uint len);

    public static IntPtr BufferLock(IntPtr buffer, out uint maxLen)
    {
        Hr(Fn<DLock>(buffer, 3)(buffer, out var data, out maxLen, out _));
        return data;
    }
    public static void BufferUnlock(IntPtr buffer) => Fn<DUnlock>(buffer, 4)(buffer);
    public static void BufferSetLength(IntPtr buffer, uint len) => Hr(Fn<DSetCurLen>(buffer, 6)(buffer, len));

    private static void Hr(int hr)
    {
        if (hr < 0) Marshal.ThrowExceptionForHR(hr);
    }
}
