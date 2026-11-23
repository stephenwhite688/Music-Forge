using System;
using System.Collections.Generic;

namespace AudioForge.PluginHost
{
    public static class PluginParameterController
    {
        public static Dictionary<string, float> GetParameters(IAudioPlugin plugin)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));

            try
            {
                return plugin.GetParameters();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get parameters: {ex.Message}");
                return new Dictionary<string, float>();
            }
        }

        public static void SetParameter(IAudioPlugin plugin, string name, float value)
        {
            if (plugin == null)
                throw new ArgumentNullException(nameof(plugin));

            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Parameter name cannot be null or empty.", nameof(name));

            plugin.SetParameter(name, value);
        }

        public static bool TrySetParameter(IAudioPlugin plugin, string name, float value)
        {
            if (plugin == null || string.IsNullOrEmpty(name))
                return false;

            try
            {
                plugin.SetParameter(name, value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static float GetParameter(IAudioPlugin plugin, string name, float defaultValue = 0.0f)
        {
            if (plugin == null || string.IsNullOrEmpty(name))
                return defaultValue;

            try
            {
                var parameters = plugin.GetParameters();
                return parameters.TryGetValue(name, out float value) ? value : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        public static bool TryGetParameter(IAudioPlugin plugin, string name, out float value)
        {
            value = 0.0f;

            if (plugin == null || string.IsNullOrEmpty(name))
                return false;

            try
            {
                var parameters = plugin.GetParameters();
                if (parameters.TryGetValue(name, out float result))
                {
                    value = result;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
