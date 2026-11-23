using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AudioForge.PluginHost
{
    public static class PluginLoader
    {
        private static readonly HashSet<string> _loadedAssemblies = new HashSet<string>();

        public static IEnumerable<PluginDescriptor> ScanDirectory(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                throw new ArgumentException("Folder path cannot be null or empty.", nameof(folder));

            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException($"Directory not found: {folder}");

            List<PluginDescriptor> descriptors = new List<PluginDescriptor>();

            foreach (string dllPath in Directory.GetFiles(folder, "*.dll"))
            {
                try
                {
                    var pluginDescriptors = ScanAssembly(dllPath);
                    descriptors.AddRange(pluginDescriptors);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to scan {dllPath}: {ex.Message}");
                }
            }

            return descriptors;
        }

        public static IEnumerable<PluginDescriptor> ScanAssembly(string assemblyPath)
        {
            if (string.IsNullOrEmpty(assemblyPath))
                throw new ArgumentException("Assembly path cannot be null or empty.", nameof(assemblyPath));

            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException($"Assembly not found: {assemblyPath}");

            List<PluginDescriptor> descriptors = new List<PluginDescriptor>();

            try
            {
                Assembly assembly = Assembly.LoadFrom(assemblyPath);

                foreach (Type type in assembly.GetTypes())
                {
                    if (typeof(IAudioPlugin).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                    {
                        try
                        {
                            IAudioPlugin plugin = (IAudioPlugin)Activator.CreateInstance(type);

                            var descriptor = new PluginDescriptor(
                                assemblyPath,
                                type.FullName,
                                plugin.Name,
                                plugin.Author,
                                plugin.Version,
                                plugin.Type
                            );

                            descriptors.Add(descriptor);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to instantiate plugin {type.FullName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to scan assembly {assemblyPath}: {ex.Message}");
                throw;
            }

            return descriptors;
        }

        public static IAudioPlugin LoadPlugin(PluginDescriptor descriptor)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            try
            {
                Assembly assembly = null;
                string assemblyPath = descriptor.FilePath;

                if (!_loadedAssemblies.Contains(assemblyPath))
                {
                    assembly = Assembly.LoadFrom(assemblyPath);
                    _loadedAssemblies.Add(assemblyPath);
                }
                else
                {
                    foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (loadedAssembly.Location == assemblyPath)
                        {
                            assembly = loadedAssembly;
                            break;
                        }
                    }
                }

                if (assembly == null)
                    throw new InvalidOperationException($"Failed to load assembly: {descriptor.FilePath}");

                Type pluginType = assembly.GetType(descriptor.ClassName);
                if (pluginType == null)
                    throw new InvalidOperationException($"Type not found: {descriptor.ClassName}");

                return (IAudioPlugin)Activator.CreateInstance(pluginType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load plugin: {ex.Message}", ex);
            }
        }

        public static string GetFileVersion(string filePath)
        {
            try
            {
                var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(filePath);
                return versionInfo.FileVersion ?? "1.0.0.0";
            }
            catch
            {
                return "1.0.0.0";
            }
        }

        public static void ClearAssemblyCache()
        {
            _loadedAssemblies.Clear();
        }
    }
}

