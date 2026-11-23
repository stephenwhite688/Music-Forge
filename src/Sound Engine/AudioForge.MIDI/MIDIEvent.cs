namespace AudioForge.MIDI;

/// <summary>MIDI channel voice message types.</summary>
public enum MidiEventType : byte
{
    NoteOff = 0x80,
    NoteOn = 0x90,
    PolyAftertouch = 0xA0,
    ControlChange = 0xB0,
    ProgramChange = 0xC0,
    ChannelPressure = 0xD0,
    PitchBend = 0xE0,
}

/// <summary>
/// A single parsed MIDI event with its absolute time in seconds.
/// Tempo/meta events are resolved during parsing; only channel
/// voice events and program changes survive into the sequence.
/// </summary>
public readonly record struct MidiEvent(
    double TimeSeconds,
    int Channel,
    MidiEventType Type,
    int Data1,   // note number / controller / program
    int Data2);  // velocity / value / pitch bend LSB combined

/// <summary>A parsed Standard MIDI File.</summary>
public sealed class MidiSequence
{
    public IReadOnlyList<MidiEvent> Events { get; }
    public int TicksPerQuarter { get; }
    public double DurationSeconds { get; }
    public int TrackCount { get; }

    public MidiSequence(IReadOnlyList<MidiEvent> events, int ticksPerQuarter,
        double durationSeconds, int trackCount)
    {
        Events = events;
        TicksPerQuarter = ticksPerQuarter;
        DurationSeconds = durationSeconds;
        TrackCount = trackCount;
    }
}
