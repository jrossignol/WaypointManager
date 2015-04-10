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
    [KSPScenario(ScenarioCreationOptions.AddToAllGames,
        GameScenes.FLIGHT, GameScenes.TRACKSTATION)]
    public class CustomWaypoints : ScenarioModule
    {
        public static string CustomWaypointsFileName
        {
            get
            {
                return string.Join(Path.DirectorySeparatorChar.ToString(), new string[] { KSPUtil.ApplicationRootPath, "GameData", "WaypointManager", "CustomWaypoints.cfg" });
            }
        }

        static public CustomWaypoints Instance { get; private set; }

        private List<Waypoint> waypoints = new List<Waypoint>();
        private int nextIndex = 0;
        private static bool customLoad = false;

        public CustomWaypoints()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            foreach (Waypoint wp in waypoints)
            {
                WaypointManager.RemoveWaypoint(wp);
            }
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

            Instance.waypoints.Add(waypoint);
            WaypointManager.AddWaypoint(waypoint);
        }

        /// <summary>
        /// Removes the given waypoint from the custom list.
        /// </summary>
        /// <param name="waypoint">The waypoint to remove</param>
        public static void RemoveWaypoint(Waypoint waypoint)
        {
            WaypointManager.RemoveWaypoint(waypoint);
            if (!Instance.waypoints.Remove(waypoint))
            {
                Debug.LogWarning("Couldn't remove custom waypoint '" + waypoint.name + "' - No such waypoint!");
            }
            else
            {
                if (waypoint.index == Instance.nextIndex - 1)
                {
                    Instance.nextIndex--;
                }
            }
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

            return waypoint.contractReference == null && waypoints.Contains(waypoint);
        }
        
        public override void OnLoad(ConfigNode node)
        {
            if (!customLoad)
            {
                base.OnLoad(node);
            }

            foreach (ConfigNode child in node.GetNodes("WAYPOINT"))
            {
                Waypoint waypoint = new Waypoint();
                waypoint.name = child.GetValue("name");
                waypoint.celestialName = child.GetValue("celestialName");
                waypoint.id = child.GetValue("icon");
                waypoint.latitude = Convert.ToDouble(child.GetValue("latitude"));
                waypoint.longitude = Convert.ToDouble(child.GetValue("longitude"));
                waypoint.altitude = Convert.ToDouble(child.GetValue("altitude"));
                waypoint.index = Convert.ToInt32(child.GetValue("index"));
                waypoint.seed = Convert.ToInt32(child.GetValue("seed"));
                waypoint.isOnSurface = true;
                waypoint.isNavigatable = true;

                // For a custom load, check for duplicates
                if (customLoad)
                {
                    foreach (Waypoint wp in waypoints.ToList())
                    {
                        if (wp.celestialName == waypoint.celestialName &&
                            Math.Abs(wp.latitude - waypoint.latitude) < 0.000001 &&
                            Math.Abs(wp.longitude - waypoint.longitude) < 0.000001 &&
                            Math.Abs(wp.altitude - waypoint.altitude) < 0.1)
                        {
                            waypoints.Remove(wp);
                            WaypointManager.RemoveWaypoint(wp);
                        }
                    }
                }

                waypoints.Add(waypoint);
                WaypointManager.AddWaypoint(waypoint);

                nextIndex = Math.Max(nextIndex, waypoint.index + 1);
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            foreach (Waypoint waypoint in waypoints)
            {
                ConfigNode child = new ConfigNode("WAYPOINT");
                node.AddNode(child);

                child.AddValue("name", waypoint.name);
                child.AddValue("celestialName", waypoint.celestialName);
                child.AddValue("icon", waypoint.id);
                child.AddValue("latitude", waypoint.latitude);
                child.AddValue("longitude", waypoint.longitude);
                child.AddValue("altitude", waypoint.altitude);
                child.AddValue("index", waypoint.index);
                child.AddValue("seed", waypoint.seed);
            }
        }

        private string HexValue(Color color)
        {
            Color32 c = color;
            return "#" + c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2");
        }

        public static void Import()
        {
            ConfigNode configNode = ConfigNode.Load(CustomWaypointsFileName);
            customLoad = true;
            Instance.Load(configNode);
            customLoad = false;

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
            Instance.Save(configNode);

            configNode.RemoveValue("name");
            configNode.RemoveValue("scene");

            configNode.Save(CustomWaypointsFileName,
                "Waypoint Manager Custom Waypoints File\r\n" +
                "//\r\n" +
                "// This file contains an extract of Waypoint Manager custom waypoints.");

            int count = Instance.waypoints.Count;
            ScreenMessages.PostScreenMessage("Exported " + count + " waypoint" + (count != 1 ? "s" : "") + " to " + CustomWaypointsFileName,
                6.0f, ScreenMessageStyle.UPPER_CENTER);
        }
    }
}
