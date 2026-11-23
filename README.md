# MusicForge

A Windows music player built on a reusable pro-audio sound engine — with a
31-band graphic equaliser designed to stress-test the engine, real-time
visualisations, network speaker streaming, and a fully themeable UI.

## The product family

| Product | What it is |
|---|---|
| **MusicForge** | The Windows music player (this repo's main app) — library management, playlists, internet radio, EQ, visualiser, plugins, network speakers |
| **AudioForge SoundEngine** | The audio engine — WASAPI playback, DSP, streaming decode, mixing, CLAP/.NET plugin hosting |
| **SpeakerForge** | Network speaker discovery and multi-room streaming (UPnP/DLNA, Chromecast, AirPlay) |

## Features

- **Media library** — persistent folder/drive sources, recursive scanning, album/track browsing, artwork tracking, playlists with preserved order
- **Internet radio** — thousands of stations, favourites, now-playing metadata, same engine playback path as local files
- **Graphic equaliser** — 31-band ISO third-octave EQ with minimum/linear phase modes, factory + user presets, import/export
- **Visualiser** — real-time spectrum, waveform, spectrogram and meter displays with themes and plugins
- **Plugins** — CLAP native plugin hosting and managed .NET plugins injected into the playback chain
- **Networked speakers** — discover and stream to speakers around your LAN, synced with local output
- **Fully themeable UI** — colour presets, per-control colours, fonts, density, tooltips/hover, theme JSON import/export, persisted across sessions
- **Tools** — audio conversion/export, track tag editing, ReplayGain, MIDI device test, library backup/compact

## Tech stack

- C# / .NET 10, Windows Forms
- SQLite (Microsoft.Data.Sqlite) for library, playlists, radio, themes and settings
- TagLibSharp for metadata, NAudio for WASAPI
- AudioForge engine components (`src/Sound Engine/`)

## Build

Requires the .NET 10 SDK on Windows.

```powershell
dotnet build AudioForge.sln
```

Run the player:

```powershell
dotnet run --project src/UI/MusicForge.UI.csproj
```

## Repository layout

```
src/
  UI/                              MusicForge WinForms app
  Sound Engine/                    AudioForge engine components (playback, DSP, streaming, plugins)
  AudioForge.SoundEngine/          Engine compatibility layer
  AudioForge.NetworkSpeakers/      Network speaker services
  AudioForge.Visualizer.Contracts/ Visualiser plugin contracts
  AudioForge.VisualPlugins.Samples/ Sample visualiser plugins
  SpeakerForge/                    Network speaker engine
installer/                        Inno Setup installer script
Specs/                            Design specifications
```

## License

MIT — see [LICENSE](LICENSE). AudioForge SoundEngine components are licensed
for SDK use under the same terms.
