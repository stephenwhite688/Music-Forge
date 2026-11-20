namespace HelixAudio.DSP._3D;

public enum SpatialMode
{
    None,
    Pan,
    HRTF,
    Binaural,
    Ambisonic,
    Surround
}

public enum AmbisonicOrder
{
    First = 1,
    Second = 2,
    Third = 3
}

public enum SurroundLayout
{
    None,
    Surround5_1,
    Surround7_1,
    DolbyAtmos
}

public enum HRTFProfile
{
    Default,
    Individualized,
    Custom
}
