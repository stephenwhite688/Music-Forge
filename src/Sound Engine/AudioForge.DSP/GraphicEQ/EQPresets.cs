using System;
using System.Linq;
using System.Collections.Generic;

namespace AudioForge.DSP.GraphicEQ
{
    public class EQPreset
    {
        public string Name { get; }
        public float[] Gains { get; }

        public EQPreset(string name, float[] gains)
        {
            Name = name;
            Gains = gains;
        }
    }

    public static class EQPresets
    {
        public static EQPreset Rock = new EQPreset("Rock",
            Enumerable.Repeat(0f, 31).ToArray());

        public static EQPreset Pop = new EQPreset("Pop",
            Enumerable.Repeat(0f, 31).ToArray());

        public static EQPreset Flat = new EQPreset("Flat",
            Enumerable.Repeat(0f, 31).ToArray());

        public static IEnumerable<EQPreset> All =>
            new[] { Rock, Pop, Flat };

        public static EQPreset? Get(string name) =>
            All.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
