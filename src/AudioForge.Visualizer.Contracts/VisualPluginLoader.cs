using System.Reflection;
using System.Runtime.Loader;

namespace AudioForge.Visualizer.Contracts;

/// <summary>
/// Discovers and loads <see cref="IVisualiserPlugin"/> implementations from
/// DLLs in a plugin folder. Each plugin assembly is loaded into its own
/// collectible-free load context so plugins stay isolated.
/// </summary>
public static class VisualPluginLoader
{
    /// <summary>
    /// Scans <paramref name="pluginDirectory"/> for *.dll files and returns
    /// every concrete <see cref="IVisualiserPlugin"/> found.
    /// </summary>
    public static IReadOnlyList<IVisualiserPlugin> Load(string pluginDirectory)
    {
        var plugins = new List<IVisualiserPlugin>();
        if (!Directory.Exists(pluginDirectory)) return plugins;

        foreach (var dll in Directory.GetFiles(pluginDirectory, "*.dll"))
        {
            try
            {
                var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
                foreach (var type in asm.GetTypes())
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    if (!typeof(IVisualiserPlugin).IsAssignableFrom(type)) continue;
                    if (Activator.CreateInstance(type) is IVisualiserPlugin plugin)
                        plugins.Add(plugin);
                }
            }
            catch
            {
                // A broken plugin must never take down the host - skip it.
            }
        }
        return plugins;
    }
}
