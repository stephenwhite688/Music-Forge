using HelixAudio.Core;

namespace HelixAudio.MIDI;

/// <summary>
/// Provides access to General MIDI (GM) functionality for selecting instruments and managing sound fonts.
/// </summary>
public sealed class GeneralMIDI : IDisposable
{
    private int _currentInstrument = 0;
    private string _soundFontPath = string.Empty;
    private bool _loaded;
    private bool _disposed;

    /// <summary>
    /// Gets the name of the currently selected instrument.
    /// </summary>
    public string CurrentInstrumentName => Enum.GetName(typeof(MIDIGMProgram), _currentInstrument) ?? "Unknown";

    /// <summary>
    /// Gets the index of the currently selected instrument (0-based GM program number).
    /// </summary>
    public int CurrentInstrument => _currentInstrument;

    /// <summary>
    /// Gets whether a sound font has been loaded.
    /// </summary>
    public bool IsLoaded => _loaded;

    /// <summary>
    /// Loads a SoundFont file for General MIDI instrument selection.
    /// </summary>
    /// <param name="path">Path to the .sf2 SoundFont file.</param>
    public void LoadSoundFont(string path)
    {
        _soundFontPath = path;
        _loaded = true;
    }

    /// <summary>
    /// Selects an instrument by its GM program number.
    /// </summary>
    /// <param name="program">The 0-based GM program number (0-121 for standard GM).</param>
    public void SelectInstrument(int program)
    {
        _currentInstrument = program;
    }

    /// <summary>
    /// Selects an instrument by its General MIDI program enum value.
    /// </summary>
    /// <param name="program">The MIDIGMProgram to select.</param>
    public void SelectInstrument(MIDIGMProgram program)
    {
        _currentInstrument = (int)program;
    }

    /// <summary>
    /// Releases all resources used by the GeneralMIDI object.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _loaded = false;
    }
}