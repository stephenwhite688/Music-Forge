using AudioForge.Core;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace AudioForge.MIDI;

/// <summary>
/// Polyphonic MIDI synthesizer node. Loads a parsed <see cref="MidiSequence"/>,
/// schedules events sample-accurately against the audio clock, and renders
/// voices through a fixed preallocated pool (zero allocation on the
/// realtime path). Voice model: dual oscillator (saw + sub-sine) with
/// ADSR envelope, per-channel volume/pan/pitch-bend from MIDI CC.
/// </summary>
public sealed class MidiSynthNode : AudioNode
{
    private const int MaxVoices = 32;
    private const int MaxChannels = 16;

    private sealed class Voice
    {
        public bool Active;
        public int Channel, Note;
        public double Phase, SubPhase;
        public float Velocity;
        public int Stage;           // 0=idle 1=attack 2=decay 3=sustain 4=release
        public float Level, StageStart;
        public double Age;
    }

    private sealed class ChannelState
    {
        public float Volume = 1f;      // CC7
        public float Pan = 0f;         // CC10, -1..1
        public float PitchBend;        // semitones, +-2
        public int Program;
    }

    private readonly Voice[] _voices = new Voice[MaxVoices];
    private readonly ChannelState[] _channels = new ChannelState[MaxChannels];
    private MidiEvent[] _events = Array.Empty<MidiEvent>();
    private int _nextEvent;
    private long _samplePos;
    private double _freqOfNote(int note) => 440.0 * Math.Pow(2.0, (note - 69) / 12.0);

    // Synth parameters (seconds / linear).
    private float _attackMs = 5f, _decayMs = 60f, _sustain = 0.7f, _releaseMs = 90f, _masterGain = 0.8f;

    public MidiSynthNode(int sampleRate, int channels = 2, string name = "MIDI Synth")
        : base(sampleRate, channels, name)
    {
        for (int i = 0; i < MaxVoices; i++) _voices[i] = new Voice();
        for (int i = 0; i < MaxChannels; i++) _channels[i] = new ChannelState();
    }

    /// <summary>
    /// Loads events from a parsed sequence. Event times are converted to
    /// sample positions at this node's sample rate (stored back into
    /// <see cref="MidiEvent.TimeSeconds"/> as a sample index).
    /// </summary>
    public void LoadSequence(MidiSequence seq)
    {
        _events = new MidiEvent[seq.Events.Count];
        for (int i = 0; i < seq.Events.Count; i++)
        {
            var e = seq.Events[i];
            _events[i] = e with { TimeSeconds = e.TimeSeconds * SampleRate };
        }
        Reset();
    }

    public void LoadFile(string path) => LoadSequence(MidiFile.Parse(path));
    public int EventCount => _events.Length;
    public bool IsFinished => _nextEvent >= _events.Length && AllVoicesIdle();

    public override void Reset()
    {
        _nextEvent = 0;
        _samplePos = 0;
        foreach (var v in _voices) { v.Active = false; v.Stage = 0; }
        for (int i = 0; i < MaxChannels; i++) _channels[i] = new ChannelState();
    }

    // ---- parameters ----

    public override IReadOnlyList<AudioParameterInfo> GetParameterInfo() => new[]
    {
        new AudioParameterInfo("master.gain", 0f, 1.5f, _masterGain, ""),
        new AudioParameterInfo("env.attack.ms", 0.1f, 500f, _attackMs, "ms"),
        new AudioParameterInfo("env.decay.ms", 1f, 2000f, _decayMs, "ms"),
        new AudioParameterInfo("env.sustain", 0f, 1f, _sustain, ""),
        new AudioParameterInfo("env.release.ms", 1f, 5000f, _releaseMs, "ms"),
    };

    public override bool TryGetParameter(string name, out float value)
    {
        value = name switch
        {
            "master.gain" => _masterGain,
            "env.attack.ms" => _attackMs,
            "env.decay.ms" => _decayMs,
            "env.sustain" => _sustain,
            "env.release.ms" => _releaseMs,
            _ => float.NaN,
        };
        return !float.IsNaN(value);
    }

    public override bool TrySetParameter(string name, float value)
    {
        switch (name)
        {
            case "master.gain": _masterGain = Math.Clamp(value, 0f, 1.5f); return true;
            case "env.attack.ms": _attackMs = Math.Clamp(value, 0.1f, 500f); return true;
            case "env.decay.ms": _decayMs = Math.Clamp(value, 1f, 2000f); return true;
            case "env.sustain": _sustain = Math.Clamp(value, 0f, 1f); return true;
            case "env.release.ms": _releaseMs = Math.Clamp(value, 1f, 5000f); return true;
            default: return false;
        }
    }

    public override JsonObject GetState() => new JsonObject
    {
        ["master.gain"] = _masterGain,
        ["env.attack.ms"] = _attackMs,
        ["env.decay.ms"] = _decayMs,
        ["env.sustain"] = _sustain,
        ["env.release.ms"] = _releaseMs,
        ["events"] = _events.Length,
    };

