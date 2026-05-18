using System;
using System.Collections.Generic;
using VRage.Utils;

namespace PhantombiteStationRefill.Core
{
    public class ModuleManager
    {
        private readonly List<IModule> _modules = new List<IModule>();

        public void Register(IModule module)
        {
            if (module == null) return;
            _modules.Add(module);
        }

        public void InitAll()
        {
            foreach (var m in _modules)
                try { m.Init(); }
                catch (Exception ex) { MyLog.Default.WriteLineAndConsole("[StationRefill] InitAll ERROR '" + m.ModuleName + "': " + ex.Message); }
        }

        public void CloseAll()
        {
            foreach (var m in _modules)
                try { m.Close(); }
                catch (Exception ex) { MyLog.Default.WriteLineAndConsole("[StationRefill] CloseAll ERROR '" + m.ModuleName + "': " + ex.Message); }
            _modules.Clear();
        }
    }
}
