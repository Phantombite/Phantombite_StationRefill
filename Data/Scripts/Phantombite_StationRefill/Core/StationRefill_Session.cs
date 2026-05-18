using System;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game;
using PhantombiteStationRefill.Core;
using PhantombiteStationRefill.Modules;

namespace PhantombiteStationRefill.Core
{
    /// <summary>
    /// StationRefill Session — Entry Point
    ///
    /// - LoadData:  Kanal registrieren
    /// - READY:     Module init, Config laden, erste Auffüllung, REGISTER senden
    /// - Update:    Periodischer Refill via Main
    /// - CMD:       refill, rescan
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class StationRefill_Session : MySessionComponentBase
    {
        private const string SRC          = "StationRefill_Session";
        private const string VERSION      = "1.0.0";
        private const long   CHANNEL      = 1995016L;
        private const long   CHANNEL_CORE = 1995000L;

        public static int PerfLevel { get; private set; } = 0;

        private ModuleManager              _modules;
        private StationRefill_FileManager  _fileManager;
        private StationRefill_Main         _main;

        // ── LoadData ──────────────────────────────────────────────────────────

        public override void LoadData()
        {
            try
            {
                MyAPIGateway.Utilities.RegisterMessageHandler(CHANNEL, OnMessageReceived);
            }
            catch (Exception ex)
            {
                VRage.Utils.MyLog.Default.WriteLineAndConsole(
                    "[ERROR] Phantombite_StationRefill/Session: LoadData Fehler: " + ex);
            }
        }

        // ── Update ────────────────────────────────────────────────────────────

        public override void UpdateBeforeSimulation()
        {
            try { _main?.Update(); }
            catch (Exception ex)
            {
                StationRefill_Logger.Instance?.Error(SRC, "Update Fehler: " + ex.Message);
            }
        }

        // ── Message Handler ───────────────────────────────────────────────────

        private void OnMessageReceived(object data)
        {
            try
            {
                string msg = data as string;
                if (string.IsNullOrEmpty(msg)) return;

                if (msg == "READY")              { OnReady();       return; }
                if (msg.StartsWith("CMD|\"))      { OnCommand(msg);  return; }
                if (msg.StartsWith("LOGLEVEL|")) { OnLogLevel(msg); return; }
                if (msg.StartsWith("PERFLEVEL|")) { OnPerfLevel(msg); return; }
            }
            catch (Exception ex)
            {
                StationRefill_Logger.Instance?.Error(SRC, "OnMessageReceived Fehler: " + ex.Message);
            }
        }

        private void OnLogLevel(string msg)
        {
            string[] parts = msg.Split('|');
            if (parts.Length < 2) return;
            StationRefill_Logger.Instance?.SetLogLevel(parts[1]);
            StationRefill_Logger.Instance?.Log(SRC, "LOGLEVEL gesetzt: " + parts[1], 1);
        }

        private void OnPerfLevel(string msg)
        {
            string[] parts = msg.Split('|');
            if (parts.Length < 2) return;
            int level;
            if (!int.TryParse(parts[1], out level)) return;
            PerfLevel = Math.Max(0, Math.Min(1, level));
            StationRefill_Logger.Instance?.Log(SRC, "PERFLEVEL gesetzt: " + PerfLevel);
            MyAPIGateway.Utilities.SendModMessage(CHANNEL_CORE, "PERFACK|stationrefill|" + PerfLevel);
        }

        // ── READY ─────────────────────────────────────────────────────────────

        private void OnReady()
        {
            try
            {
                InitModules();
                StationRefill_Logger.Instance?.Info(SRC, "READY empfangen — starte Initialisierung");
                _main.Start(_fileManager);
                StationRefill_Logger.Instance?.Info(SRC, "Initialisierung abgeschlossen");
            }
            catch (Exception ex)
            {
                StationRefill_Logger.Instance?.Error(SRC, "OnReady Fehler: " + ex);
            }
            finally
            {
                SendRegister();
            }
        }

        // ── CMD ───────────────────────────────────────────────────────────────

        private void OnCommand(string msg)
        {
            if (!MyAPIGateway.Multiplayer.IsServer) return;

            try
            {
                string[] parts = msg.Split('|');
                if (parts.Length < 3) return;

                string cmdName = parts[1].ToLower();

                ulong steamId = 0;
                string steamPart = parts[parts.Length - 1];
                if (steamPart.StartsWith("STEAM:"))
                    ulong.TryParse(steamPart.Substring(6), out steamId);

                StationRefill_Logger.Instance?.Debug(SRC, "Command: '" + cmdName + "'");

                switch (cmdName)
                {
                    case "refill":
                        if (_main == null) { SendCmdResult(cmdName, steamId, false, "StationRefill nicht initialisiert."); return; }
                        _main.ForceRefill();
                        SendCmdResult(cmdName, steamId, true, "Auffüllung gestartet.");
                        break;

                    case "rescan":
                        if (_main == null) { SendCmdResult(cmdName, steamId, false, "StationRefill nicht initialisiert."); return; }
                        _main.ForceRescan();
                        SendCmdResult(cmdName, steamId, true, "Stationen neu gesucht und aufgefüllt.");
                        break;

                    default:
                        SendCmdResult(cmdName, steamId, false, "Unbekannter Command: " + cmdName);
                        break;
                }
            }
            catch (Exception ex)
            {
                StationRefill_Logger.Instance?.Error(SRC, "OnCommand Fehler: " + ex.Message);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void InitModules()
        {
            _modules?.CloseAll();
            _modules = new ModuleManager();

            var logger = new StationRefill_Logger();
            _modules.Register(logger);

            _fileManager = new StationRefill_FileManager();
            _modules.Register(_fileManager);

            _main = new StationRefill_Main();
            _modules.Register(_main);

            _modules.InitAll();
        }

        private void SendRegister()
        {
            string msg = "REGISTER|stationrefill|Station Refill — Füllt Stationen automatisch auf|" + VERSION + "|" + CHANNEL
                + "|refill:1:Stationen sofort auffüllen"
                + "|rescan:1:Stationen neu suchen und auffüllen";
            MyAPIGateway.Utilities.SendModMessage(CHANNEL_CORE, msg);
            StationRefill_Logger.Instance?.Log(SRC, "REGISTER gesendet an Core");
        }

        private void SendCmdResult(string cmdName, ulong steamId, bool ok, string message)
        {
            string status = ok ? "ok" : "error";
            string result = "CMDRESULT|stationrefill|" + cmdName + "||" + steamId + "|" + status + "|" + message;
            MyAPIGateway.Utilities.SendModMessage(CHANNEL_CORE, result);
            StationRefill_Logger.Instance?.Info(SRC, "CMDRESULT [" + status + "]: " + message);
        }

        // ── UnloadData ────────────────────────────────────────────────────────

        protected override void UnloadData()
        {
            try
            {
                if (MyAPIGateway.Utilities != null)
                    MyAPIGateway.Utilities.UnregisterMessageHandler(CHANNEL, OnMessageReceived);
                _modules?.CloseAll();
            }
            catch (Exception ex)
            {
                StationRefill_Logger.Instance?.Error(SRC, "UnloadData Fehler: " + ex.Message);
            }
        }
    }
}