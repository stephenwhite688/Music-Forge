using AudioForge.PluginHost.CLAP.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AudioForge.PluginHost.CLAP;

/// <summary>
/// CLAP plugin host: scans directories for .clap bundles, enumerates
/// plugin descriptors via the factory, and instantiates plugins.
/// </summary>
public sealed class ClapPluginHost : IDisposable
{
    private readonly List<(IntPtr Handle, ClapAbi.ClapPluginEntry Entry)> _loaded = new();
    private bool _disposed;

    public sealed class ClapPluginDescriptor
    {
        public string Id = "";
        public string Name = "";
        public string Vendor = "";
        public string Version = "";
        public string Description = "";
        public string Path = "";
    }

    /// <summary>Scans a directory (non-recursive) for .clap plugins.</summary>
    public IReadOnlyList<ClapPluginDescriptor> ScanDirectory(string directory)
    {
        var results = new List<ClapPluginDescriptor>();
        if (!Directory.Exists(directory)) return results;
        foreach (var file in Directory.EnumerateFiles(directory, "*.clap"))
        {
            try { results.AddRange(EnumerateFile(file)); }
            catch { /* skip unloadable plugin */ }
        }
        return results;
    }

    /// <summary>Enumerates all plugins exported by a .clap file.</summary>
    public IReadOnlyList<ClapPluginDescriptor> EnumerateFile(string path)
    {
        var (handle, entry) = LoadEntry(path);
        try
        {
            return EnumerateEntry(handle, entry, path);
        }
        finally
        {
            // Keep library loaded so returned descriptors stay valid;
            // unloading happens on Dispose or when CreatePlugin is called.
        }
    }

    /// <summary>Creates and initializes a plugin instance by descriptor.</summary>
    public ClapPlugin CreatePlugin(ClapPluginDescriptor descriptor)
    {
        var (handle, entry) = LoadEntry(descriptor.Path);
        var factoryPtr = GetFactory(handle, entry);
        var factory = Marshal.PtrToStructure<ClapAbi.ClapPluginFactory>(factoryPtr);
        var createFn = Marshal.GetDelegateForFunctionPointer<ClapAbi.FactoryCreatePluginFn>(factory.CreatePlugin);

        byte[] idBytes = Encoding.UTF8.GetBytes(descriptor.Id + '\0');
        var h = GCHandle.Alloc(idBytes, GCHandleType.Pinned);
        IntPtr pluginPtr;
        try { pluginPtr = createFn(factoryPtr, IntPtr.Zero, h.AddrOfPinnedObject()); }
        finally { h.Free(); }
        if (pluginPtr == IntPtr.Zero)
            throw new InvalidOperationException($"CLAP factory could not create plugin '{descriptor.Id}'.");

        var vtable = Marshal.PtrToStructure<ClapAbi.ClapPlugin>(pluginPtr);
        var plugin = new ClapPlugin(handle, entry, factoryPtr, pluginPtr, vtable,
            descriptor.Id, descriptor.Name, descriptor.Vendor, descriptor.Version, descriptor.Description);
        plugin.Initialize();
        _loaded.Add((handle, entry));
        return plugin;
    }

    // ---- internals ----

    private (IntPtr Handle, ClapAbi.ClapPluginEntry Entry) LoadEntry(string path)
    {
        IntPtr handle = NativeMethods.LoadLibrary(path);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException($"LoadLibrary failed for '{path}' (error {Marshal.GetLastWin32Error()}).");
        IntPtr entryPtr = NativeMethods.GetProcAddress(handle, "clap_entry");
        if (entryPtr == IntPtr.Zero)
        {
            NativeMethods.FreeLibrary(handle);
            throw new InvalidOperationException($"'{path}' does not export clap_entry.");
        }
        var entry = Marshal.PtrToStructure<ClapAbi.ClapPluginEntry>(entryPtr);
        if (entry.ClapVersion.Major != ClapAbi.CLAP_VERSION_MAJOR)
        {
            NativeMethods.FreeLibrary(handle);
            throw new InvalidOperationException($"'{path}' requires CLAP major {entry.ClapVersion.Major}, host supports {ClapAbi.CLAP_VERSION_MAJOR}.");
        }
        var initFn = Marshal.GetDelegateForFunctionPointer<ClapAbi.InitFn>(entry.Init);
        byte[] pathBytes = Encoding.UTF8.GetBytes(path + '\0');
        var ph = GCHandle.Alloc(pathBytes, GCHandleType.Pinned);
        try
        {
            if (!initFn(ph.AddrOfPinnedObject()))
            {
                NativeMethods.FreeLibrary(handle);
                throw new InvalidOperationException($"clap_entry.init failed for '{path}'.");
            }
        }
        finally { ph.Free(); }
        return (handle, entry);
    }

    private IntPtr GetFactory(IntPtr handle, ClapAbi.ClapPluginEntry entry)
    {
        var getFactory = Marshal.GetDelegateForFunctionPointer<ClapAbi.GetFactoryFn>(entry.GetFactory);
        byte[] idBytes = Encoding.UTF8.GetBytes(ClapAbi.FACTORY_ID + '\0');
        var h = GCHandle.Alloc(idBytes, GCHandleType.Pinned);
        try
        {
            IntPtr ptr = getFactory(h.AddrOfPinnedObject());
            if (ptr == IntPtr.Zero)
                throw new InvalidOperationException("Plugin does not export clap.plugin-factory.");
            return ptr;
        }
        finally { h.Free(); }
    }

    private List<ClapPluginDescriptor> EnumerateEntry(IntPtr handle, ClapAbi.ClapPluginEntry entry, string path)
    {
        var results = new List<ClapPluginDescriptor>();
        IntPtr factoryPtr = GetFactory(handle, entry);
        var factory = Marshal.PtrToStructure<ClapAbi.ClapPluginFactory>(factoryPtr);
        var countFn = Marshal.GetDelegateForFunctionPointer<ClapAbi.FactoryCountFn>(factory.GetPluginCount);
        var getDescFn = Marshal.GetDelegateForFunctionPointer<ClapAbi.FactoryGetDescriptorFn>(factory.GetPluginDescriptor);

        uint count = countFn(factoryPtr);
        int descSize = Marshal.SizeOf<ClapAbi.ClapPluginDescriptor>();
        for (uint i = 0; i < count; i++)
        {
            IntPtr dptr = getDescFn(factoryPtr, i);
            if (dptr == IntPtr.Zero) continue;
            var d = Marshal.PtrToStructure<ClapAbi.ClapPluginDescriptor>(dptr);
            results.Add(new ClapPluginDescriptor
            {
                Id = PtrToUtf8(d.Id),
                Name = PtrToUtf8(d.Name),
                Vendor = PtrToUtf8(d.Vendor),
                Version = PtrToUtf8(d.Version),
                Description = PtrToUtf8(d.Description),
                Path = path,
            });
        }
        return results;
    }

    private static string PtrToUtf8(IntPtr p)
    {
        if (p == IntPtr.Zero) return "";
        var sb = new StringBuilder();
        for (int i = 0; i < 4096; i++)
        {
            byte b = Marshal.ReadByte(p, i);
            if (b == 0) break;
            sb.Append((char)b);
        }
        return sb.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var (handle, entry) in _loaded)
        {
            try
            {
                var deinit = Marshal.GetDelegateForFunctionPointer<ClapAbi.VoidFn>(entry.Deinit);
                deinit();
            }
            catch { }
            NativeMethods.FreeLibrary(handle);
        }
        _loaded.Clear();
    }
}
