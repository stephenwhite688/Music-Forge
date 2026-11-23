namespace AudioForge.PluginHost
{
    public class PluginHostStatistics
    {
        public int TotalPlugins { get; set; }
        public int Filters { get; set; }
        public int Effects { get; set; }
        public int Generators { get; set; }
        public int Analyzers { get; set; }
        public int Custom { get; set; }

        public override string ToString()
        {
            return $"Total: {TotalPlugins} (Filters: {Filters}, Effects: {Effects}, " +
                   $"Generators: {Generators}, Analyzers: {Analyzers}, Custom: {Custom})";
        }
    }
}
