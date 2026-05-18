using System;
using Sandbox.ModAPI;
using VRage.Utils;

namespace PhantombiteStationRefill.Modules
{
    using Core;

    public class StationRefill_Logger : IModule
    {
        public string ModuleName => "StationRefill_Logger";
        public static StationRefill_Logger Instance { get; private set; }

        private int _level = 0;
        private const string MOD_NAME   = "Phantombite_StationRefill";
        private const string PREFIX     = "[PB.StationRefill]";
        private const long   LOG_CHANNEL = 1995999L;

        public void Init()     { Instance = this; }
        public void Update()   { }
        public void SaveData() { }
        public void Close()    { Instance = null; }

        public int Level => _level;

        public void SetLogLevel(string levelStr)
        {
            int num;
            if (int.TryParse(levelStr.Trim(), out num))
                _level = Math.Max(0, Math.Min(3, num));
            else
            {
                string s = levelStr.ToLower().Trim();
                _level = s == "trace" ? 3 : s == "debug" ? 1 : 0;
            }
        }

        public void Log(string src, string msg, int level = 0)
        {
            if (level > _level) return;
            MyLog.Default.WriteLineAndConsole(PREFIX + " [" + level + "] [" + src + "] " + msg);
            Send(level.ToString(), src, msg);
        }

        public void Warn(string src, string msg)
        {
            MyLog.Default.WriteLineAndConsole(PREFIX + " [WARN] [" + src + "] " + msg);
            Send("WARN", src, msg);
        }

        public void Error(string src, string msg)
        {
            MyLog.Default.WriteLineAndConsole(PREFIX + " [ERROR] [" + src + "] " + msg);
            Send("ERROR", src, msg);
        }

        private void Send(string level, string src, string msg)
        {
            try { MyAPIGateway.Utilities.SendModMessage(LOG_CHANNEL, "LOG|" + MOD_NAME + "|" + level + "|" + src + "|" + msg); }
            catch { }
        }

        // Compat-Wrapper
        public void Info(string src, string msg)  => Log(src, msg, 0);
        public void Debug(string src, string msg) => Log(src, msg, 1);
    }
}