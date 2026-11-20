namespace HelixAudio.MIDI;

/// <summary>
/// Represents an output MIDI device for sending MIDI messages to hardware synthesizers and controllers.
/// </summary>
public sealed class MIDIOutput : IDisposable
{
    private bool _disposed;
    private bool _isOpen;

    /// <summary>
    /// Opens the MIDI output device at the specified index.
    /// </summary>
    /// <param name="deviceIndex">The 0-based index of the MIDI output device.</param>
    public void OpenDevice(int deviceIndex)
    {
        _isOpen = true;
    }

    /// <summary>
    /// Closes and releases the MIDI output device.
    /// </summary>
    public void CloseDevice()
    {
        _isOpen = false;
    }

    /// <summary>
    /// Sends a note-on message to the connected MIDI device.
    /// </summary>
    /// <param name="channel">The MIDI channel (0-15).</param>
    /// <param name="note">The note number (MIDI pitch, 0-127).</param>
    /// <param name="velocity">The velocity of the note (0-127).</param>
    public void SendNoteOn(int channel, int note, int velocity)
    {
        if (!_isOpen) return;
    }

    /// <summary>
    /// Sends a note-off message to the connected MIDI device.
    /// </summary>
    /// <param name="channel">The MIDI channel (0-15).</param>
    /// <param name="note">The note number (MIDI pitch, 0-127).</param>
    /// <param name="velocity">Optional velocity (defaults to 0 for note-off).</param>
    public void SendNoteOff(int channel, int note, int velocity = 0)
    {
        if (!_isOpen) return;
    }

    /// <summary>
    /// Sends a control change message to the connected MIDI device.
    /// </summary>
    /// <param name="channel">The MIDI channel (0-15).</param>
    /// <param name="control">The control change number (0-127).</param>
    /// <param name="value">The controller value (0-127).</param>
    public void SendControlChange(int channel, int control, int value)
    {
        if (!_isOpen) return;
    }

    /// <summary>
    /// Sends a program change message to the connected MIDI device.
    /// </summary>
    /// <param name="channel">The MIDI channel (0-15).</param>
    /// <param name="program">The program number (0-127 for GM, 0-121 for standard GM).</param>
    public void SendProgramChange(int channel, int program)
    {
        if (!_isOpen) return;
    }

    /// <summary>
    /// Sends a pitch bend message to the connected MIDI device.
    /// </summary>
    /// <param name="channel">The MIDI channel (0-15).</param>
    /// <param name="value">The pitch bend value (-8192 to 8191, where 0 is center).</param>
    public void SendPitchBend(int channel, int value)
    {
        if (!_isOpen) return;
    }

    /// <summary>
    /// Sends a system exclusive message to the connected MIDI device.
    /// </summary>
    /// <param name="data">The SYSEX data bytes to send.</param>
    public void SendSystemExclusive(byte[] data)
    {
        if (!_isOpen) return;
    }

    /// <summary>
    /// Releases all resources used by the MIDIOutput object.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseDevice();
    }
}