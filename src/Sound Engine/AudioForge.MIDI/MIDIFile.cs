using System.Text;

namespace AudioForge.MIDI;

/// <summary>
/// Standard MIDI File (SMF) parser. Supports format 0 and 1, all
/// channel voice events, and tempo-map resolution across tracks.
/// SMPTE division and format 2 are rejected with clear errors.
/// Events are merged and sorted by absolute time in seconds.
/// </summary>
public static class MidiFile
{
    private const byte MetaTempo = 0x51;

    public static MidiSequence Parse(string path) => Parse(File.ReadAllBytes(path));

    public static MidiSequence Parse(byte[] data)
    {
        if (data.Length < 14) throw new InvalidDataException("File too small for MIDI header.");
        RequireAscii(data, 0, "MThd");
        int headerLen = ReadBE32(data, 4);
        if (headerLen < 6) throw new InvalidDataException("Bad MIDI header length.");
        int format = ReadBE16(data, 8);
        int nTracks = ReadBE16(data, 10);
        int division = ReadBE16(data, 12);
        if (format == 2) throw new NotSupportedException("MIDI format 2 (sequential) is not supported.");
        if ((division & 0x8000) != 0)
            throw new NotSupportedException("SMPTE-divided MIDI files are not supported.");
        if (division <= 0) throw new InvalidDataException("Invalid ticks-per-quarter division.");

        int pos = 8 + headerLen; // MThd: 4 magic + 4 length + headerLen data

        // Raw events per track plus global tempo changes.
        var rawEvents = new List<RawEvent>();
        var tempoChanges = new List<(long tick, double secPerTick)>();
        double defaultSecPerTick = 0.5 / division; // 120 BPM

        for (int t = 0; t < nTracks && pos + 8 <= data.Length; t++)
        {
            RequireAscii(data, pos, "MTrk");
            int trackLen = (int)ReadBE32(data, pos + 4);
            int end = pos + 8 + trackLen;
            if (end > data.Length) throw new InvalidDataException("Truncated MTrk chunk.");
            pos += 8;

            long tick = 0;
            byte running = 0;
            while (pos < end)
            {
                tick += ReadVarLen(data, ref pos);
                byte status = data[pos];
                if (status < 0x80) { status = running; }
                else { pos++; if (status < 0xF0) running = status; }

                if (status == 0xFF) // meta event
                {
                    byte metaType = data[pos++];
                    int len = (int)ReadVarLen(data, ref pos);
                    if (metaType == MetaTempo && len == 3)
                    {
                        int us = (data[pos] << 16) | (data[pos + 1] << 8) | data[pos + 2];
                        tempoChanges.Add((tick, us / 1_000_000.0 / division));
                    }
                    pos += len;
                    continue;
                }
                if (status == 0xF0 || status == 0xF7) // sysex
                {
                    int len = (int)ReadVarLen(data, ref pos);
                    pos += len;
                    continue;
                }
                if (status < 0x80 || status >= 0xF0)
                    throw new InvalidDataException($"Bad MIDI status byte 0x{status:X2}.");

                int kind = status & 0xF0;
                int channel = status & 0x0F;
                int n1 = data[pos++];
                int n2 = 0;
                if (kind != 0xC0 && kind != 0xD0) n2 = data[pos++];
                rawEvents.Add(new RawEvent(tick, kind, channel, n1, n2));
            }
            pos = end;
        }

        // Build tempo map: sorted unique tempo changes with accumulated seconds.
        tempoChanges.Sort((a, b) => a.tick.CompareTo(b.tick));
        var mapTicks = new List<long>();
        var mapSecs = new List<double>();   // seconds at mapTicks[i]
        var mapSpt = new List<double>();    // sec/tick in segment starting at mapTicks[i]
        long lastTick = 0;
        double accSec = 0, spt = defaultSecPerTick;
        foreach (var (tt, nspt) in tempoChanges)
        {
            if (tt == lastTick && mapTicks.Count > 0) { spt = nspt; mapSpt[^1] = nspt; continue; }
            accSec += (tt - lastTick) * spt;
            lastTick = tt;
            spt = nspt;
            mapTicks.Add(tt);
            mapSecs.Add(accSec);
            mapSpt.Add(spt);
        }

        double TickToSeconds(long tk)
        {
            // Binary search for the segment containing tk.
            int lo = 0, hi = mapTicks.Count - 1, seg = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (mapTicks[mid] <= tk) { seg = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            if (seg < 0) return tk * defaultSecPerTick;
            return mapSecs[seg] + (tk - mapTicks[seg]) * mapSpt[seg];
        }

        var events = new List<MidiEvent>(rawEvents.Count);
        double maxTime = 0;
        foreach (var r in rawEvents)
        {
            var type = r.Kind switch
            {
                0x80 => MidiEventType.NoteOff,
                0x90 => MidiEventType.NoteOn,
                0xA0 => MidiEventType.PolyAftertouch,
                0xB0 => MidiEventType.ControlChange,
                0xC0 => MidiEventType.ProgramChange,
                0xD0 => MidiEventType.ChannelPressure,
                0xE0 => MidiEventType.PitchBend,
                _ => (MidiEventType)0,
            };
            // Note-on with velocity 0 is a note-off per MIDI spec.
            if (type == MidiEventType.NoteOn && r.D2 == 0) type = MidiEventType.NoteOff;
            double t = TickToSeconds(r.Tick);
            events.Add(new MidiEvent(t, r.Channel, type, r.D1, r.D2));
            if (t > maxTime) maxTime = t;
        }
        events.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
        return new MidiSequence(events, division, maxTime, nTracks);
    }

    /// <summary>
    /// Writes a <see cref="MidiSequence"/> as a format-0 Standard MIDI File
    /// at 120 BPM (events' TimeSeconds are converted to ticks).
    /// </summary>
    public static void Write(string path, MidiSequence sequence)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(fs);

        int tpq = sequence.TicksPerQuarter;
        double secPerTick = 0.5 / tpq; // 120 BPM

        // Build track events: delta ticks + bytes.
        var track = new MemoryStream();
        using (var tw = new BinaryWriter(track, System.Text.Encoding.ASCII, true))
        {
            // Tempo meta: 500000 µs per quarter note.
            WriteVarLen(tw, 0);
            tw.Write((byte)0xFF); tw.Write((byte)0x51); WriteVarLen(tw, 3);
            tw.Write((byte)0x07); tw.Write((byte)0xA1); tw.Write((byte)0x20); // 500000

            long lastTick = 0;
            var sorted = new List<MidiEvent>(sequence.Events);
            sorted.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
            foreach (var e in sorted)
            {
                long tick = (long)Math.Round(e.TimeSeconds / secPerTick);
                WriteVarLen(tw, (ulong)Math.Max(0, tick - lastTick));
                lastTick = tick;

                byte status = (byte)((byte)e.Type | (byte)(e.Channel & 0x0F));
                switch (e.Type)
                {
                    case MidiEventType.ProgramChange:
                    case MidiEventType.ChannelPressure:
                        tw.Write(status); tw.Write((byte)e.Data1);
                        break;
                    default:
                        tw.Write(status); tw.Write((byte)e.Data1); tw.Write((byte)e.Data2);
                        break;
                }
            }
            WriteVarLen(tw, 0);
            tw.Write((byte)0xFF); tw.Write((byte)0x2F); WriteVarLen(tw, 0); // EOT
        }

        byte[] trackBytes = track.ToArray();

        // Header: MThd, len 6, format 0, 1 track, tpq.
        w.Write(System.Text.Encoding.ASCII.GetBytes("MThd"));
        WriteBE32(w, 6);
        WriteBE16(w, 0);
        WriteBE16(w, 1);
        WriteBE16(w, (ushort)tpq);

        // Track: MTrk, len, data.
        w.Write(System.Text.Encoding.ASCII.GetBytes("MTrk"));
        WriteBE32(w, (uint)trackBytes.Length);
        w.Write(trackBytes);
    }

