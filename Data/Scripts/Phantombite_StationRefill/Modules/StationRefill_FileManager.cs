using System;
using System.Text;
using Sandbox.ModAPI;
using PhantombiteStationRefill.Core;

namespace PhantombiteStationRefill.Modules
{
    /// <summary>
    /// StationRefill_FileManager
    ///
    /// Config im World-Storage: StationRefill_Config.ini
    ///
    /// [Settings]
    /// IntervalHours=5
    /// AmmoSubtype=RapidFireAutomaticRifleGun_Mag_50rd
    /// FactionTags=SPT
    /// </summary>
    public class StationRefill_FileManager : IModule
    {
        public string ModuleName => "StationRefill_FileManager";
        private const string SRC         = "StationRefill_FileManager";
        private const string CONFIG_FILE = "StationRefill_Config.ini";

        public int      IntervalHours { get; private set; } = 5;
        public string   AmmoSubtype   { get; private set; } = "RapidFireAutomaticRifleGun_Mag_50rd";
        public string[] FactionTags   { get; private set; } = { "SPT" };

        public void Init()
        {
            EnsureConfigExists();
            LoadConfig();
        }

        public void Update()   { }
        public void SaveData() { }
        public void Close()    { }

        private void EnsureConfigExists()
        {
            try
            {
                if (!FileExists(CONFIG_FILE))
                {
                    WriteFile(CONFIG_FILE, BuildDefaultConfig());
                    Log("Standard-Config erstellt: " + CONFIG_FILE);
                }
            }
            catch (Exception ex) { Error("EnsureConfigExists: " + ex.Message); }
        }

        private string BuildDefaultConfig()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# ==============================================================================");
            sb.AppendLine("# StationRefill Config — Phantombite_StationRefill");
            sb.AppendLine("# ==============================================================================");
            sb.AppendLine("# IntervalHours  = Stunden zwischen automatischen Auffüllungen");
            sb.AppendLine("# AmmoSubtype    = SubtypeId der Munition für Geschütze");
            sb.AppendLine("# FactionTags    = Kommagetrennte Fraktions-Tags (z.B. SPT,SPRT)");
            sb.AppendLine("# ==============================================================================");
            sb.AppendLine("[Settings]");
            sb.AppendLine("IntervalHours=5");
            sb.AppendLine("AmmoSubtype=RapidFireAutomaticRifleGun_Mag_50rd");
            sb.AppendLine("FactionTags=SPT");
            return sb.ToString();
        }

        private void LoadConfig()
        {
            try
            {
                string content = ReadFile(CONFIG_FILE);
                if (content == null) { Error("Config nicht lesbar"); return; }

                bool inSettings = false;
                foreach (var rawLine in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                    if (line == "[Settings]") { inSettings = true; continue; }
                    if (line.StartsWith("["))  { inSettings = false; continue; }
                    if (!inSettings) continue;

                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();

                    switch (key)
                    {
                        case "IntervalHours":
                            int h; if (int.TryParse(val, out h) && h > 0) IntervalHours = h; break;
                        case "AmmoSubtype":
                            if (!string.IsNullOrEmpty(val)) AmmoSubtype = val; break;
                        case "FactionTags":
                            var tags = val.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 0; i < tags.Length; i++) tags[i] = tags[i].Trim();
                            if (tags.Length > 0) FactionTags = tags; break;
                    }
                }

                Log("Config geladen — IntervalHours=" + IntervalHours
                    + ", Fraktionen=" + string.Join(",", FactionTags)
                    + ", Ammo=" + AmmoSubtype);
            }
            catch (Exception ex) { Error("LoadConfig: " + ex.Message); }
        }

        private string ReadFile(string filename)
        {
            try
            {
                if (!MyAPIGateway.Utilities.FileExistsInWorldStorage(filename, typeof(StationRefill_FileManager)))
                    return null;
                using (var r = MyAPIGateway.Utilities.ReadFileInWorldStorage(filename, typeof(StationRefill_FileManager)))
                    return r.ReadToEnd();
            }
            catch (Exception ex) { Error("ReadFile: " + ex.Message); return null; }
        }

        private void WriteFile(string filename, string content)
        {
            try
            {
                using (var w = MyAPIGateway.Utilities.WriteFileInWorldStorage(filename, typeof(StationRefill_FileManager)))
                    w.Write(content);
            }
            catch (Exception ex) { Error("WriteFile: " + ex.Message); }
        }

        private bool FileExists(string filename)
        {
            try { return MyAPIGateway.Utilities.FileExistsInWorldStorage(filename, typeof(StationRefill_FileManager)); }
            catch { return false; }
        }

        private void Log(string msg)   => StationRefill_Logger.Instance?.Info(SRC, msg);
        private void Error(string msg) => StationRefill_Logger.Instance?.Error(SRC, msg);
    }
}
