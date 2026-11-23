# MusicForge Changelog

All notable changes to MusicForge are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- Global UI theming: 6 presets (Dark, Light, Blue, Green, Red, High Contrast),
  per-colour pickers, font picker, density modes, theme import/export (.mf-theme.json)
- Startup splash screen
- Startup tab selection (fixed or last-used)
- UI performance modes (Low / Balanced / High) with real rendering savings
- Engine logging on/off toggle
- Options: database backup/restore, profile export
- New full-bleed application icon

### Fixed
- NU1104 restore errors from stale HelixAudio.Filters project reference
- Taskbar icon rendering (old icon artwork filled ~11% of frame)

## [1.0.0] - Initial public build

### Added
- Music library: source folders/drives, recursive scan, album/track browsing
- Playlist creation and management
- Playback through the AudioForge (HelixAudio) engine: WASAPI output,
  EQ, visualiser taps, CLAP plugin hosting
- Internet radio: directory search, favourites, presets
- Graphic equaliser: 31-band ISO EQ, presets, A/B, per-channel link/unlink
- Visualiser: 6 display modes, themes, overlays, plugin support
- Media tools: stream URL open (HLS/DASH), audio convert/export
  (MP3/M4A-AAC/OGG/WAV), track properties with ReplayGain + BPM
- MIDI device enumeration and test
- Network speaker discovery and grouped playback (SpeakerForge)
- Shuffle and repeat modes
- Play count / last-played tracking
- Self-contained single-file build; Inno Setup installer script
- Crash logging (UI, background, and audio-thread exceptions)
- Automatic migration of legacy AudioForge library database
