using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.ObjectBuilders;
using PhantombiteStationRefill.Core;

namespace PhantombiteStationRefill.Modules
{
    /// <summary>
    /// StationRefill_Main
    ///
    /// Hält alle Stationen konfigurierter Fraktionen vollständig versorgt.
    /// Reaktoren: Uran auffüllen. Geschütze: Munition auffüllen.
    /// Intervall und Konfiguration kommen aus StationRefill_FileManager.
    /// </summary>
    public class StationRefill_Main : IModule
    {
        public string ModuleName => "StationRefill_Main";
        private const string SRC = "StationRefill_Main";

        private List<VRage.Game.ModAPI.IMyCubeGrid>            _stationGrids = new List<VRage.Game.ModAPI.IMyCubeGrid>();
        private List<IMyReactor>                                _reactors     = new List<IMyReactor>();
        private List<IMyUserControllableGun>                    _turrets      = new List<IMyUserControllableGun>();
        private List<VRage.Game.ModAPI.Ingame.MyInventoryItem>  _reuseItems   = new List<VRage.Game.ModAPI.Ingame.MyInventoryItem>();

        private int _tickCounter   = 0;
        private int _intervalTicks = 0;

        // PerfLevel 1: Queue für verteilten Item-Spawn
        private readonly System.Collections.Generic.Queue<System.Action> _refillQueue
            = new System.Collections.Generic.Queue<System.Action>();
        private const int BATCH_PER_TICK = 5; // Items pro Tick bei PerfLevel 1

        private StationRefill_FileManager _config;

        public void Init()     { }
        public void SaveData() { }

        public void Start(StationRefill_FileManager config)
        {
            _config        = config;
            _intervalTicks = config.IntervalHours * 216000;

            Log("Start — Fraktionen: " + string.Join(", ", config.FactionTags)
                + ", Intervall: " + config.IntervalHours + "h"
                + ", Ammo: " + config.AmmoSubtype);

            if (!MyAPIGateway.Multiplayer.IsServer) return;

            FindAllStations();
            if (_stationGrids.Count > 0) Refill();
        }

        public void Update()
        {
            if (_config == null || !MyAPIGateway.Multiplayer.IsServer) return;
            if (_stationGrids.Count == 0) return;

            _tickCounter++;
            if (_tickCounter < _intervalTicks) return;
            _tickCounter = 0;

            Log("Intervall erreicht (" + _config.IntervalHours + "h) — Auffüllung");
            Refill();
        }

        public void Close()
        {
            _stationGrids.Clear();
            _reactors.Clear();
            _turrets.Clear();
        }

        public void ForceRefill() { Log("ForceRefill via Command"); Refill(); }
        public void ForceRescan() { Log("ForceRescan via Command"); FindAllStations(); if (_stationGrids.Count > 0) Refill(); }

        // ── Stationen suchen ──────────────────────────────────────────────────

        private void FindAllStations()
        {
            try
            {
                _stationGrids.Clear();
                _reactors.Clear();
                _turrets.Clear();

                var factionMembers = new HashSet<long>();
                foreach (var tag in _config.FactionTags)
                {
                    var faction = MyAPIGateway.Session.Factions.TryGetFactionByTag(tag);
                    if (faction == null) { Log("Fraktion '" + tag + "' nicht gefunden"); continue; }
                    foreach (var member in faction.Members) factionMembers.Add(member.Key);
                    Log("Fraktion '" + tag + "' — " + faction.Members.Count + " Mitglieder");
                }

                if (factionMembers.Count == 0) { Log("Keine Fraktionsmitglieder — abgebrochen"); return; }

                var entities = new HashSet<VRage.ModAPI.IMyEntity>();
                MyAPIGateway.Entities.GetEntities(entities);

                foreach (var entity in entities)
                {
                    var grid = entity as VRage.Game.ModAPI.IMyCubeGrid;
                    if (grid == null || !grid.IsStatic) continue;
                    bool owned = false;
                    foreach (var id in grid.BigOwners)
                        if (factionMembers.Contains(id)) { owned = true; break; }
                    if (!owned) continue;
                    _stationGrids.Add(grid);
                    Log("Station gefunden: '" + grid.DisplayName + "'");
                }

                if (_stationGrids.Count > 0) CacheAllBlocks();

                Log("Suche abgeschlossen — " + _stationGrids.Count + " Station(en), "
                    + _reactors.Count + " Reaktoren, " + _turrets.Count + " Geschütze");
            }
            catch (Exception ex) { Error("FindAllStations: " + ex.Message); }
        }

        private void CacheAllBlocks()
        {
            _reactors.Clear();
            _turrets.Clear();
            var blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
            foreach (var grid in _stationGrids)
            {
                blocks.Clear();
                grid.GetBlocks(blocks);
                foreach (var slim in blocks)
                {
                    var reactor = slim.FatBlock as IMyReactor;
                    if (reactor != null) { _reactors.Add(reactor); continue; }
                    var turret = slim.FatBlock as IMyUserControllableGun;
                    if (turret != null) _turrets.Add(turret);
                }
            }
        }

        // ── Auffüllen ─────────────────────────────────────────────────────────

