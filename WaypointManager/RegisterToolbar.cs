using UnityEngine;
using ToolbarControl_NS;

namespace WaypointManager
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RegisterToolbar : MonoBehaviour
    {
        void Start()
        {
            ToolbarControl.RegisterMod(WaypointManager.MODID, WaypointManager.MODNAME);
        }
    }
}
