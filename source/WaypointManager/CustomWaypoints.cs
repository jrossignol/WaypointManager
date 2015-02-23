using System;
using System.Collections.Generic;
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
        static public CustomWaypoints Instance { get; private set; }

        private List<Waypoint> waypoints = new List<Waypoint>();
        private int nextIndex = 0;

        public CustomWaypoints()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            foreach (Waypoint wp in waypoints)
            {
                FinePrint.WaypointManager.RemoveWaypoint(wp);
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
            FinePrint.WaypointManager.AddWaypoint(waypoint);
        }

        /// <summary>
        /// Removes the given waypoint from the custom list.
        /// </summary>
        /// <param name="waypoint">The waypoint to remove</param>
        public static void RemoveWaypoint(Waypoint waypoint)
        {
            FinePrint.WaypointManager.RemoveWaypoint(waypoint);
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
            base.OnLoad(node);

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

                waypoints.Add(waypoint);
                FinePrint.WaypointManager.AddWaypoint(waypoint);

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
    }
}
