using System;
using System.Collections.Generic;
using System.IO;


namespace AudioForge.PluginHost
{
    public class PluginHost
    {
        private readonly PluginManager _manager;
        private readonly string _pluginDirectory;

        public PluginManager Manager => _manager;
        public string PluginDirectory => _pluginDirectory;

        public PluginHost(string pluginDirectory)
        {
            if (string.IsNullOrEmpty(pluginDirectory))
                throw new ArgumentException("Plugin directory cannot be null or empty.", nameof(pluginDirectory));

            _pluginDirectory = pluginDirectory;
            _manager = new PluginManager();
        }

        public IEnumerable<PluginDescriptor> Scan()
        {
            try
            {
                return PluginLoader.ScanDirectory(_pluginDirectory);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to scan plugin directory: {ex.Message}");
                return new List<PluginDescriptor>();
            }
        }

        public Guid Load(PluginDescriptor descriptor)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            if (!File.Exists(descriptor.FilePath))
                throw new FileNotFoundException($"Plugin file not found: {descriptor.FilePath}");

            return _manager.LoadPlugin(descriptor);
        }

        public Dictionary<string, Guid> LoadAll()
        {
            var results = new Dictionary<string, Guid>();
            var descriptors = Scan();

            foreach (var descriptor in descriptors)
            {
                try
                {
                    Guid id = _manager.LoadPlugin(descriptor);
                    results[descriptor.Name] = id;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load plugin {descriptor.Name}: {ex.Message}");
                }
            }

            return results;
        }

        public bool Unload(Guid id)
        {
            return _manager.UnloadPlugin(id);
        }

        public IAudioPlugin Get(Guid id)
        {
            return _manager.GetPlugin(id);
        }

        public IEnumerable<IAudioPlugin> All()
        {
            return _manager.GetAllPlugins();
        }

        public PluginNode CreateNode(Guid id, int sampleRate, int channels)
        {
            IAudioPlugin plugin = _manager.GetPlugin(id);
            if (plugin == null)
                throw new InvalidOperationException($"Plugin not found: {id}");

            PluginDescriptor descriptor = _manager.GetDescriptor(id);
            string nodeName = descriptor?.Name ?? plugin.Name;

            plugin.Initialize(sampleRate, channels);
            return new PluginNode(sampleRate, channels, plugin, nodeName);
        }

        public PluginNode CreateNodeFromDescriptor(PluginDescriptor descriptor, int sampleRate, int channels)
        {
            Guid id = _manager.LoadPlugin(descriptor);
            return CreateNode(id, sampleRate, channels);
        }

        public void InitializeAll(int sampleRate, int channels)
        {
            _manager.InitializeAll(sampleRate, channels);
        }

        public void ResetAll()
        {
            _manager.ResetAll();
        }

        public void Clear()
        {
            _manager.Clear();
        }

        public string GetPluginDescription(Guid id)
        {
            PluginDescriptor descriptor = _manager.GetDescriptor(id);
            if (descriptor == null)
                return "Unknown plugin";

            return $"{descriptor.Name} v{descriptor.Version} by {descriptor.Author} ({descriptor.Type})";
        }

        public PluginHostStatistics GetStatistics()
        {
            var stats = new PluginHostStatistics();

            foreach (var desc in _manager.GetAllDescriptors())
            {
                stats.TotalPlugins++;
                switch (desc.Type)
                {
                    case PluginType.Filter: stats.Filters++; break;
                    case PluginType.Effect: stats.Effects++; break;
                    case PluginType.Generator: stats.Generators++; break;
                    case PluginType.Analyzer: stats.Analyzers++; break;
                    case PluginType.Custom: stats.Custom++; break;
                }
            }

            return stats;
        }
    }
}

