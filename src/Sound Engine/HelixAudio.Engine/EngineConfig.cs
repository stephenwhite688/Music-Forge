namespace HelixAudio.Engine;
public class EngineConfig
{
    public int SampleRate { get; set; } = 44100;
    public int Channels { get; set; } = 2;
    public int BlockSize { get; set; } = 1024;
    public string BackendName { get; set; } = "Wasapi";
    public string DeviceId { get; set; }
    public bool LowLatency { get; set; } = false;
    public static EngineConfig CreateDefault() => new EngineConfig();
}
