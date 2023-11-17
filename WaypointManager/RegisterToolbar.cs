using UnityEngine;
using ToolbarControl_NS;
using KSP_Log;

namespace WaypointManager
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RegisterToolbar : MonoBehaviour
    {
        public static Log Log;

        void Start()
        {
            ToolbarControl.RegisterMod(WaypointManager.MODID, WaypointManager.MODNAME);
#if DEBUG
            Log = new Log("WaypointManager", Log.LEVEL.INFO);
#else
            Log = new Log("WaypointManager", Log.LEVEL.ERROR);
#endif
        }
    }
}
