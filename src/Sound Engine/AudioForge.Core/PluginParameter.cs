namespace AudioForge.Core
{
    public class PluginParameter
    {
        public string Id { get; }
        public string Name { get; }
        public float Min { get; }
        public float Max { get; }
        public float Default { get; }

        public PluginParameter(string id, string name, float min, float max, float @default)
        {
            Id = id;
            Name = name;
            Min = min;
            Max = max;
            Default = @default;
        }
    }
}
