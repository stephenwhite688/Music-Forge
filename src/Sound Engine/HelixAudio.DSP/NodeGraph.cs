using System;
using System.Collections.Generic;
using System.Linq;

namespace HelixAudio.DSP
{
    public class NodeGraph
    {
        private readonly List<DSPNode> _nodes;
        private readonly List<NodeConnection> _connections;
        private int _sampleRate;
        private int _channels;
        private bool _isPrepared;

        public int NodeCount => _nodes.Count;
        public int ConnectionCount => _connections.Count;

        public NodeGraph()
        {
            _nodes = new List<DSPNode>();
            _connections = new List<NodeConnection>();
            _sampleRate = 44100;
            _channels = 2;
            _isPrepared = false;
        }

        private class NodeConnection
        {
            public DSPNode Source { get; set; }
            public DSPNode Target { get; set; }
            public int SourceOutputIndex { get; set; }
            public int TargetInputIndex { get; set; }

            public NodeConnection(DSPNode source, DSPNode target, int sourceOutput = 0, int targetInput = 0)
            {
                Source = source;
                Target = target;
                SourceOutputIndex = sourceOutput;
                TargetInputIndex = targetInput;
            }
        }

        public void AddNode(DSPNode node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            if (_nodes.Contains(node))
                throw new InvalidOperationException("Node already exists in the graph.");

            _nodes.Add(node);
        }

        public void RemoveNode(DSPNode node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            _connections.RemoveAll(c => c.Source == node || c.Target == node);
            _nodes.Remove(node);
        }

        public void Connect(DSPNode source, DSPNode target, int sourceOutput = 0, int targetInput = 0)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (!_nodes.Contains(source))
                throw new InvalidOperationException("Source node not in graph.");
            
            if (!_nodes.Contains(target))
                throw new InvalidOperationException("Target node not in graph.");

            if (HasPath(target, source))
                throw new InvalidOperationException("Circular dependency detected.");

            _connections.Add(new NodeConnection(source, target, sourceOutput, targetInput));
        }

        public void Disconnect(DSPNode source, DSPNode target)
        {
            if (source == null || target == null)
                return;

            _connections.RemoveAll(c => c.Source == source && c.Target == target);
        }

        public void Prepare(int sampleRate, int channels)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRate));
            
            if (channels <= 0)
                throw new ArgumentException("Channel count must be positive.", nameof(channels));

            _sampleRate = sampleRate;
            _channels = channels;
            _isPrepared = true;
        }

        public float[] Process(float[] input)
        {
            if (!_isPrepared)
                throw new InvalidOperationException("Graph must be prepared before processing.");

            if (_nodes.Count == 0 || _connections.Count == 0)
                return input;

            var sortedNodes = TopologicalSort();
            if (sortedNodes == null)
                throw new InvalidOperationException("Graph contains cycles.");

            var results = new Dictionary<DSPNode, float[]>();

            foreach (var node in sortedNodes)
            {
                var inputs = _connections.FindAll(c => c.Target == node);
                float[] nodeInput;

                if (inputs.Count == 0)
                {
                    nodeInput = input ?? new float[0];
                }
                else
                {
                    var mixed = new float[input?.Length ?? 0];
                    foreach (var conn in inputs)
                    {
                        if (results.TryGetValue(conn.Source, out var buffer))
                        {
                            for (int i = 0; i < Math.Min(mixed.Length, buffer.Length); i++)
                                mixed[i] += buffer[i];
                        }
                    }
                    nodeInput = mixed;
                }

                results[node] = node.Process(nodeInput);
            }

            return results.Count > 0 ? results.Values.Last() : input;
        }

        public void Reset()
        {
            foreach (var node in _nodes)
            {
                node.Reset();
            }
        }

        public void Clear()
        {
            _nodes.Clear();
            _connections.Clear();
        }

        private bool HasPath(DSPNode from, DSPNode to)
        {
            if (from == to)
                return true;

            foreach (var connection in _connections)
            {
                if (connection.Source == from)
                {
                    if (HasPath(connection.Target, to))
                        return true;
                }
            }

            return false;
        }

        private List<DSPNode> TopologicalSort()
        {
            var visited = new HashSet<DSPNode>();
            var result = new List<DSPNode>();
            var temp = new HashSet<DSPNode>();

            foreach (var node in _nodes)
            {
                if (!visited.Contains(node))
                {
                    if (!TopologicalSortVisit(node, visited, temp, result))
                        return null;
                }
            }

            return result;
        }

        private bool TopologicalSortVisit(DSPNode node, HashSet<DSPNode> visited, HashSet<DSPNode> temp, List<DSPNode> result)
        {
            if (temp.Contains(node))
                return false;

            if (!visited.Contains(node))
            {
                temp.Add(node);

                foreach (var connection in _connections)
                {
                    if (connection.Source == node)
                    {
                        if (!TopologicalSortVisit(connection.Target, visited, temp, result))
                            return false;
                    }
                }

                visited.Add(node);
                temp.Remove(node);
                result.Insert(0, node);
            }

            return true;
        }
    }
}
