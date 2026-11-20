namespace HelixAudio.Mixing
{
    public class MixerChannel
    {
        public int Id { get; }
        public float Gain { get; set; } = 1.0f;
        public float Pan { get; set; } = 0.0f; // -1 left, 0 center, +1 right
        public bool Mute { get; set; } = false;
        public bool Solo { get; set; } = false;
        public bool Enabled { get; set; } = true;

        public MixerChannel(int id)
        {
            Id = id;
        }
    }
}
