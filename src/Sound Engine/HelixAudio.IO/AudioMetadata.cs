using System;

namespace HelixAudio.IO
{
    /// <summary>
    /// Represents audio metadata such as title, artist, album, and comments.
    /// </summary>
    public class AudioMetadata
    {
        private string _title;
        private string _artist;
        private string _album;
        private string _comment;

        /// <summary>
        /// Gets or sets the title of the audio.
        /// </summary>
        public string Title
        {
            get => _title ?? string.Empty;
            set => _title = value;
        }

        /// <summary>
        /// Gets or sets the artist of the audio.
        /// </summary>
        public string Artist
        {
            get => _artist ?? string.Empty;
            set => _artist = value;
        }

        /// <summary>
        /// Gets or sets the album of the audio.
        /// </summary>
        public string Album
        {
            get => _album ?? string.Empty;
            set => _album = value;
        }

        /// <summary>
        /// Gets or sets the comment for the audio.
        /// </summary>
        public string Comment
        {
            get => _comment ?? string.Empty;
            set => _comment = value;
        }

        /// <summary>
        /// Initializes a new instance of the AudioMetadata class.
        /// </summary>
        public AudioMetadata()
        {
            _title = string.Empty;
            _artist = string.Empty;
            _album = string.Empty;
            _comment = string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the AudioMetadata class with values.
        /// </summary>
        /// <param name="title">Title of the audio.</param>
        /// <param name="artist">Artist of the audio.</param>
        /// <param name="album">Album of the audio.</param>
        /// <param name="comment">Comment for the audio.</param>
        public AudioMetadata(string title, string artist = "", string album = "", string comment = "")
        {
            _title = title ?? string.Empty;
            _artist = artist ?? string.Empty;
            _album = album ?? string.Empty;
            _comment = comment ?? string.Empty;
        }

        /// <summary>
        /// Returns a string representation of the metadata.
        /// </summary>
        /// <returns>Formatted metadata string.</returns>
        public override string ToString()
        {
            return $"Title: {Title}, Artist: {Artist}, Album: {Album}, Comment: {Comment}";
        }

        /// <summary>
        /// Checks if the metadata has any non-empty fields.
        /// </summary>
        /// <returns>True if any field is populated.</returns>
        public bool IsEmpty()
        {
            return string.IsNullOrEmpty(_title) &&
                   string.IsNullOrEmpty(_artist) &&
                   string.IsNullOrEmpty(_album) &&
                   string.IsNullOrEmpty(_comment);
        }
    }
}
