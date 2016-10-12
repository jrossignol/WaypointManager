using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using FinePrint;

namespace WaypointManager
{
    /// <summary>
    /// Class for creating/maintaining/storing custom waypoints.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class CustomWaypoints : MonoBehaviour
    {
        public static string CustomWaypointsFileName
        {
            get
            {
                return string.Join(Path.DirectorySeparatorChar.ToString(), new string[] { KSPUtil.ApplicationRootPath, "GameData", "WaypointManager", "CustomWaypoints.cfg" });
            }
        }

        public static CustomWaypoints Instance { get; private set; }

        private int nextIndex = 0;

        public CustomWaypoints()
        {
            Instance = this;
        }

        void OnAwake()
        {
            DontDestroyOnLoad(this);
            GameEvents.onCustomWaypointLoad.Add(OnCustomWaypointLoad);
            GameEvents.onCustomWaypointSave.Add(OnCustomWaypointSave);
        }

        void OnDestroy()
        {
            /*foreach (Waypoint wp in waypoints)
            {
                //FinePrint.WaypointManager.RemoveWaypoint(wp);
            }*/
        }

        /// <summary>
        /// Adds the waypoint to the custom waypoint list.
        /// </summary>
        /// <param name="waypoint">The waypoint to add</param>
        public static void AddWaypoint(Waypoint waypoint)
        {
            waypoint.isOnSurface = true;
            waypoint.isNavigatable = true;
            waypoint.index = Instance.nextIndex++;

            ScenarioCustomWaypoints.AddWaypoint(waypoint);
        }

        /// <summary>
        /// Removes the given waypoint from the custom list.
        /// </summary>
        /// <param name="waypoint">The waypoint to remove</param>
        public static void RemoveWaypoint(Waypoint waypoint)
        {
            ScenarioCustomWaypoints.RemoveWaypoint(waypoint);
        }

        /// <summary>
        /// Checks if the given waypoint is one of our custom waypoints.
        /// </summary>
        /// <param name="waypoint">The waypoint to check.</param>
        /// <returns>True if the waypoint is a custom waypoint.</returns>
        public bool IsCustom(Waypoint waypoint)
        {
            if (waypoint == null)
            {
                return false;
            }

            return waypoint.isCustom;
        }

        public void OnCustomWaypointLoad(GameEvents.FromToAction<Waypoint, ConfigNode> fta)
        {
            Waypoint waypoint = fta.from;
            ConfigNode node = fta.to;

            waypoint.id = node.GetValue("icon");
            waypoint.altitude = Convert.ToDouble(node.GetValue("altitude"));
            waypoint.index = Convert.ToInt32(node.GetValue("index"));
            waypoint.seed = Convert.ToInt32(node.GetValue("seed"));

            nextIndex = Math.Max(nextIndex, waypoint.index + 1);
        }

        public void OnCustomWaypointSave(GameEvents.FromToAction<Waypoint, ConfigNode> fta)
        {
            Waypoint waypoint = fta.from;
            ConfigNode node = fta.to;

            node.AddValue("icon", waypoint.id);
            node.AddValue("altitude", waypoint.altitude);
            node.AddValue("index", waypoint.index);
            node.AddValue("seed", waypoint.seed);
        }

        private string HexValue(Color color)
        {
            Color32 c = color;
            return "#" + c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2");
        }

        public static void Import()
        {
            ConfigNode configNode = ConfigNode.Load(CustomWaypointsFileName);
            ScenarioCustomWaypoints.Instance.OnLoad(configNode);

            int count = configNode.nodes.Count;
            ScreenMessages.PostScreenMessage("Imported " + count + " waypoint" + (count != 1 ? "s" : "") + " from " + CustomWaypointsFileName,
                6.0f, ScreenMessageStyle.UPPER_CENTER);
        }

        public static void Export()
        {
            if (File.Exists(CustomWaypointsFileName))
            {
                CustomWaypointGUI.ShowExportDialog();
            }
            else
            {
                DoExport();
            }
        }

        public static void DoExport()
        {
            ConfigNode configNode = new ConfigNode("CUSTOM_WAYPOINTS");
            ScenarioCustomWaypoints.Instance.OnSave(configNode);

            configNode.Save(CustomWaypointsFileName,
                "Waypoint Manager Custom Waypoints File\r\n" +
                "//\r\n" +
                "// This file contains an extract of Waypoint Manager custom waypoints.");

            int count = configNode.nodes.Count;
            ScreenMessages.PostScreenMessage("Exported " + count + " waypoint" + (count != 1 ? "s" : "") + " to " + CustomWaypointsFileName,
                6.0f, ScreenMessageStyle.UPPER_CENTER);
        }
    }
}
