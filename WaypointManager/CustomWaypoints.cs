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
        public static string CustomWaypointsDirectory
        {
            get
            {
                return string.Join(Path.DirectorySeparatorChar.ToString(), new string[] { KSPUtil.ApplicationRootPath, "GameData", "WaypointManager", "PluginData" });

            }
        }
        public static string CustomWaypointsFileName
        {
            get
            {
                return string.Join(CustomWaypointsDirectory, "CustomWaypoints.cfg");
            }
        }

        public static CustomWaypoints Instance { get; private set; }

        private int nextIndex = 0;

        public CustomWaypoints()
        {
            Instance = this;
        }

        void Awake()
        {
            DontDestroyOnLoad(this);
            GameEvents.onCustomWaypointLoad.Add(OnCustomWaypointLoad);
            GameEvents.onCustomWaypointSave.Add(OnCustomWaypointSave);
        }

        /// <summary>
        /// Adds the waypoint to the custom waypoint list.
        /// </summary>
        /// <param name="waypoint">The waypoint to add</param>
        public static void AddWaypoint(Waypoint waypoint)
        {
            int seed = waypoint.seed;
            string id = waypoint.id;
            double altitude = waypoint.altitude;

            ScenarioCustomWaypoints.AddWaypoint(waypoint);

            waypoint.seed = seed;
            waypoint.id = id;
            waypoint.altitude = altitude;

            waypoint.index = Instance.nextIndex++;
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

            if (node.HasValue("icon"))
            {
                waypoint.id = node.GetValue("icon");
            }
            if (node.HasValue("altitude"))
            {
                waypoint.altitude = Convert.ToDouble(node.GetValue("altitude"));
            }
            if (node.HasValue("index"))
            {
                waypoint.index = Convert.ToInt32(node.GetValue("index"));
            }
            if (node.HasValue("seed"))
            {
                waypoint.seed = Convert.ToInt32(node.GetValue("seed"));
            }

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

        const string WAYPOINT_URL = "WAYPOINT";

        public static void Import()
        {
            ConfigNode master = new ConfigNode("CUSTOM_WAYPOINTS");
            int fileCount = 0, preload = 0;

            ConfigNode configNode = null; ;
            configNode = ConfigNode.Load(CustomWaypointsFileName);
            fileCount = configNode.CountNodes;

            if (configNode != null)
            {
                AddWaypointsFromConfig(master, configNode);
            }

            if (master.CountNodes == 0)
            {
                ScreenMessages.PostScreenMessage(string.Format("Couldn't load custom waypoint file {0}!", CustomWaypointsFileName),
                    6.0f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            ScenarioCustomWaypoints.Instance.OnLoad(master);

            int count = master.nodes.Count;
            if (fileCount > 0)
                ScreenMessages.PostScreenMessage("Imported " + fileCount + " waypoint" + (fileCount != 1 ? "s" : "") + " from " + CustomWaypointsFileName,
                    6.0f, ScreenMessageStyle.UPPER_CENTER);
            if (preload > 0)
                ScreenMessages.PostScreenMessage("Imported " + preload + " preload" + (preload != 1 ? "s" : "") + " from pre-loaded configs",
                    6.0f, ScreenMessageStyle.UPPER_CENTER);
        }

        internal static void AddWaypointsFromConfig(ConfigNode master, ConfigNode configNode)
        {
            // Add the non-dupes into a new list
            foreach (ConfigNode child in configNode.GetNodes("WAYPOINT"))
            {
                bool isDuplicate = false;
                string celestialName = child.GetValue("celestialName");
                double latitude = double.Parse(child.GetValue("latitude"));
                double longitude = double.Parse(child.GetValue("longitude"));
                double altitude = double.Parse(child.GetValue("altitude"));

                if (FinePrint.WaypointManager.Instance() != null)
                {
                    foreach (Waypoint wp in FinePrint.WaypointManager.Instance().Waypoints)
                    {
                        if (wp.celestialName == celestialName &&
                            Math.Abs(wp.latitude - latitude) < 0.00001 &&
                            Math.Abs(wp.longitude - longitude) < 0.00001 &&
                            Math.Abs(wp.altitude - altitude) < 0.1)
                        {
                            isDuplicate = true;
                            break;
                        }
                    }
                }

                if (!isDuplicate)
                {
                    master.AddNode(child);
                }
            }
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