        private void Refill()
        {
            try
            {
                bool anyRemoved = false;
                for (int i = _stationGrids.Count - 1; i >= 0; i--)
                {
                    if (_stationGrids[i] == null || _stationGrids[i].MarkedForClose)
                    { _stationGrids.RemoveAt(i); anyRemoved = true; }
                }
                if (anyRemoved)
                {
                    if (_stationGrids.Count == 0) { FindAllStations(); return; }
                    CacheAllBlocks();
                }

                StationRefill_Logger.Instance?.Log(SRC,
                    "Refill startet — " + _reactors.Count + " Reaktoren, "
                    + _turrets.Count + " Geschütze, " + _stationGrids.Count + " Station(en)");

                var uraniumId = new MyDefinitionId(typeof(MyObjectBuilder_Ingot), "Uranium");
                var ammoId    = new MyDefinitionId(typeof(MyObjectBuilder_AmmoMagazine), _config.AmmoSubtype);

                int perfLevel = StationRefill_Session.PerfLevel;

                if (perfLevel >= 1)
                {
                    // PerfLevel 1: Queue aufbauen — BATCH_PER_TICK Actions pro Tick
                    _refillQueue.Clear();
                    foreach (var reactor in _reactors)
                    {
                        var r = reactor;
                        _refillQueue.Enqueue(() =>
                        {
                            if (r.MarkedForClose) return;
                            var inv = r.GetInventory(0);
                            if (inv == null) return;
                            int diff = GetMaxAmount(inv, uraniumId) - GetItemAmount(inv, uraniumId);
                            if (diff <= 0) return;
                            AddItems(inv, uraniumId, diff);
                            StationRefill_Logger.Instance?.Log(SRC, "Queue: Reaktor " + r.DisplayName + " +Uranium x" + diff, 1);
                        });
                    }
                    foreach (var turret in _turrets)
                    {
                        var t = turret;
                        _refillQueue.Enqueue(() =>
                        {
                            if (t.MarkedForClose) return;
                            var inv = t.GetInventory(0);
                            if (inv == null) return;
                            int diff = GetMaxAmount(inv, ammoId) - GetItemAmount(inv, ammoId);
                            if (diff <= 0) return;
                            AddItems(inv, ammoId, diff);
                            StationRefill_Logger.Instance?.Log(SRC, "Queue: Geschütz " + t.DisplayName + " +Ammo x" + diff, 1);
                        });
                    }
                    StationRefill_Logger.Instance?.Log(SRC,
                        "Queue aufgebaut: " + _refillQueue.Count + " Actions (" + BATCH_PER_TICK + "/Tick)");
                    return;
                }

                // PerfLevel 0: alles auf einmal
                int reactorsFilled = 0, turretsFilled = 0;

                foreach (var reactor in _reactors)
                {
                    if (reactor.MarkedForClose) continue;
                    var inv = reactor.GetInventory(0);
                    if (inv == null) continue;
                    int diff = GetMaxAmount(inv, uraniumId) - GetItemAmount(inv, uraniumId);
                    if (diff <= 0) continue;
                    AddItems(inv, uraniumId, diff);
                    reactorsFilled++;
                    StationRefill_Logger.Instance?.Log(SRC, "Reaktor " + reactor.DisplayName + " +Uranium x" + diff, 2);
                }

                foreach (var turret in _turrets)
                {
                    if (turret.MarkedForClose) continue;
                    var inv = turret.GetInventory(0);
                    if (inv == null) continue;
                    int diff = GetMaxAmount(inv, ammoId) - GetItemAmount(inv, ammoId);
                    if (diff <= 0) continue;
                    AddItems(inv, ammoId, diff);
                    turretsFilled++;
                    StationRefill_Logger.Instance?.Log(SRC, "Geschütz " + turret.DisplayName + " +Ammo x" + diff, 2);
                }

                StationRefill_Logger.Instance?.Log(SRC,
                    "Auffüllung — " + reactorsFilled + "/" + _reactors.Count + " Reaktoren, "
                    + turretsFilled + "/" + _turrets.Count + " Geschütze");
            }
            catch (Exception ex) { Error("Refill: " + ex.Message); }
        }

        // ── Inventory Helpers ─────────────────────────────────────────────────

        private void AddItems(VRage.Game.ModAPI.IMyInventory inv, MyDefinitionId id, int amount)
        {
            try
            {
                var builder = MyObjectBuilderSerializer.CreateNewObject(id);
                var physObj = builder as MyObjectBuilder_PhysicalObject;
                if (physObj != null) inv.AddItems((MyFixedPoint)amount, physObj);
            }
            catch (Exception ex) { Error("AddItems: " + ex.Message); }
        }

        private int GetItemAmount(VRage.Game.ModAPI.IMyInventory inv, MyDefinitionId id)
        {
            _reuseItems.Clear();
            inv.GetItems(_reuseItems);
            int total = 0;
            foreach (var item in _reuseItems)
                if (item.Type.TypeId == id.TypeId.ToString() && item.Type.SubtypeId == id.SubtypeName)
                    total += (int)item.Amount;
            return total;
        }

        private int GetMaxAmount(VRage.Game.ModAPI.IMyInventory inv, MyDefinitionId id)
        {
            try
            {
                float maxVolL = (float)inv.MaxVolume * 1000f;
                var   defId   = MyDefinitionId.Parse(id.TypeId + "/" + id.SubtypeName);
                var   itemDef = Sandbox.Definitions.MyDefinitionManager.Static.GetPhysicalItemDefinition(defId);
                if (itemDef == null || itemDef.Volume <= 0f) return 10000;
                return (int)(maxVolL / (itemDef.Volume * 1000f));
            }
            catch { return 10000; }
        }

        private void Log(string msg)   => StationRefill_Logger.Instance?.Info(SRC, msg);
        private void Error(string msg) => StationRefill_Logger.Instance?.Error(SRC, msg);
    }
}