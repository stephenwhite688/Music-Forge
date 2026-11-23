using System;
using System.Linq;
using System.Collections.Generic;

namespace AudioForge.DSP.MasterEQ
{
    public class MasterEQPreset
    {
        public string Name { get; }
        public float[] Gains { get; }

        public MasterEQPreset(string name, float[] gains)
        {
            Name = name;
            Gains = gains;
        }
    }

    public static class MasterEQPresets
    {
        public static MasterEQPreset Flat = new MasterEQPreset(
            "Flat", Enumerable.Repeat(0f, 64).ToArray());

        public static MasterEQPreset BassBoost = new MasterEQPreset(
            "BassBoost", Enumerable.Range(0, 64)
                .Select(i => i < 10 ? 4f : 0f).ToArray());

        public static MasterEQPreset AirBoost = new MasterEQPreset(
            "AirBoost", Enumerable.Range(0, 64)
                .Select(i => i > 50 ? 3f : 0f).ToArray());

        public static IEnumerable<MasterEQPreset> All =>
            new[] { Flat, BassBoost, AirBoost };

        public static MasterEQPreset? Get(string name) =>
            All.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
