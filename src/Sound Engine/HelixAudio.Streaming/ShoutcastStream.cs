using HelixAudio.Core;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HelixAudio.Streaming
{
    public sealed class ShoutcastStream : IStream
    {
        private readonly HttpClient _httpClient;
        private readonly Uri _uri;
        private CancellationTokenSource? _cts;
        private Stream? _networkStream;
        private bool _isStarted;

        public AudioFormat Format { get; }

        public ShoutcastStream(string url, AudioFormat format)
        {
            _httpClient = new HttpClient();
            _uri = new Uri(url);
            Format = format;
        }

        public async Task StartAsync()
        {
            if (_isStarted)
                return;

            _cts = new CancellationTokenSource();

            var response = await _httpClient.GetAsync(_uri, HttpCompletionOption.ResponseHeadersRead, _cts.Token)
                                            .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
            _networkStream = await response.Content.ReadAsStreamAsync(_cts.Token).ConfigureAwait(false);

            _isStarted = true;
        }

        public Task StopAsync()
        {
            if (!_isStarted)
                return Task.CompletedTask;

            _cts?.Cancel();
            _networkStream?.Dispose();
            _networkStream = null;
            _isStarted = false;

            return Task.CompletedTask;
        }

        public async Task<byte[]> ReadAsync(int count)
        {
            if (!_isStarted || _networkStream == null)
                return Array.Empty<byte>();

            byte[] buffer = new byte[count];
            int read = await _networkStream.ReadAsync(buffer, 0, count, _cts!.Token).ConfigureAwait(false);

            if (read == 0)
                return Array.Empty<byte>();

            if (read == count)
                return buffer;

            byte[] trimmed = new byte[read];
            Array.Copy(buffer, trimmed, read);
            return trimmed;
        }

        public async Task PlayAsync()
        {
            // Simple pull-based playback loop: user is expected to feed the data into an AudioEngine.
            // This method just continuously reads from the stream until stopped.
            const int chunkSize = 4096;

            await StartAsync().ConfigureAwait(false);

            while (_isStarted && _cts is not null && !_cts.IsCancellationRequested)
            {
                var data = await ReadAsync(chunkSize).ConfigureAwait(false);
                if (data.Length == 0)
                    break;

                // At this level we don't know the output sink; the caller is expected to
                // consume data via ReadAsync or wrap this in a higher-level streamer.
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _networkStream?.Dispose();
            _httpClient.Dispose();
            _cts?.Dispose();
        }
    }
}
