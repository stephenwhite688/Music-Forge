param(
    [string]$OutName = "Button Icon Specifications.docx"
)

$ErrorActionPreference = "Stop"

$sections = [ordered]@{}

$sections["Media Ribbon"] = @"
Select Drive|Full-colour icon of a computer hard disk drive - selects a drive as a media source
Select Folder|Full-colour icon of an open folder containing a music note - selects a folder as a media source
Recent Sources|Full-colour icon of a clock face over a folder - lists recently used media sources
Clear Source|Full-colour icon of an eraser wiping a folder - removes the current media source
Scan Library|Full-colour icon of a magnifying glass over a music library - scans sources into the library
Rescan Library|Full-colour icon of circular refresh arrows around a magnifying glass - rescans the library for changes
Save Library|Full-colour icon of a floppy disk with a music note - saves the media library
Load Library|Full-colour icon of an open folder with a music note - loads a saved media library
Delete Library|Full-colour icon of a trash can with a music note - deletes the media library
New Playlist|Full-colour icon of a playlist document with a green plus - creates a new playlist
Load Playlist|Full-colour icon of a playlist document with a folder - loads an existing playlist
Save Playlist|Full-colour icon of a playlist document with a floppy disk - saves the current playlist
Delete Playlist|Full-colour icon of a playlist document with a red cross - deletes the current playlist
View Artwork|Full-colour icon of a framed picture with a music note - views album artwork
Download Artwork|Full-colour icon of a picture with a downward arrow - downloads album artwork
Replace Artwork|Full-colour icon of a picture with circular swap arrows - replaces album artwork
Search|Full-colour icon of a magnifying glass - searches the library
Clear|Full-colour icon of an eraser - clears the search box
Statistics|Full-colour icon of a bar chart with a music note - shows library statistics
Maintenance|Full-colour icon of a wrench crossed with a music note - opens library maintenance tools
Settings|Full-colour icon of a gear cog - opens media settings
"@

$sections["Internet Radio Ribbon"] = @"
Browse Stations|Full-colour icon of a radio tower beside a list - browses the station directory
Countries|Full-colour icon of a globe - browses stations by country
Languages|Full-colour icon of a speech bubble with characters - browses stations by language
Genres|Full-colour icon of a price tag with a musical note - browses stations by genre
Refresh Directory|Full-colour icon of circular refresh arrows - reloads the station directory
Search|Full-colour icon of a magnifying glass - searches radio stations
Clear|Full-colour icon of an eraser - clears the station search box
Play|Full-colour icon of a green play triangle - plays the selected station
Stop|Full-colour icon of a red stop square - stops the station stream
Refresh Station|Full-colour icon of circular arrows around a radio - refreshes station data
Open Website|Full-colour icon of a globe with an arrow - opens the station homepage
Add Favourite|Full-colour icon of a gold star with a plus - adds station to favourites
Remove Favourite|Full-colour icon of a gold star with a minus - removes station from favourites
Presets|Full-colour icon of a radio preset dial - manages station presets
History|Full-colour icon of a clock with a curved arrow - shows connection history
Station Info|Full-colour icon of a lowercase i in a circle over a radio - shows station information
Bitrate|Full-colour icon of a speedometer gauge - shows stream bitrate
Codec|Full-colour icon of sound waves entering a microchip - shows stream codec
Current Track|Full-colour icon of a music note with a play triangle - shows the now-playing track
Diagnostics|Full-colour icon of a stethoscope over sound waves - runs radio diagnostics
Logs|Full-colour icon of a document with text lines - views radio logs
Settings|Full-colour icon of a gear cog - opens radio settings
"@

