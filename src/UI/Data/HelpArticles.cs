namespace MusicForge.UI.Data;

/// <summary>
/// Built-in help articles seeded into the HelpTopics table on first run.
/// Content uses a tiny markup: "# " heading, "## " subheading, "- " bullet.
/// </summary>
internal static class HelpArticles
{
    public sealed record Article(string Key, string Title, string Category, int SortOrder, string Content);

    public static IReadOnlyList<Article> All { get; } = new[]
    {
        new Article("quickstart", "Quick Start", "Getting Started", 0, """
# Quick Start

Welcome to MusicForge - your music, your library, your way.

## 1. Add your music
- Open the Media tab.
- In the Source group, choose Select Folder (or Select Drive for a whole drive).
- Your folders appear as library sources and persist between sessions.

## 2. Scan your library
- In the Library group, click Scan Library (or Rescan Library).
- MusicForge indexes every supported audio file it finds, including tags and artwork.

## 3. Play something
- Browse albums or tracks in the library view.
- Double-click a track, or select tracks and use the transport bar at the bottom.
- Space toggles play/pause.

## 4. Shape the sound
- Graphic Equalizer tab: 31-band EQ with presets - try Factory Presets first.
- Visualizer tab: live spectrum/waveform displays that react to your music.

## 5. Go further
- Playlists: create, load and play ordered track lists (Media > Playlist).
- Internet radio: browse thousands of stations (Internet radio tab).
- Networked speakers: stream to speakers around your home (Networked Speakers tab).
- Plugins: extend the sound engine with CLAP and .NET plugins (Plugins tab).
- Make it yours: themes, colours, fonts and layouts in the Options tab.

Tip: everything you change in Options is saved automatically and restored next launch.
"""),

        new Article("media", "Media Tab", "Tabs", 10, """
# Media Tab

Your local music library, playlists and playback tools.

## Source group
- Select Drive / Select Folder: add a folder or whole drive as a library source. Sources persist.
- Clear Sources: remove all sources (files are not deleted).
- Show / Hide Library: toggle the library browser pane.

## Library group
- Scan Library / Rescan Library: index audio files under your sources. Rescan picks up new or moved files.
- Library Properties: source list and scan details.
- Export Backup / Backup Library: save a backup of your library database.

## Playlist group
- New Playlist, Load Playlist, Play Playlist, Delete Playlist.
- Playlist order is preserved exactly as you arranged it.
- Close Playlist returns to normal library browsing.

## Search
- Search box filters the library by title, artist, album or genre.

## Output
- Dropdown selects the local audio output device (speakers, headphones, HDMI, Bluetooth).
- "System Default" follows Windows - it switches automatically when you connect headphones.

## Tools
- Open Stream URL: play an internet stream directly through the engine.
- Convert / Export Audio: encode tracks to other formats.
- Track Properties: view and edit tags, artwork, ReplayGain.
- MIDI Devices: list and test MIDI inputs/outputs.
- Library Statistics, Compact Database: library maintenance.

## Transport bar (bottom of window)
- Play/pause, stop, previous, next, repeat/shuffle, volume, mini visualiser.
- Space = play/pause.
"""),

        new Article("radio", "Internet Radio Tab", "Tabs", 20, """
# Internet Radio Tab

Stream thousands of radio stations through the same Helix playback engine as local music.

## Directory group
- Load Directory / Refresh Directory: download the station directory.
- Browse views: All Stations, By Country, By Language, By Genre, Popular, Recently Added.

## Search
- Filter stations by name, genre, country or language.

## Playback
- Play Station streams the selected station.
- Stop Station ends playback.
- Refresh Station reconnects a dropped stream.

## Favourites
- Add / Remove Favourite and Show Favourites for your saved stations.

## Information
- Station Properties, Stream Information (codec, bitrate), Current Track (now-playing metadata when the station provides it), Station Homepage, Reliability score.

Tips: stations appear with logos where available. Favourite the ones you love - they persist between sessions.
"""),

        new Article("eq", "Graphic Equalizer Tab", "Tabs", 30, """
# Graphic Equalizer Tab

A full 31-band ISO third-octave equaliser - the engine's stress test and your tone-shaping tool.

## The display
- 31 vertical sliders, one per third-octave band from 20 Hz to 20 kHz.
- Drag a slider to boost or cut that band; the curve shows the resulting response.
- Readouts show each band's gain and centre frequency.

## Ribbon groups
- Target: choose what the EQ processes (master output, preamp, specific bus).
- EQ Control: enable/disable the equaliser, input/output level, gain range.
- Band Mode: Minimum Phase (low latency) or Linear Phase (constant ~85 ms delay, no phase smearing).
- Channels: stereo linking options - link bands for identical L/R curves or adjust channels separately.
- Display: curve style, grid, scale options.
- Levels: input/output metering and trim.
- Presets: Factory Presets (built-in curves) and User Presets (your own, saved to the database).
  - Import / Export shares preset files between machines.
- Engine: live engine diagnostics for the EQ path.

## Tips
- Small moves go a long way - +/-3 dB is often plenty.
- Linear Phase is best for mixing/mastering; Minimum Phase for live monitoring.
- Save your favourite curves as User Presets before experimenting.
"""),

        new Article("visualizer", "Visualizer Tab", "Tabs", 40, """
# Visualizer Tab

Real-time audio visualisations driven by the Helix analysis engine.

## Ribbon groups
- Visualiser: start/stop the display, choose the visualiser plugin if several are installed.
- Display Mode: switch between spectrum analyser, waveform, oscilloscope, VU-style and combined modes.
- FFT: FFT size and window settings - larger FFTs give finer frequency resolution, smaller ones react faster.
- Display: colours, line styles and background - follows your theme plus visualiser-specific settings.
- Overlays: optional peak-hold, grid and RMS overlays.
- Tools: capture/snapshot options and visualiser diagnostics.

## Themes
- Built-in themes: Dark, High Contrast, Heat, Neon, Grayscale.
- Your app theme colours feed through where supported.

## Plugins
- Visualiser plugins extend the display options - see the Plugins tab.

Tip: the mini visualiser in the transport bar mirrors the main display at low cost.
"""),

        new Article("speakers", "Networked Speakers Tab", "Tabs", 50, """
# Networked Speakers Tab

Stream your music to network speakers around your home (SpeakerForge engine).

## Finding speakers
- Devices > Scan Network: discover UPnP/DLNA, Chromecast, AirPlay and compatible speakers on your LAN.
- Refresh Devices re-scans; discovered devices persist in the database.
- The list shows each device's name, type and status. Tick the speakers you want.
- The "Local" tick includes this PC's own output in playback.

## Playback menu
- Play Selected Speakers: stream to ticked devices only.
- Local + Selected Speakers: this PC plus the ticked network speakers, synchronised.
- Play All Speakers: every discovered device.
- Play Local Only: just this PC (same as normal playback).
- Stop All Speakers: halt every stream.

## Per-speaker controls
- Volume sliders next to each device adjust that speaker's level.
- Stop All Playback in the Devices group kills every active stream.

## Requirements
- Speakers and PC must be on the same network.
- Some speaker types need their companion app once to enable network audio.
"""),

        new Article("plugins", "Plugins Tab", "Tabs", 60, """
# Plugins Tab

Extend the sound engine with audio plugins.

## Supported formats
- CLAP: modern native plugin format - load .clap plugins for effects, instruments and analysis.
- .NET plugins: managed HelixAudio plugins built against the plugin SDK.

## Using plugins
- The Plugins menu lists discovered plugins grouped by type.
- Insert a plugin into the playback chain; its parameters appear in the plugin's own editor window.
- Enable/disable plugins without removing them.

## Installing plugins
- CLAP plugins: drop the .clap file (and its folder) into the standard CLAP search paths, then refresh the plugin list.
- .NET plugins: place the managed DLL where the plugin host scans for it.

Note: VST3 support is planned for a future release - it is the biggest remaining format gap.
"""),

        new Article("options", "Options Tab", "Tabs", 70, """
# Options Tab

Everything about how MusicForge looks, starts and behaves.

## Appearance
- Themes: Dark, Light, Blue, Green, Red, High Contrast presets - applied live.
- Colours: individual pickers for window, surfaces, accents, text, borders, EQ and visualiser colours.
- Fonts: choose the UI font, larger/smaller text, antialiasing.
- Icons: icon pack selection (more packs coming).
- Reset: restore theme/font defaults.

## Interface
- Layout: Compact / Standard / Comfortable density.
- Tooltips & Hover: show tooltips, mouse-over highlight effects.
- Reset Window Layout.

## Startup
- Startup Tab: open on a fixed tab, or Automatic (last used).

## Tools
- Library backup/restore, settings export/import, performance profile export/import.
- Performance modes: Low (pauses visualisers), Balanced, High.
- Engine logging on/off, cache clearing, diagnostics folder.

## Help / About
- User Guide (this help system), keyboard shortcuts, report a problem, diagnostics log.
- About, version, licence and change log.

All options persist automatically between sessions.
"""),
    };
}
