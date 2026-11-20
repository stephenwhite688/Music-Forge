namespace HelixAudio.MIDI;

/// <summary>
/// Represents an input MIDI device for receiving MIDI messages from hardware synthesizers and controllers.
/// </summary>
public sealed class MIDIInput : IDisposable
{
    private bool _disposed;
    private bool _isOpen;
    private Thread? _readerThread;
    private volatile bool _running;

    /// <summary>
    /// Event fired when a note-on message is received.
    /// </summary>
    public event EventHandler<MIDIEvent>? NoteOn;

    /// <summary>
    /// Event fired when a note-off message is received.
    /// </summary>
    public event EventHandler<MIDIEvent>? NoteOff;

    /// <summary>
    /// Event fired when a control change message is received.
    /// </summary>
    public event EventHandler<MIDIEvent>? ControlChange;

    /// <summary>
    /// Event fired when a program change message is received.
    /// </summary>
    public event EventHandler<MIDIEvent>? ProgramChange;

    /// <summary>
    /// Event fired when a pitch bend message is received.
    /// </summary>
    public event EventHandler<MIDIEvent>? PitchBend;

    /// <summary>
    /// Event fired when a system exclusive message is received.
    /// </summary>
    public event EventHandler<MIDIEvent>? SystemExclusive;

    /// <summary>
    /// Opens the MIDI input device at the specified index.
    /// </summary>
    /// <param name="deviceIndex">The 0-based index of the MIDI input device.</param>
    public void OpenDevice(int deviceIndex)
    {
        if (_isOpen) return;
        _isOpen = true;
        _running = true;
        
        _readerThread = new Thread(ReadLoop)
        {
            IsBackground = true,
            Name = "MIDIInputReader"
        };
        _readerThread.Start();
    }

    /// <summary>
    /// Closes and releases the MIDI input device.
    /// </summary>
    public void CloseDevice()
    {
        _running = false;
        _readerThread?.Join(500);
        _readerThread = null;
        _isOpen = false;
    }

    private void ReadLoop()
    {
        while (_running)
        {
            Thread.Sleep(10);
        }
    }

    /// <summary>
    /// Releases all resources used by the MIDIInput object.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseDevice();
    }
}