using System;
using HelixAudio.Filters;

namespace HelixAudio.Engine
{
    public class FilterNode : EngineNode
    {
        private readonly FilterBase _filter;

        public FilterBase Filter => _filter;

        public FilterNode(int sampleRate, int channels, FilterBase filter, string name = null)
            : base(sampleRate, channels, NodeType.Filter, name)
        {
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
        }

        public override float[] Process(float[] input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (input.Length == 0)
                throw new ArgumentException("Buffer cannot be empty.", nameof(input));
            if (input.Length % Channels != 0)
                throw new ArgumentException($"Buffer length must be a multiple of channel count ({Channels}).", nameof(input));

            return _filter.Process(input);
        }

        public override void Reset()
        {
            _filter.Reset();
        }
    }
}
