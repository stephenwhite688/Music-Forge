using System;
using System.Collections.Generic;

namespace HelixAudio.Engine
{
    public class EngineController
    {
        private readonly AudioEngine _engine;
        private readonly Dictionary<string, Guid> _nodeMap;

        public AudioEngine Engine => _engine;
        public AudioEngine.EngineState State => _engine.State;

        public EngineController(AudioEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _nodeMap = new Dictionary<string, Guid>();
        }

        public void LoadPipeline_DefaultPlayback()
        {
            ClearPipeline();
        }

        public void LoadPipeline_GraphicEQ()
        {
            ClearPipeline();
        }

        public void LoadPipeline_SpatialDemo()
        {
            ClearPipeline();
        }

        public void ClearPipeline()
        {
            _engine.Graph.Clear();
            _nodeMap.Clear();
        }

        public void Start()
        {
            _engine.Start();
        }

        public void Stop()
        {
            _engine.Stop();
        }

        public void Pause()
        {
            _engine.Pause();
        }

        public void Resume()
        {
            _engine.Resume();
        }

        public EngineNode FindNodeByName(string name)
        {
            return _engine.Graph.FindNodeByName(name);
        }

        public void RegisterNode(string name, Guid nodeId)
        {
            _nodeMap[name] = nodeId;
        }

        public EngineNode GetRegisteredNode(string name)
        {
            if (_nodeMap.TryGetValue(name, out var id))
                return _engine.Graph.GetNode(id);
            return null;
        }
    }
}
