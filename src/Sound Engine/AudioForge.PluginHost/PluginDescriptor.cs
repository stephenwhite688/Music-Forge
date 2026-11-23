using System;

namespace AudioForge.PluginHost
{
    public class PluginDescriptor
    {
        public string FilePath { get; }
        public string ClassName { get; }
        public string Name { get; }
        public string Author { get; }
        public string Version { get; }
        public PluginType Type { get; }

        public PluginDescriptor(string filePath, string className, string name,
                               string author, string version, PluginType type)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            ClassName = className ?? throw new ArgumentNullException(nameof(className));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Author = author ?? string.Empty;
            Version = version ?? "1.0.0";
            Type = type;
        }

        public override string ToString()
        {
            return $"{Name} v{Version} by {Author} ({Type}) - {ClassName}";
        }
    }
}
