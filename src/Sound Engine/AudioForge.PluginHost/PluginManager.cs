using System;
using System.Collections.Generic;
using System.Linq;

namespace AudioForge.PluginHost
{
    public class PluginManager
    {
        private readonly Dictionary<Guid, IAudioPlugin> _plugins;
        private readonly Dictionary<Guid, PluginDescriptor> _descriptors;
        private readonly object _lock;

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _plugins.Count;
                }
            }
        }

        public PluginManager()
        {
            _plugins = new Dictionary<Guid, IAudioPlugin>();
            _descriptors = new Dictionary<Guid, PluginDescriptor>();
            _lock = new object();
        }

        public Guid LoadPlugin(PluginDescriptor descriptor)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            lock (_lock)
            {
                var existing = _descriptors.FirstOrDefault(kvp =>
                    kvp.Value.FilePath == descriptor.FilePath &&
                    kvp.Value.ClassName == descriptor.ClassName);

                if (existing.Key != Guid.Empty)
                    return existing.Key;

                IAudioPlugin plugin = PluginLoader.LoadPlugin(descriptor);
                Guid id = Guid.NewGuid();
                _plugins[id] = plugin;
                _descriptors[id] = descriptor;
                return id;
            }
        }

        public bool UnloadPlugin(Guid id)
        {
            lock (_lock)
            {
                if (!_plugins.ContainsKey(id))
                    return false;

                try
                {
                    _plugins[id].Reset();
                }
                catch { }

                _plugins.Remove(id);
                _descriptors.Remove(id);
                return true;
            }
        }

        public IAudioPlugin GetPlugin(Guid id)
        {
            lock (_lock)
            {
                _plugins.TryGetValue(id, out var plugin);
                return plugin;
            }
        }

        public PluginDescriptor GetDescriptor(Guid id)
        {
            lock (_lock)
            {
                _descriptors.TryGetValue(id, out var descriptor);
                return descriptor;
            }
        }

        public IEnumerable<Guid> GetAllPluginIds()
        {
            lock (_lock)
            {
                return _plugins.Keys.ToList();
            }
        }

        public IEnumerable<IAudioPlugin> GetAllPlugins()
        {
            lock (_lock)
            {
                return _plugins.Values.ToList();
            }
        }

        public IEnumerable<PluginDescriptor> GetAllDescriptors()
        {
            lock (_lock)
            {
                return _descriptors.Values.ToList();
            }
        }

        public Guid FindPluginByName(string name)
        {
            lock (_lock)
            {
                foreach (var kvp in _descriptors)
                {
                    if (kvp.Value.Name == name)
                        return kvp.Key;
                }
                return Guid.Empty;
            }
        }

        public IEnumerable<Guid> FindPluginsByType(PluginType type)
        {
            lock (_lock)
            {
                var results = new List<Guid>();
                foreach (var kvp in _descriptors)
                {
                    if (kvp.Value.Type == type)
                        results.Add(kvp.Key);
                }
                return results;
            }
        }

        public void InitializeAll(int sampleRate, int channels)
        {
            lock (_lock)
            {
                foreach (var plugin in _plugins.Values)
                {
                    try
                    {
                        plugin.Initialize(sampleRate, channels);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to initialize plugin: {ex.Message}");
                    }
                }
            }
        }

        public void ResetAll()
        {
            lock (_lock)
            {
                foreach (var plugin in _plugins.Values)
                {
                    try
                    {
                        plugin.Reset();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to reset plugin: {ex.Message}");
                    }
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                foreach (var plugin in _plugins.Values)
                {
                    try
                    {
                        plugin.Reset();
                    }
                    catch { }
                }
                _plugins.Clear();
                _descriptors.Clear();
            }
        }
    }
}

