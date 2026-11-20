using System.Collections.Generic;
using HelixAudio.Core;

namespace HelixAudio.Mixing
{
    public class MixerNode : AudioNode
    {
        private readonly MixerProcessor _processor;
        private readonly List<MixerChannel> _channels = new();

        public MixerNode(int channelCount, float panLawDb = -3.0f)
            : base(48000, 2, "MixerNode")
        {
            _processor = new MixerProcessor(panLawDb);

            for (int i = 0; i < channelCount; i++)
            {
                var ch = new MixerChannel(i);
                _channels.Add(ch);
                _processor.AddChannel(ch);
            }
        }

        public IReadOnlyList<MixerChannel> Channels => _channels;

        public MixerChannel GetChannel(int index) => _channels[index];

        // input: interleaved or per-channel buffers handled by caller;
        // here we assume per-channel float[] arrays passed in.
        public void Mix(float[][] inputs, float[] leftOut, float[] rightOut)
        {
            _processor.Process(inputs, leftOut, rightOut);
        }

        public override float[] Process(float[] buffer)
        {
            // For now, pass through. Engine-level processing can be wired later.
            return buffer;
        }
    }
}