    private static void WriteVarLen(BinaryWriter w, ulong value)
    {
        var buf = new byte[8];
        int n = 0;
        buf[n++] = (byte)(value & 0x7F);
        value >>= 7;
        while (value > 0) { buf[n++] = (byte)((value & 0x7F) | 0x80); value >>= 7; }
        while (n > 0) w.Write(buf[--n]);
    }

    private static void WriteBE16(BinaryWriter w, ushort v) { w.Write((byte)(v >> 8)); w.Write((byte)v); }
    private static void WriteBE32(BinaryWriter w, uint v)
    {
        w.Write((byte)(v >> 24)); w.Write((byte)(v >> 16)); w.Write((byte)(v >> 8)); w.Write((byte)v);
    }

    private readonly record struct RawEvent(long Tick, int Kind, int Channel, int D1, int D2);

    private static void RequireAscii(byte[] d, int pos, string magic)
    {
        for (int i = 0; i < magic.Length; i++)
            if (d[pos + i] != (byte)magic[i])
                throw new InvalidDataException($"Expected '{magic}' at offset {pos}.");
    }

    private static int ReadBE16(byte[] d, int pos) => (d[pos] << 8) | d[pos + 1];

    private static int ReadBE32(byte[] d, int pos)
        => (d[pos] << 24) | (d[pos + 1] << 16) | (d[pos + 2] << 8) | d[pos + 3];

    private static long ReadVarLen(byte[] d, ref int pos)
    {
        long v = 0;
        while (true)
        {
            byte b = d[pos++];
            v = (v << 7) | (b & 0x7F);
            if ((b & 0x80) == 0) break;
        }
        return v;
    }
}