$sections["Visualiser Ribbon"] = @"
Start|Full-colour icon of a green play triangle over a waveform - starts the visualiser
Stop|Full-colour icon of a red stop square over a waveform - stops the visualiser
Pause|Full-colour icon of two pause bars over a waveform - pauses the visualiser
Full Screen|Full-colour icon of four corner arrows expanding - toggles full-screen visualiser
Waveform|Full-colour icon of an oscilloscope waveform line - selects waveform display
Spectrum|Full-colour icon of rainbow frequency bars - selects spectrum analyser display
Spectrogram|Full-colour icon of a scrolling heat-map grid - selects spectrogram display
Entertainment|Full-colour icon of a disco ball - selects entertainment visual modes
Stereo|Full-colour icon of two speaker channels labelled L and R - selects stereo layout
Surround|Full-colour icon of a 5.1 speaker layout diagram - selects surround layout
Spatial|Full-colour icon of a 3D sphere with sound waves - selects spatial audio view
Channels|Full-colour icon of a grid of channel knobs - selects channel view
FFT Size|Full-colour icon of a grid with a resolution symbol - selects FFT size
Window|Full-colour icon of a window frame - selects the FFT window function
Smoothing|Full-colour icon of a smoothed curve line - adjusts FFT smoothing
Peak Hold|Full-colour icon of held peak dots on a spectrum - toggles peak hold
Theme|Full-colour icon of a paint palette - selects the visualiser theme
Colours|Full-colour icon of a colour wheel - selects visualiser colours
Scale|Full-colour icon of a ruler with a log curve - selects frequency scale
Layout|Full-colour icon of rearranged panel boxes - manages visualiser layout
Track Info|Full-colour icon of an information tag - shows track overlay info
Technical Data|Full-colour icon of a spec sheet with braces - shows technical overlay data
Timecode|Full-colour icon of a timecode display - shows timecode overlay
Markers|Full-colour icon of a bookmark flag - manages overlay markers
Save|Full-colour icon of a floppy disk - saves the visualiser preset
Load|Full-colour icon of an open folder - loads a visualiser preset
Factory|Full-colour icon of a factory building - loads factory presets
Reset|Full-colour icon of a circular arrow returning - resets visualiser settings
Screenshot|Full-colour icon of a camera - captures a visualiser screenshot
Export|Full-colour icon of an arrow leaving a box - exports visualiser data
Diagnostics|Full-colour icon of a stethoscope over sound waves - runs visualiser diagnostics
Settings|Full-colour icon of a gear cog - opens visualiser settings
Panel|Full-colour icon of three horizontal sliders with a small gear - opens the group settings panel
"@

$sections["Graphic Equalizer Ribbon"] = @"
Media|Full-colour icon of a play triangle over a screen - targets the media playback engine
Internet Radio|Full-colour icon of a radio tower - targets the internet radio engine
Global Engine|Full-colour icon of a CPU chip with a sound wave - targets the entire audio engine
Status|Full-colour icon of a dashboard gauge - shows the current EQ target status
Enable|Full-colour icon of a power button with a check - enables the equaliser
Bypass|Full-colour icon of an arrow skipping over an EQ curve - bypasses the equaliser
Flat|Full-colour icon of a straight horizontal line - sets a flat EQ response
Compare|Full-colour icon of the letters A and B split - compares EQ states
10 Band|Full-colour icon of ten vertical sliders - selects 10-band EQ
15 Band|Full-colour icon of fifteen vertical sliders - selects 15-band EQ
31 Band|Full-colour icon of thirty-one vertical sliders - selects 31-band EQ
Custom|Full-colour icon of sliders with a wrench - selects custom band layout
Stereo Link|Full-colour icon of two linked chain rings over speakers - links stereo channels
Left|Full-colour icon of a speaker labelled L - selects the left channel
Right|Full-colour icon of a speaker labelled R - selects the right channel
Dual Mono|Full-colour icon of two mono speakers merged - selects dual mono mode
EQ Curve|Full-colour icon of a curved EQ response line - shows the EQ curve
Spectrum|Full-colour icon of rainbow frequency bars - shows the spectrum display
Pre/Post|Full-colour icon of two spectra with swap arrows - toggles pre/post-EQ view
Metering|Full-colour icon of a VU meter - shows input and output metering
Input Gain|Full-colour icon of an upward arrow into a slider - adjusts input gain
Output Gain|Full-colour icon of an upward arrow out of a slider - adjusts output gain
Headroom|Full-colour icon of a gauge with open space - monitors headroom
Clip Monitor|Full-colour icon of a warning triangle over a clipped waveform - monitors clipping
Factory|Full-colour icon of a factory building - loads factory EQ presets
User|Full-colour icon of a person silhouette - loads user EQ presets
Save|Full-colour icon of a floppy disk - saves the EQ preset
Load|Full-colour icon of an open folder - loads an EQ preset
Peak|Full-colour icon of a peak-hold meter - shows peak analysis
RMS|Full-colour icon of an RMS meter - shows RMS analysis
Dynamic Range|Full-colour icon of a range graph with peaks and valleys - shows dynamic range
Correlation|Full-colour icon of two phase waveforms - shows stereo correlation
DSP Load|Full-colour icon of a CPU with a load gauge - shows DSP usage
Latency|Full-colour icon of a clock with a delay arrow - shows processing latency
Statistics|Full-colour icon of a bar chart - shows engine statistics
Diagnostics|Full-colour icon of a stethoscope over sound waves - runs engine diagnostics
Panel|Full-colour icon of three horizontal sliders with a small gear - opens the group settings panel
"@

