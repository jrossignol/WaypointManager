using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace WaypointManager
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class WaypointManagerGameEvents : MonoBehaviour
    {
        /// <summary>
        /// Use this when adding a waypoint icon to have waypoint manager read it.
        /// </summary>
        public static EventData<string> onWaypointIconAdded = new EventData<string>("OnWaypointIconAdded");

        void WaypointIconAdded(string name)
        {
            CustomWaypointGUI.customIcons.AddUnique(name);
        }

        void Awake()
        {
            onWaypointIconAdded.Add(new EventData<string>.OnEvent(WaypointIconAdded));
        }
    }
}
