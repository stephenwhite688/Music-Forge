using System;
using System.IO;
using System.Text;

namespace AudioForge.IO
{
    /// <summary>
    /// Detects audio file formats by examining file headers.
    /// </summary>
    public static class FormatDetector
    {
        /// <summary>
        /// Detects the format of an audio file.
        /// </summary>
        /// <param name="path">Path to the audio file.</param>
        /// <returns>Format string: "wav", "pcm", or "unknown".</returns>
        public static string Detect(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));

            if (!File.Exists(path))
                throw new FileNotFoundException("File not found.", path);

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Detect(stream);
        }

        /// <summary>
        /// Detects the format of an audio stream.
        /// </summary>
        /// <param name="stream">Stream containing audio data.</param>
        /// <returns>Format string: "wav", "pcm", or "unknown".</returns>
        public static string Detect(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanRead)
                throw new ArgumentException("Stream must be readable.", nameof(stream));

            long originalPosition = stream.Position;

            try
            {
                if (stream.Length < 12)
                    return "unknown";

                stream.Seek(0, SeekOrigin.Begin);

                byte[] header = new byte[12];
                int bytesRead = stream.Read(header, 0, 12);

                if (bytesRead < 12)
                    return "unknown";

                string riffHeader = Encoding.ASCII.GetString(header, 0, 4);
                if (riffHeader == "RIFF")
                {
                    string waveHeader = Encoding.ASCII.GetString(header, 8, 4);
                    if (waveHeader == "WAVE")
                        return "wav";
                }

                return "unknown";
            }
            finally
            {
                stream.Seek(originalPosition, SeekOrigin.Begin);
            }
        }

        /// <summary>
        /// Detects the format of an audio file using the file extension.
        /// </summary>
        /// <param name="path">Path to the audio file.</param>
        /// <returns>Format string: "wav", "pcm", or "unknown".</returns>
        public static string DetectByExtension(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "unknown";

            string extension = Path.GetExtension(path).ToLowerInvariant();

            return extension switch
            {
                ".wav" => "wav",
                ".pcm" or ".raw" => "pcm",
                _ => "unknown"
            };
        }

        /// <summary>
        /// Attempts to detect both by header and extension.
        /// </summary>
        /// <param name="path">Path to the audio file.</param>
        /// <returns>Format string: "wav", "pcm", or "unknown".</returns>
        public static string DetectCombined(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "unknown";

            if (File.Exists(path))
            {
                string headerFormat = Detect(path);
                if (headerFormat != "unknown")
                    return headerFormat;
            }

            return DetectByExtension(path);
        }
    }
}
