using HelixAudio.Core;
using System;
using System.Threading.Tasks;

namespace HelixAudio.Streaming
{
    public interface IStream : IDisposable
    {
        AudioFormat Format { get; }
        Task StartAsync();
        Task StopAsync();
        Task<byte[]> ReadAsync(int count);
        Task PlayAsync();
    }
}