$sections["Networked Speakers Ribbon"] = @"
Scan|Full-colour icon of a radar sweep over a network - scans for speakers
Connect|Full-colour icon of a plug joining two sockets - connects to a speaker
Disconnect|Full-colour icon of a plug being pulled from a socket - disconnects a speaker
Refresh|Full-colour icon of circular refresh arrows - refreshes the device list
Speaker|Full-colour icon of a single speaker cabinet - routes audio to one speaker
Group|Full-colour icon of three linked speakers - routes audio to a speaker group
All|Full-colour icon of a grid of speakers - routes audio to all speakers
Create Group|Full-colour icon of linked speakers with a plus - creates a speaker group
Edit Group|Full-colour icon of linked speakers with a pencil - edits a speaker group
Delete Group|Full-colour icon of linked speakers with a trash can - deletes a speaker group
Rooms|Full-colour icon of a house with speaker icons - manages speaker rooms
Sync|Full-colour icon of two circular arrows linked - enables speaker synchronisation
Latency|Full-colour icon of a clock with a delay arrow - shows sync latency
Status|Full-colour icon of a dashboard gauge - shows sync status
Test Tone|Full-colour icon of a speaker emitting sound waves - plays a test tone
Left|Full-colour icon of a speaker labelled L - plays test tone on left channel
Right|Full-colour icon of a speaker labelled R - plays test tone on right channel
Stereo|Full-colour icon of two speakers labelled L and R - plays stereo test tone
Panel|Full-colour icon of three horizontal sliders with a small gear - opens the group settings panel
"@

$sections["Options Ribbon"] = @"
Themes|Full-colour icon of a paint palette over a window - selects the application theme
Colours|Full-colour icon of a colour wheel - selects interface colours
Fonts|Full-colour icon of the letters Aa in different typefaces - selects interface fonts
Icons|Full-colour icon of a grid of small icons - selects the icon set
Reset|Full-colour icon of a circular arrow returning - resets appearance settings
Layout|Full-colour icon of rearranged window panels - selects the interface layout
Windows|Full-colour icon of overlapping window rectangles - manages open windows
Ribbon|Full-colour icon of a ribbon strip with tabs - configures the ribbon
Tooltips|Full-colour icon of a speech bubble with a question mark - toggles tooltips
Accessibility|Full-colour icon of a person accessibility symbol - opens accessibility settings
Startup|Full-colour icon of a rocket lifting off - configures startup behaviour
Workspace|Full-colour icon of a desk with a briefcase - manages workspaces
Sessions|Full-colour icon of a clock with a history arrow - manages sessions
Profiles|Full-colour icon of a user profile card - manages user profiles
Defaults|Full-colour icon of sliders returning to a centre mark - restores default settings
Media Paths|Full-colour icon of a folder with a path line - manages media locations
Library|Full-colour icon of a bookshelf - manages library configuration
Backup|Full-colour icon of a shield over a database cylinder - manages backups
Import|Full-colour icon of a downward arrow into a box - imports settings or profiles
Export|Full-colour icon of an upward arrow out of a box - exports settings or profiles
Performance|Full-colour icon of a speedometer gauge - configures performance mode
Memory|Full-colour icon of a RAM memory chip - manages memory settings
Cache|Full-colour icon of a database cylinder with a lightning bolt - manages the cache
Logging|Full-colour icon of a pen writing on a log sheet - manages logging
Diagnostics|Full-colour icon of a stethoscope over sound waves - opens diagnostics
Contents|Full-colour icon of an open book - opens help contents
Search|Full-colour icon of a magnifying glass - searches help topics
Tutorials|Full-colour icon of a graduation cap - opens tutorials
FAQ|Full-colour icon of a speech bubble with a question mark - opens frequently asked questions
Support|Full-colour icon of a headset - opens support contact options
About|Full-colour icon of a lowercase i in a circle - shows about AudioForge
Contact|Full-colour icon of an envelope - opens contact options
Author|Full-colour icon of a fountain pen - shows author information
Credits|Full-colour icon of a clapperboard - shows credits
Licence|Full-colour icon of a certificate document - shows licence information
Version|Full-colour icon of a price tag with a version number - shows version information
Panel|Full-colour icon of three horizontal sliders with a small gear - opens the group settings panel
"@

