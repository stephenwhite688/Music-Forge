namespace MusicForge.UI.Data;

/// <summary>
/// Full SQLite schema for AudioForge. Stores metadata, references, paths and
/// settings only — media files, artwork, logos, log exports and backups live
/// on the filesystem outside the database.
/// </summary>
internal static class Schema
{
    public const string Sql = @"
PRAGMA foreign_keys = ON;

-- ===================== MEDIA LIBRARY =====================
CREATE TABLE IF NOT EXISTS Libraries (
    Id INTEGER PRIMARY KEY,
    LibraryName TEXT NOT NULL,
    CreationDate TEXT NOT NULL,
    LastScanDate TEXT,
    LibraryVersion INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS LibrarySources (
    Id INTEGER PRIMARY KEY,
    LibraryId INTEGER NOT NULL REFERENCES Libraries(Id) ON DELETE CASCADE,
    DriveLetter TEXT,
    FolderPath TEXT NOT NULL,
    SourceType TEXT NOT NULL,
    DateAdded TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_LibrarySources_LibraryId ON LibrarySources(LibraryId);

CREATE TABLE IF NOT EXISTS Artists (
    Id INTEGER PRIMARY KEY,
    ArtistName TEXT NOT NULL,
    SortName TEXT
);
CREATE INDEX IF NOT EXISTS IX_Artists_ArtistName ON Artists(ArtistName);
CREATE INDEX IF NOT EXISTS IX_Artists_SortName ON Artists(SortName);

CREATE TABLE IF NOT EXISTS Albums (
    Id INTEGER PRIMARY KEY,
    AlbumName TEXT,
    ArtistId INTEGER REFERENCES Artists(Id) ON DELETE SET NULL,
    Year INTEGER,
    Genre TEXT,
    ArtworkPath TEXT
);
CREATE INDEX IF NOT EXISTS IX_Albums_AlbumName ON Albums(AlbumName);
CREATE INDEX IF NOT EXISTS IX_Albums_ArtistId ON Albums(ArtistId);

CREATE TABLE IF NOT EXISTS Tracks (
    Id INTEGER PRIMARY KEY,
    Title TEXT,
    ArtistId INTEGER REFERENCES Artists(Id) ON DELETE SET NULL,
    AlbumId INTEGER REFERENCES Albums(Id) ON DELETE SET NULL,
    Genre TEXT,
    Year INTEGER,
    TrackNumber INTEGER,
    DurationSeconds REAL,
    FilePath TEXT NOT NULL UNIQUE,
    FileSize INTEGER,
    DateAdded TEXT NOT NULL,
    LastPlayed TEXT,
    PlayCount INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS IX_Tracks_Title ON Tracks(Title);
CREATE INDEX IF NOT EXISTS IX_Tracks_ArtistId ON Tracks(ArtistId);
CREATE INDEX IF NOT EXISTS IX_Tracks_AlbumId ON Tracks(AlbumId);
CREATE INDEX IF NOT EXISTS IX_Tracks_Genre ON Tracks(Genre);
CREATE INDEX IF NOT EXISTS IX_Tracks_Year ON Tracks(Year);
CREATE INDEX IF NOT EXISTS IX_Tracks_LastPlayed ON Tracks(LastPlayed);

CREATE TABLE IF NOT EXISTS Artwork (
    Id INTEGER PRIMARY KEY,
    ArtworkPath TEXT NOT NULL,
    AlbumId INTEGER REFERENCES Albums(Id) ON DELETE CASCADE,
    LastUpdated TEXT
);
CREATE INDEX IF NOT EXISTS IX_Artwork_AlbumId ON Artwork(AlbumId);

-- ===================== PLAYLIST SYSTEM =====================
CREATE TABLE IF NOT EXISTS Playlists (
    Id INTEGER PRIMARY KEY,
    PlaylistName TEXT NOT NULL,
    CreationDate TEXT NOT NULL,
    ModifiedDate TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS PlaylistTracks (
    Id INTEGER PRIMARY KEY,
    PlaylistId INTEGER NOT NULL REFERENCES Playlists(Id) ON DELETE CASCADE,
    TrackId INTEGER NOT NULL REFERENCES Tracks(Id) ON DELETE CASCADE,
    PlaylistPosition INTEGER NOT NULL,
    UNIQUE (PlaylistId, PlaylistPosition)
);
CREATE INDEX IF NOT EXISTS IX_PlaylistTracks_PlaylistId ON PlaylistTracks(PlaylistId, PlaylistPosition);
CREATE INDEX IF NOT EXISTS IX_PlaylistTracks_TrackId ON PlaylistTracks(TrackId);

-- ===================== INTERNET RADIO =====================
CREATE TABLE IF NOT EXISTS Stations (
    Id INTEGER PRIMARY KEY,
    StationName TEXT NOT NULL,
    StreamUrl TEXT NOT NULL,
    HomepageUrl TEXT,
    Country TEXT,
    Language TEXT,
    GenreTags TEXT,
    Codec TEXT,
    Bitrate INTEGER,
    ReliabilityScore REAL,
    LogoUrl TEXT
);
CREATE INDEX IF NOT EXISTS IX_Stations_StationName ON Stations(StationName);
CREATE INDEX IF NOT EXISTS IX_Stations_Country ON Stations(Country);
CREATE INDEX IF NOT EXISTS IX_Stations_Language ON Stations(Language);

CREATE TABLE IF NOT EXISTS RadioFavourites (
    Id INTEGER PRIMARY KEY,
    StationId INTEGER NOT NULL REFERENCES Stations(Id) ON DELETE CASCADE,
    DateAdded TEXT NOT NULL,
    UNIQUE (StationId)
);

CREATE TABLE IF NOT EXISTS RadioPresets (
    Id INTEGER PRIMARY KEY,
    PresetName TEXT NOT NULL,
    StationId INTEGER NOT NULL REFERENCES Stations(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS RadioHistory (
    Id INTEGER PRIMARY KEY,
    StationId INTEGER NOT NULL REFERENCES Stations(Id) ON DELETE CASCADE,
    ConnectionDate TEXT NOT NULL,
    ConnectionTime TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_RadioHistory_StationId ON RadioHistory(StationId);

CREATE TABLE IF NOT EXISTS RadioRecentlyPlayed (
    Id INTEGER PRIMARY KEY,
    StationId INTEGER NOT NULL REFERENCES Stations(Id) ON DELETE CASCADE,
    LastPlayed TEXT NOT NULL,
    UNIQUE (StationId)
);

CREATE TABLE IF NOT EXISTS StationMetadataCache (
    Id INTEGER PRIMARY KEY,
    StationId INTEGER NOT NULL REFERENCES Stations(Id) ON DELETE CASCADE,
    LastValidTrack TEXT,
    Timestamp TEXT NOT NULL,
    ReliabilityRating REAL,
    UNIQUE (StationId)
);

-- ===================== SPEAKERS =====================
CREATE TABLE IF NOT EXISTS Devices (
    Id INTEGER PRIMARY KEY,
    DeviceName TEXT NOT NULL,
    Manufacturer TEXT,
    Model TEXT,
    DeviceType TEXT,
    NetworkAddress TEXT,
    LastSeen TEXT
);
CREATE INDEX IF NOT EXISTS IX_Devices_NetworkAddress ON Devices(NetworkAddress);

CREATE TABLE IF NOT EXISTS SpeakerGroups (
    Id INTEGER PRIMARY KEY,
    GroupName TEXT NOT NULL,
    CreationDate TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS SpeakerGroupMembers (
    Id INTEGER PRIMARY KEY,
    GroupId INTEGER NOT NULL REFERENCES SpeakerGroups(Id) ON DELETE CASCADE,
    DeviceId INTEGER NOT NULL REFERENCES Devices(Id) ON DELETE CASCADE,
    UNIQUE (GroupId, DeviceId)
);

CREATE TABLE IF NOT EXISTS Rooms (
    Id INTEGER PRIMARY KEY,
    RoomName TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS RoomMembers (
    Id INTEGER PRIMARY KEY,
    RoomId INTEGER NOT NULL REFERENCES Rooms(Id) ON DELETE CASCADE,
    DeviceId INTEGER NOT NULL REFERENCES Devices(Id) ON DELETE CASCADE,
    UNIQUE (RoomId, DeviceId)
);

CREATE TABLE IF NOT EXISTS SpeakerProfiles (
    Id INTEGER PRIMARY KEY,
    ProfileName TEXT NOT NULL,
    ConfigurationData TEXT
);

-- ===================== VISUALISER =====================
CREATE TABLE IF NOT EXISTS VisualiserPresets (
    Id INTEGER PRIMARY KEY,
    PresetName TEXT NOT NULL,
    DisplayMode TEXT,
    FftSettings TEXT,
    Theme TEXT,
    ColourSettings TEXT
);

CREATE TABLE IF NOT EXISTS VisualiserLayouts (
    Id INTEGER PRIMARY KEY,
    LayoutName TEXT NOT NULL,
    LayoutConfiguration TEXT
);

-- ===================== MIXER =====================
CREATE TABLE IF NOT EXISTS MixerPresets (
    Id INTEGER PRIMARY KEY,
    PresetName TEXT NOT NULL,
    MixerConfiguration TEXT
);

CREATE TABLE IF NOT EXISTS EqPresets (
    Id INTEGER PRIMARY KEY,
    PresetName TEXT NOT NULL,
    EqConfiguration TEXT
);

CREATE TABLE IF NOT EXISTS EffectsPresets (
    Id INTEGER PRIMARY KEY,
    PresetName TEXT NOT NULL,
    EffectsConfiguration TEXT
);

CREATE TABLE IF NOT EXISTS RoutingPresets (
    Id INTEGER PRIMARY KEY,
    PresetName TEXT NOT NULL,
    RoutingConfiguration TEXT
);

-- ===================== APPLICATION SETTINGS =====================
CREATE TABLE IF NOT EXISTS GeneralSettings (
    Id INTEGER PRIMARY KEY CHECK (Id = 1),
    StartupMode TEXT,
    SelectedTheme TEXT,
    InterfaceLayout TEXT,
    LastOpenedMode TEXT
);
INSERT OR IGNORE INTO GeneralSettings (Id) VALUES (1);

CREATE TABLE IF NOT EXISTS WindowLayouts (
    Id INTEGER PRIMARY KEY,
    LayoutName TEXT NOT NULL,
    LayoutConfiguration TEXT
);

CREATE TABLE IF NOT EXISTS Themes (
    Id INTEGER PRIMARY KEY,
    ThemeName TEXT NOT NULL,
    ColourScheme TEXT,
    FontSettings TEXT,
    IconSettings TEXT
);

-- ===================== HELP SYSTEM =====================
CREATE TABLE IF NOT EXISTS HelpTopics (
    Id INTEGER PRIMARY KEY,
    TopicKey TEXT NOT NULL UNIQUE,
    Title TEXT NOT NULL,
    Category TEXT NOT NULL,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    Content TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS HelpSearchIndex (
    Id INTEGER PRIMARY KEY,
    TopicName TEXT NOT NULL,
    TopicCategory TEXT,
    SearchKeywords TEXT
);
CREATE INDEX IF NOT EXISTS IX_HelpSearchIndex_SearchKeywords ON HelpSearchIndex(SearchKeywords);

CREATE TABLE IF NOT EXISTS HelpBookmarks (
    Id INTEGER PRIMARY KEY,
    TopicId INTEGER NOT NULL,
    UserBookmark TEXT
);

-- ===================== DIAGNOSTICS =====================
CREATE TABLE IF NOT EXISTS ApplicationLog (
    Id INTEGER PRIMARY KEY,
    Timestamp TEXT NOT NULL,
    EventType TEXT,
    EventMessage TEXT
);
CREATE INDEX IF NOT EXISTS IX_ApplicationLog_Timestamp ON ApplicationLog(Timestamp);

CREATE TABLE IF NOT EXISTS RadioLog (
    Id INTEGER PRIMARY KEY,
    StationId INTEGER REFERENCES Stations(Id) ON DELETE SET NULL,
    ErrorType TEXT,
    ErrorMessage TEXT
);

CREATE TABLE IF NOT EXISTS EngineLog (
    Id INTEGER PRIMARY KEY,
    Timestamp TEXT NOT NULL,
    Component TEXT,
    Message TEXT
);
CREATE INDEX IF NOT EXISTS IX_EngineLog_Timestamp ON EngineLog(Timestamp);

CREATE TABLE IF NOT EXISTS SearchHistory (
    Id INTEGER PRIMARY KEY,
    SearchText TEXT NOT NULL,
    SearchType TEXT,
    SearchDate TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_SearchHistory_SearchDate ON SearchHistory(SearchDate);
";
}
