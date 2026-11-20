using System;
using System.Collections.Generic;
using System.Linq;
using HelixAudio.Core;

namespace HelixAudio.Engine
{
    public class ProcessingGraph
    {
        private readonly Dictionary<Guid, EngineNode> _nodes;
        private List<EngineNode> _executionOrder;
        private bool _isDirty;

        public IReadOnlyCollection<EngineNode> Nodes => _nodes.Values.ToList().AsReadOnly();
        public int NodeCount => _nodes.Count;

        public ProcessingGraph()
        {
            _nodes = new Dictionary<Guid, EngineNode>();
            _executionOrder = new List<EngineNode>();
            _isDirty = false;
        }

        public EngineNode AddNode(EngineNode node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            if (_nodes.ContainsKey(node.Id))
                throw new InvalidOperationException($"Node {node.Id} already exists in the graph.");

            _nodes[node.Id] = node;
            _isDirty = true;
            return node;
        }

        public bool RemoveNode(Guid id)
        {
            if (!_nodes.TryGetValue(id, out var node))
                return false;

            node.DisconnectAll();
            _nodes.Remove(id);
            _isDirty = true;
            return true;
        }

        public EngineNode GetNode(Guid id)
        {
            _nodes.TryGetValue(id, out var node);
            return node;
        }

        public EngineNode FindNodeByName(string name)
        {
            return _nodes.Values.FirstOrDefault(n => n.Name == name);
        }

        public void Clear()
        {
            foreach (var node in _nodes.Values)
            {
                node.DisconnectAll();
            }
            _nodes.Clear();
            _executionOrder.Clear();
            _isDirty = true;
        }

        public void Connect(Guid fromId, Guid toId)
        {
            if (!_nodes.TryGetValue(fromId, out var from))
                throw new ArgumentException($"Node {fromId} not found.");

            if (!_nodes.TryGetValue(toId, out var to))
                throw new ArgumentException($"Node {toId} not found.");

            from.ConnectTo(to);
            _isDirty = true;
        }

        public void Disconnect(Guid fromId, Guid toId)
        {
            if (!_nodes.TryGetValue(fromId, out var from))
                return;

            if (!_nodes.TryGetValue(toId, out var to))
                return;

            from.DisconnectFrom(to);
            _isDirty = true;
        }

        public void RebuildExecutionOrder()
        {
            if (!_isDirty)
                return;

            var visited = new HashSet<EngineNode>();
            var temporary = new HashSet<EngineNode>();
            var result = new List<EngineNode>();

            foreach (var node in _nodes.Values)
            {
                if (!visited.Contains(node))
                {
                    if (!TopologicalSort(node, visited, temporary, result))
                    {
                        throw new InvalidOperationException("Cycle detected in processing graph.");
                    }
                }
            }

            // TopologicalSort appends a node after all of its outputs have been
            // visited. Reverse that post-order so sources run before consumers.
            result.Reverse();
            _executionOrder = result;
            _isDirty = false;
            AudioDiagnostics.Log("Graph", $"Execution order rebuilt; nodes={_executionOrder.Count}; order={string.Join(" -> ", _executionOrder.Select(node => node.Name))}");
        }

        private bool TopologicalSort(EngineNode node, HashSet<EngineNode> visited,
                                     HashSet<EngineNode> temporary, List<EngineNode> result)
        {
            if (temporary.Contains(node))
                return false;

            if (visited.Contains(node))
                return true;

            temporary.Add(node);

            foreach (var output in node.Outputs)
            {
                if (!TopologicalSort(output, visited, temporary, result))
                    return false;
            }

            temporary.Remove(node);
            visited.Add(node);
            result.Add(node);
            return true;
        }

        public float[] Process(float[] input)
        {
            if (_isDirty)
                RebuildExecutionOrder();

            if (_executionOrder.Count == 0)
                return input;

            var inputNodes = _executionOrder.Where(n => n.Inputs.Count == 0 && n.Enabled).ToList();
            var outputNodes = _executionOrder.Where(n => n.Outputs.Count == 0 && n.Enabled).ToList();

            var processedBuffers = new Dictionary<Guid, float[]>();

            foreach (var node in _executionOrder)
            {
                if (!node.Enabled)
                {
                    if (node.Inputs.Count > 0 && processedBuffers.ContainsKey(node.Inputs[0].Id))
                    {
                        processedBuffers[node.Id] = processedBuffers[node.Inputs[0].Id];
                    }
                    continue;
                }

                float[] nodeInput;

                if (node.Inputs.Count == 0)
                {
                    nodeInput = input;
                }
                else if (node.Inputs.Count == 1)
                {
                    var sourceId = node.Inputs[0].Id;
                    nodeInput = processedBuffers.TryGetValue(sourceId, out var buffer)
                                ? buffer : input;
                }
                else
                {
                    var mixedInput = new float[input.Length];
                    foreach (var source in node.Inputs)
                    {
                        if (processedBuffers.TryGetValue(source.Id, out var buffer))
                        {
                            for (int i = 0; i < Math.Min(mixedInput.Length, buffer.Length); i++)
                            {
                                mixedInput[i] += buffer[i];
                            }
                        }
                    }
                    nodeInput = mixedInput;
                }

                try
                {
                    var output = node.Process(nodeInput);
                    processedBuffers[node.Id] = output;
                    AudioDiagnostics.LogRateLimited(
                        $"node:{node.Id}",
                        "Graph",
                        $"Node processed; name={node.Name}; type={node.Type}; inputSamples={nodeInput.Length}; outputSamples={output.Length}",
                        TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing node {node.Name}: {ex.Message}");
                    AudioDiagnostics.LogError("Graph", $"Node processing failed; name={node.Name}; type={node.Type}", ex);
                    processedBuffers[node.Id] = nodeInput;
                }
            }

            if (outputNodes.Count > 0)
            {
                var lastNode = outputNodes[outputNodes.Count - 1];
                if (processedBuffers.TryGetValue(lastNode.Id, out var output))
                    return output;
            }

            return input;
        }

        public void Reset()
        {
            foreach (var node in _nodes.Values)
            {
                node.Reset();
            }
        }
    }
}
