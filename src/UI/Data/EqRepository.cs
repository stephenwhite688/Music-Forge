using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MusicForge.UI.Data;

/// <summary>A stored graphic-EQ preset (31 band gains + trims).</summary>
public sealed class EqPresetRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public float[] GainsDb { get; set; } = new float[31];
    public float InputGainDb { get; set; }
    public float OutputGainDb { get; set; }
}

/// <summary>CRUD for user graphic-EQ presets in the EqPresets table.</summary>
public sealed class EqRepository
{
    private sealed class StateJson
    {
        public float[] GainsDb { get; set; } = Array.Empty<float>();
        public float[][]? ChannelGainsDb { get; set; }
        public float InputGainDb { get; set; }
        public float OutputGainDb { get; set; }
        public bool Bypass { get; set; }
        public bool Enabled { get; set; } = true;
        public float BandQ { get; set; } = 4.317f;
        public bool ChannelLinked { get; set; } = true;
        public string Mode { get; set; } = "MinimumPhase";
    }
    private sealed class PresetJson
    {
        public float[] GainsDb { get; set; } = Array.Empty<float>();
        public float InputGainDb { get; set; }
        public float OutputGainDb { get; set; }
    }

    public List<EqPresetRecord> GetAll()
    {
        var list = new List<EqPresetRecord>();
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, PresetName, EqConfiguration FROM EqPresets ORDER BY PresetName;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var rec = new EqPresetRecord { Id = r.GetInt32(0), Name = r.GetString(1) };
            try
            {
                var json = JsonSerializer.Deserialize<PresetJson>(r.IsDBNull(2) ? "{}" : r.GetString(2));
                if (json is not null)
                {
                    rec.GainsDb = json.GainsDb.Length == 31 ? json.GainsDb : new float[31];
                    rec.InputGainDb = json.InputGainDb;
                    rec.OutputGainDb = json.OutputGainDb;
                }
            }
            catch { }
            list.Add(rec);
        }
        return list;
    }

    public int Save(string name, IReadOnlyList<float> gainsDb, float inputGainDb, float outputGainDb)
    {
        var json = JsonSerializer.Serialize(new PresetJson
        {
            GainsDb = gainsDb as float[] ?? new List<float>(gainsDb).ToArray(),
            InputGainDb = inputGainDb,
            OutputGainDb = outputGainDb
        });

        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        // Update if a preset with this name exists, else insert
        using (var check = connection.CreateCommand())
        {
            check.CommandText = "SELECT Id FROM EqPresets WHERE PresetName = $name LIMIT 1;";
            check.Parameters.AddWithValue("$name", name);
            if (check.ExecuteScalar() is long existing)
            {
                using var upd = connection.CreateCommand();
                upd.CommandText = "UPDATE EqPresets SET EqConfiguration = $json WHERE Id = $id;";
                upd.Parameters.AddWithValue("$json", json);
                upd.Parameters.AddWithValue("$id", (int)existing);
                upd.ExecuteNonQuery();
                return (int)existing;
            }
        }
        using var ins = connection.CreateCommand();
        ins.CommandText = "INSERT INTO EqPresets (PresetName, EqConfiguration) VALUES ($name, $json); SELECT last_insert_rowid();";
        ins.Parameters.AddWithValue("$name", name);
        ins.Parameters.AddWithValue("$json", json);
        return (int)(long)ins.ExecuteScalar()!;
    }

    public void Delete(int id)
    {
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM EqPresets WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // ---------- Live state ----------

    public void SaveState(HelixAudio.DSP.GraphicEQ.GraphicEqNode node, string mode = "MinimumPhase")
    {
        var state = new StateJson
        {
            GainsDb = node.GetBandGains(),
            ChannelGainsDb = node.ChannelLinked ? null : Enumerable.Range(0, node.PreparedChannels > 0 ? node.PreparedChannels : 2).Select(ch => node.GetBandGains(ch)).ToArray(),
            InputGainDb = node.InputGainDb,
            OutputGainDb = node.OutputGainDb,
            Bypass = node.Bypass,
            Enabled = node.Enabled,
            BandQ = node.BandQ,
            ChannelLinked = node.ChannelLinked,
            Mode = mode,
        };
        using var connection = MusicForgeDatabase.Instance.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS EqState (Id INTEGER PRIMARY KEY CHECK (Id = 1), StateJson TEXT NOT NULL); INSERT INTO EqState (Id, StateJson) VALUES (1, @json) ON CONFLICT(Id) DO UPDATE SET StateJson = excluded.StateJson;";
        cmd.Parameters.AddWithValue("@json", JsonSerializer.Serialize(state));
        cmd.ExecuteNonQuery();
    }

    public string? LoadState(HelixAudio.DSP.GraphicEQ.GraphicEqNode node)
    {
        string? json;
        using (var connection = MusicForgeDatabase.Instance.OpenConnection())
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT StateJson FROM EqState WHERE Id = 1;";
            json = cmd.ExecuteScalar() as string;
        }
        if (json == null) return null;
        var state = JsonSerializer.Deserialize<StateJson>(json);
        if (state == null) return null;
        node.SetAllGains(state.GainsDb);
        if (state.ChannelGainsDb != null)
            for (int ch = 0; ch < state.ChannelGainsDb.Length; ch++)
                for (int b = 0; b < state.ChannelGainsDb[ch].Length && b < node.BandCount; b++)
                    node.SetBandGain(ch, b, state.ChannelGainsDb[ch][b]);
        node.InputGainDb = state.InputGainDb;
        node.OutputGainDb = state.OutputGainDb;
        node.Bypass = state.Bypass;
        node.Enabled = state.Enabled;
        node.BandQ = state.BandQ;
        node.ChannelLinked = state.ChannelLinked;
        return state.Mode;
    }
}
