using HelixAudio.Core;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HelixAudio.Streaming
{
    public sealed class HTTPStream : IStream
    {
        private readonly HttpClient _httpClient;
        private readonly Uri _uri;
        private Stream? _networkStream;
        private CancellationTokenSource? _cts;
        private bool _started;
        private bool _disposed;

        public AudioFormat Format { get; }

        public event EventHandler<Metadata>? MetadataUpdated;

        public HTTPStream(string url)
        {
            _httpClient = new HttpClient();
            _uri = new Uri(url);

            // Replace DefaultFloatStereo with explicit format
            Format = new AudioFormat(2, 48000, true, 32);
        }

        public async Task StartAsync()
        {
            if (_started)
                return;

            _cts = new CancellationTokenSource();

            var response = await _httpClient.GetAsync(
                _uri,
                HttpCompletionOption.ResponseHeadersRead,
                _cts.Token
            ).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            _networkStream = await response.Content.ReadAsStreamAsync(_cts.Token)
                                                   .ConfigureAwait(false);

            _started = true;
        }

        public Task StopAsync()
        {
            if (!_started)
                return Task.CompletedTask;

            _cts?.Cancel();
            _networkStream?.Dispose();
            _networkStream = null;
            _started = false;

            return Task.CompletedTask;
        }

        public async Task<byte[]> ReadAsync(int count)
        {
            if (!_started || _networkStream == null)
                return Array.Empty<byte>();

            byte[] buffer = new byte[count];
            int read = await _networkStream.ReadAsync(buffer, 0, count, _cts!.Token)
                                           .ConfigureAwait(false);

            if (read <= 0)
                return Array.Empty<byte>();

            if (read == count)
                return buffer;

            byte[] trimmed = new byte[read];
            Array.Copy(buffer, trimmed, read);
            return trimmed;
        }

        public async Task PlayAsync()
        {
            const int chunkSize = 4096;

            await StartAsync().ConfigureAwait(false);

            while (_started && _cts is not null && !_cts.IsCancellationRequested)
            {
                var data = await ReadAsync(chunkSize).ConfigureAwait(false);
                if (data.Length == 0)
                    break;

                // No playback sink here — caller handles the audio data.
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _cts?.Cancel();
            _networkStream?.Dispose();
            _httpClient.Dispose();
            _cts?.Dispose();
        }
    }
}