$outPath = "H:\Audio forge with helix sound engine using winforms\Full colour button icon specs\$OutName"

$word = New-Object -ComObject Word.Application
$word.Visible = $false
try {
    $doc = $word.Documents.Add()
    $sel = $word.Selection
    $sel.Font.Name = "Segoe UI"
    $sel.Font.Size = 11

    # Title
    $sel.Font.Size = 22
    $sel.Font.Bold = 1
    $sel.TypeText("AudioForge Button Icon Specifications")
    $sel.TypeParagraph()
    $sel.Font.Size = 11
    $sel.Font.Bold = 0
    $sel.TypeText("Full-colour icons required for every ribbon button, delivered as multi-resolution .ico files (containing various built-in sizes, e.g. 16x16, 24x24, 32x32, 48x48) on a white background. Buttons that share an icon are listed with the same description and the same suggested file name - one file is reused for all of them. Name each delivered icon file exactly as shown in the Suggested file name column. Transport bar icons are already specified and are not included.")
    $sel.TypeParagraph()
    $sel.TypeParagraph()

    foreach ($ribbon in $sections.Keys) {
        # Ribbon heading
        try { $sel.Style = $doc.Styles.Item("Heading 1") } catch { }
        $sel.TypeText($ribbon)
        $sel.TypeParagraph()
        try { $sel.Style = $doc.Styles.Item("Normal") } catch { }

        $lines = $sections[$ribbon] -split "`r?`n" | Where-Object { $_.Trim().Length -gt 0 }
        $table = $doc.Tables.Add($sel.Range, ($lines.Count + 1), 3)
        $table.Borders.Enable = 1
        $table.Range.Font.Name = "Segoe UI"
        $table.Range.Font.Size = 9

        $table.Cell(1, 1).Range.Text = "Button"
        $table.Cell(1, 2).Range.Text = "Icon Description"
        $table.Cell(1, 3).Range.Text = "Suggested file name"
        $table.Rows.Item(1).Range.Font.Bold = 1

        for ($i = 0; $i -lt $lines.Count; $i++) {
            $parts = $lines[$i] -split "\|", 2
            $desc = $parts[1].Trim()
            if ($desc.EndsWith(".")) {
                $desc = $desc.Substring(0, $desc.Length - 1) + ", on a white background."
            } else {
                $desc += " on a white background."
            }
            $fname = ($parts[0].Trim().ToLower() -replace '[^a-z0-9]+', '_').Trim('_') + ".ico"
            $table.Cell($i + 2, 1).Range.Text = $parts[0].Trim()
            $table.Cell($i + 2, 2).Range.Text = $desc
            $table.Cell($i + 2, 3).Range.Text = $fname
        }

        # Move selection past the table
        $sel.EndKey(6) | Out-Null
        $sel.TypeParagraph()
    }

    $doc.SaveAs([ref]$outPath, [ref]16)
    $doc.Close()
    Write-Output "Saved: $outPath"
}
finally {
    $word.Quit()
    [System.Runtime.Interopservices.Marshal]::ReleaseComObject($word) | Out-Null
}
