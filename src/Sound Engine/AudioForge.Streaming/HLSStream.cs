using AudioForge.IO;
using AudioForge.IO.Codecs;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography;

namespace AudioForge.Streaming;

/// <summary>
/// HTTP Live Streaming client. Fetches an m3u8 (master or media playlist),
/// decrypts AES-128 segments when required, decodes each segment through
/// Media Foundation, and exposes the result as a pull-based
/// <see cref="AudioStream"/> suitable for <c>RealtimePlayer</c>.
///
/// Live playlists (no EXT-X-ENDLIST) are re-fetched when the segment list
/// is exhausted. Network fetches happen on the audio pull thread with a
/// one-segment-ahead prefetch. MPEG-TS segments are attempted through
/// Media Foundation; support depends on the OS decoder.
/// </summary>
public sealed class HlsStream : IDisposable
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { { "User-Agent", "AudioForge/1.0" } }
    };

    private readonly ConcurrentQueue<(int Index, byte[] Data)> _prefetched = new();
    private readonly string _playlistUrl;
    private string _baseUrl;
    private HlsPlaylist _playlist = null!;
    private bool _liveEnded;
    private int _segmentIndex;
    private AudioStream? _currentSegment;
    private byte[]? _keyBytes;
    private bool _disposed;
    private readonly object _gate = new();
    private float[] _readScratch = Array.Empty<float>();

    private HlsStream(string url)
    {
        _playlistUrl = url;
        _baseUrl = GetDirectory(url);
    }

    /// <summary>Opens an HLS URL and returns a decoded, streaming AudioStream.</summary>
    public static async Task<AudioStream> OpenAsync(string url, CancellationToken ct = default)
    {
        var hs = new HlsStream(url);
        await hs.InitializeAsync(ct);
        // Eagerly open segment 0 to learn the stream format.
        hs.AdvanceSegment();
        if (hs._currentSegment is null)
            throw new InvalidDataException("HLS playlist contained no decodable segments.");
        var fmt = hs._currentSegment;

        return AudioStream.CreateStreaming(
            fmt.SampleRate, fmt.Channels, -1,
            hs.ReadFrames,
            seek: null,
            onDispose: hs.Dispose);
    }

    private async Task InitializeAsync(CancellationToken ct)
    {
        string text = await FetchTextAsync(_playlistUrl, ct);
        var pl = HlsPlaylist.Parse(text);
        if (pl.IsMaster)
        {
            if (pl.Variants.Count == 0) throw new InvalidDataException("Master playlist has no variants.");
            // Highest bandwidth variant.
            var v = pl.Variants.OrderByDescending(x => x.Bandwidth).First();
            string variantUrl = Resolve(_baseUrl, v.Uri);
            text = await FetchTextAsync(variantUrl, ct);
            _playlist = HlsPlaylist.Parse(text);
            _baseUrl = GetDirectory(variantUrl);
        }
        else
        {
            _playlist = pl;
        }
    }

    private void AdvanceSegment()
    {
        lock (_gate)
        {
            _currentSegment?.Dispose();
            _currentSegment = null;
            while (_segmentIndex < _playlist.Segments.Count)
            {
                var seg = _playlist.Segments[_segmentIndex++];
                byte[]? data = TakePrefetched(_segmentIndex - 1) ?? FetchSegment(seg);
                if (data is null || data.Length == 0) continue;
                try
                {
                    var decrypted = DecryptIfNeeded(data, seg.SequenceNumber);
                    _currentSegment = CodecManager.DecodeStream(new MemoryStream(decrypted));
                    if (_currentSegment is not null) break;
                }
                catch { /* skip undecodable segment */ }
            }
            // Prefetch next segment or refresh a live playlist.
            if (_segmentIndex < _playlist.Segments.Count)
            {
                int idx = _segmentIndex;
                var seg = _playlist.Segments[idx];
                _ = Task.Run(async () =>
                {
                    try
                    {
                        byte[]? d = await FetchSegmentAsync(seg, CancellationToken.None);
                        if (d is not null) _prefetched.Enqueue((idx, d));
                    }
                    catch { }
                });
            }
            else if (_playlist.IsLive && !_liveEnded)
            {
                _ = Task.Run(async () =>
                {
                    try { await RefreshLivePlaylistAsync(); } catch { }
                });
            }
        }
    }

    private int ReadFrames(float[] dest, int count)
    {
        if (_disposed) return 0;
        int total = 0;
        while (total < count)
        {
            if (_currentSegment is null)
            {
                if (_liveEnded) return total;
                AdvanceSegment();
                if (_currentSegment is null)
                {
                    if (!_playlist.IsLive) return total;
                    // Live: wait for the playlist refresh to produce segments.
                    for (int i = 0; i < 20 && _segmentIndex >= _playlist.Segments.Count && !_liveEnded; i++)
                        Thread.Sleep(250);
                    if (_segmentIndex >= _playlist.Segments.Count) return total;
                    continue;
                }
            }
            var s = _currentSegment;
            EnsureScratch(count * s.Channels);
            int got = s.ReadFramesInto(_readScratch, count);
            int copy = Math.Min(got * s.Channels, dest.Length - total * s.Channels);
            if (copy > 0) Array.Copy(_readScratch, 0, dest, total * s.Channels, copy);
            if (got <= 0) { AdvanceSegment(); continue; }
            total += got;
        }
        return total;
    }

    private void EnsureScratch(int floats)
    {
        if (_readScratch.Length < floats) _readScratch = new float[floats];
    }

    // ---- playlist/segment IO ----

    private async Task RefreshLivePlaylistAsync()
    {
        string text = await FetchTextAsync(_playlistUrl, CancellationToken.None);
        var pl = HlsPlaylist.Parse(text);
        if (pl.IsMaster) return;
        var known = new HashSet<string>(_playlist.Segments.Select(s => s.Uri));
        foreach (var s in pl.Segments)
            if (!known.Contains(s.Uri)) _playlist.Segments.Add(s);
        if (!pl.IsLive) _liveEnded = true;
    }

    private byte[]? TakePrefetched(int index)
    {
        while (_prefetched.TryDequeue(out var item))
            if (item.Index == index) return item.Data;
        return null;
    }

    private Task<byte[]?> FetchSegmentAsync(HlsSegment seg, CancellationToken ct)
        => FetchBytesAsync(Resolve(_baseUrl, seg.Uri), ct);

    private byte[]? FetchSegment(HlsSegment seg) => FetchSegmentAsync(seg, CancellationToken.None).GetAwaiter().GetResult();

    private async Task<byte[]?> FetchBytesAsync(string url, CancellationToken ct)
    {
        try
        {
            using var resp = await Http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }
        catch { return null; }
    }

    private async Task<string> FetchTextAsync(string url, CancellationToken ct)
    {
        using var resp = await Http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    private byte[] DecryptIfNeeded(byte[] data, long sequenceNumber)
    {
        var key = _playlist.Key;
        if (key is null || key.Method != "AES-128" || key.Uri is null) return data;
        if (_keyBytes is null)
            _keyBytes = FetchBytesAsync(Resolve(_baseUrl, key.Uri), CancellationToken.None)
                .GetAwaiter().GetResult() ?? throw new InvalidDataException("Could not fetch HLS key.");
        int len = data.Length - (data.Length % 16);
        var iv = ParseIv(key.Iv) ?? SequenceIv(sequenceNumber);
        using var aes = Aes.Create();
        aes.Key = _keyBytes; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.None;
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(data, 0, len);
    }

    private static byte[]? ParseIv(string? iv)
    {
        if (iv is null || !iv.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return null;
        var b = new byte[16];
        for (int i = 0; i < 16; i++)
            b[i] = Convert.ToByte(iv.Substring(2 + i * 2, 2), 16);
        return b;
    }

    private static byte[] SequenceIv(long seq)
    {
        var b = new byte[16];
        for (int i = 0; i < 8; i++) b[15 - i] = (byte)(seq >> (8 * i));
        return b;
    }

    private static string Resolve(string baseUrl, string uri)
    {
        if (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return uri;
        return baseUrl.TrimEnd('/') + "/" + uri.TrimStart('/');
    }

    private static string GetDirectory(string url)
    {
        int i = url.LastIndexOf('/');
        return i < 0 ? url : url[..(i + 1)];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_gate) { _currentSegment?.Dispose(); _currentSegment = null; }
    }
}
