namespace HelixAudio.MIDI;

/// <summary>
/// Represents a MIDI event with type, channel, data, and timestamp information.
/// </summary>
public class MIDIEvent
{
    /// <summary>
    /// The type of MIDI message (e.g., NoteOn, NoteOff, ControlChange).
    /// </summary>
    public MIDIMessageType Type { get; set; }

    /// <summary>
    /// The MIDI channel (0-15) or system channel for system messages.
    /// </summary>
    public byte Channel { get; set; }

    /// <summary>
    /// First data byte (note number, control change number, program number, etc.).
    /// </summary>
    public byte Data1 { get; set; }

    /// <summary>
    /// Second data byte (velocity, control value, etc.).
    /// </summary>
    public byte Data2 { get; set; }

    /// <summary>
    /// Timestamp of the event in microseconds since epoch.
    /// </summary>
    public long Timestamp { get; set; }

    /// <summary>
    /// Gets whether this is a note on message with non-zero velocity.
    /// </summary>
    public bool IsNoteOn => Type == MIDIMessageType.NoteOn && Data2 > 0;

    /// <summary>
    /// Gets whether this is a note off or velocity-zero note on.
    /// </summary>
    public bool IsNoteOff => Type == MIDIMessageType.NoteOff || (Type == MIDIMessageType.NoteOn && Data2 == 0);

    /// <summary>
    /// The note number for note events, or control change number for CC events.
    /// </summary>
    public int Note => Data1;

    /// <summary>
    /// The velocity for note events, or control value for CC events.
    /// </summary>
    public int Velocity => Data2;

    /// <summary>
    /// The control number for Control Change messages.
    /// </summary>
    public int Control => Data1;

    /// <summary>
    /// The controller value for Control Change messages.
    /// </summary>
    public int Value => Data2;

    /// <summary>
    /// Creates a new MIDIEvent with the specified message type and data.
    /// </summary>
    /// <param name="type">The MIDI message type.</param>
    /// <param name="channel">The MIDI channel or system channel.</param>
    /// <param name="data1">First data byte (note number, CC number, program number).</param>
    /// <param name="data2">Second data byte (velocity, CC value).</param>
    public MIDIEvent(MIDIMessageType type, byte channel, byte data1, byte data2 = 0)
    {
        Type = type;
        Channel = channel;
        Data1 = data1;
        Data2 = data2;
    }

    /// <summary>
    /// Returns a string representation of the MIDI event.
    /// </summary>
    /// <returns>A formatted string with type, channel, and data values.</returns>
    public override string ToString()
    {
        return $"{Type} Ch{Channel} D1:{Data1} D2:{Data2} @{Timestamp}";
    }
}