    public override void SetState(JsonObject state)
    {
        if (state["master.gain"] is { } g) TrySetParameter("master.gain", g.GetValue<float>());
        if (state["env.attack.ms"] is { } a) TrySetParameter("env.attack.ms", a.GetValue<float>());
        if (state["env.decay.ms"] is { } d) TrySetParameter("env.decay.ms", d.GetValue<float>());
        if (state["env.sustain"] is { } s) TrySetParameter("env.sustain", s.GetValue<float>());
        if (state["env.release.ms"] is { } r) TrySetParameter("env.release.ms", r.GetValue<float>());
    }

    // ---- realtime process ----

    public override void Process(Span<float> buffer)
    {
        int frames = buffer.Length / Format.Channels;
        int ch = Format.Channels;
        for (int f = 0; f < frames; f++)
        {
            DispatchDueEvents();
            float l = 0, r = 0;
            RenderFrame(ref l, ref r);
            int idx = f * ch;
            if (ch >= 2) { buffer[idx] = l; buffer[idx + 1] = r; }
            else buffer[idx] = (l + r) * 0.5f;
            _samplePos++;
        }
    }

    private void DispatchDueEvents()
    {
        // Events store time in samples (seconds * SampleRate at load).
        while (_nextEvent < _events.Length && _events[_nextEvent].TimeSeconds <= _samplePos)
        {
            var e = _events[_nextEvent++];
            int c = Math.Clamp(e.Channel, 0, MaxChannels - 1);
            switch (e.Type)
            {
                case MidiEventType.NoteOn: NoteOn(c, e.Data1, e.Data2); break;
                case MidiEventType.NoteOff: NoteOff(c, e.Data1); break;
                case MidiEventType.ControlChange:
                    if (e.Data1 == 7) _channels[c].Volume = e.Data2 / 127f;
                    else if (e.Data1 == 10) _channels[c].Pan = (e.Data2 / 127f) * 2f - 1f;
                    break;
                case MidiEventType.PitchBend:
                    _channels[c].PitchBend = (e.Data2 / 16383f) * 4f - 2f; // 14-bit combined
                    break;
            }
        }
    }

    private void NoteOn(int channel, int note, int velocity)
    {
        if (velocity <= 0) { NoteOff(channel, note); return; }
        Voice? v = null;
        foreach (var cand in _voices)
            if (!cand.Active) { v = cand; break; }
        if (v is null) // steal oldest
        {
            v = _voices[0];
            foreach (var cand in _voices) if (cand.Age > v.Age) v = cand;
        }
        v.Active = true;
        v.Channel = channel; v.Note = note;
        v.Velocity = velocity / 127f;
        v.Stage = 1; v.Level = 0; v.StageStart = 0; v.Age = 0;
        v.Phase = 0; v.SubPhase = 0;
    }

    private void NoteOff(int channel, int note)
    {
        foreach (var v in _voices)
            if (v.Active && v.Channel == channel && v.Note == note && v.Stage != 4)
            { v.Stage = 4; v.StageStart = v.Level; }
    }

    private void RenderFrame(ref float l, ref float r)
    {
        float sr = SampleRate;
        float attLen = Math.Max(1, _attackMs * sr / 1000f);
        float decLen = Math.Max(1, _decayMs * sr / 1000f);
        float relLen = Math.Max(1, _releaseMs * sr / 1000f);

        for (int i = 0; i < MaxVoices; i++)
        {
            var v = _voices[i];
            if (!v.Active) continue;
            v.Age++;

            // Envelope.
            float env;
            switch (v.Stage)
            {
                case 1: // attack
                    env = (float)(v.Age / attLen);
                    if (env >= 1f) { v.Stage = 2; v.Age = 0; env = 1f; }
                    break;
                case 2: // decay -> sustain
                    env = 1f + (_sustain - 1f) * (float)Math.Min(1.0, v.Age / decLen);
                    if (v.Age >= decLen) { v.Stage = 3; env = _sustain; }
                    break;
                case 3: env = _sustain; break;
                case 4: // release
                    env = v.StageStart * (1f - (float)Math.Min(1.0, v.Age / relLen));
                    if (env <= 0.001f) { v.Active = false; continue; }
                    break;
                default: env = 0; break;
            }
            v.Level = env;

            var ch = _channels[v.Channel];
            // Live pitch bend applies to all voices on the channel.
            double freq = _freqOfNote(v.Note) * Math.Pow(2.0, ch.PitchBend / 12.0);
            double inc = freq / sr;
            double subInc = freq * 0.5 / sr;

            // Dual osc: saw + sub sine, cheap polyblep-free (aliasing acceptable for synth sketch).
            v.Phase += inc; if (v.Phase >= 1) v.Phase -= 1;
            v.SubPhase += subInc; if (v.SubPhase >= 1) v.SubPhase -= 1;
            float saw = (float)(2.0 * v.Phase - 1.0);
            float sub = (float)Math.Sin(v.SubPhase * 2.0 * Math.PI);
            float s = (saw * 0.6f + sub * 0.4f) * env * v.Velocity * ch.Volume * _masterGain;

            // Equal-power pan.
            float panA = (ch.Pan + 1f) * (float)(Math.PI / 4.0);
            l += s * (float)Math.Cos(panA);
            r += s * (float)Math.Sin(panA);
        }
    }

    private bool AllVoicesIdle()
    {
        foreach (var v in _voices) if (v.Active) return false;
        return true;
    }
}
