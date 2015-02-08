using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Contracts;
using FinePrint;

namespace WaypointManager
{
    /// <summary>
    /// Stores additional waypoint data
    /// </summary>
    public class WaypointData
    {
        public Waypoint waypoint = null;
        public double height = 0.0;
        public double distanceToActive = 0.0;
        public double lastChecked = 0.0;
        public CelestialBody celestialBody = null;

        private static double lastCacheUpdate = 0.0;

        private static Dictionary<Waypoint, WaypointData> waypointData = new Dictionary<Waypoint, WaypointData>();
        private static Dictionary<Contract, List<WaypointData>> waypointByContract = new Dictionary<Contract, List<WaypointData>>();
        private static Dictionary<CelestialBody, List<WaypointData>> waypointByBody = new Dictionary<CelestialBody, List<WaypointData>>();

        /// <summary>
        /// Gets all waypoint data items as an enumeration.
        /// </summary>
        public static IEnumerable<WaypointData> Waypoints { get { return waypointData.Values.AsEnumerable(); } }

        /// <summary>
        /// Gets all waypoint data keyed by contract.
        /// </summary>
        public static IEnumerable<KeyValuePair<Contract, List<WaypointData>>> WaypointByContracts { get { return waypointByContract.AsEnumerable(); } }

        /// <summary>
        /// Gets all waypoint data keyed by Celestial Body.
        /// </summary>
        public static IEnumerable<KeyValuePair<CelestialBody, List<WaypointData>>> WaypointByBody { get { return waypointByBody.AsEnumerable(); } }

        /// <summary>
        /// Caches the waypoint data.
        /// </summary>
        public static void CacheWaypointData()
        {
            if (lastCacheUpdate == UnityEngine.Time.fixedTime)
            {
                return;
            }
            lastCacheUpdate = UnityEngine.Time.fixedTime;

            bool changed = false;

            if (FinePrint.WaypointManager.Instance() != null)
            {
                // Add new waypoints
                foreach (Waypoint w in FinePrint.WaypointManager.Instance().AllWaypoints())
                {
                    if (w != null && w.isNavigatable)
                    {
                        WaypointData wpd;

                        // Update values that are only cached once
                        if (!waypointData.ContainsKey(w))
                        {
                            wpd = new WaypointData();
                            wpd.waypoint = w;
                            wpd.celestialBody = Util.GetBody(w.celestialName);

                            // Figure out the terrain height
                            double latRads = Math.PI / 180.0 * w.latitude;
                            double lonRads = Math.PI / 180.0 * w.longitude;
                            Vector3d radialVector = new Vector3d(Math.Cos(latRads) * Math.Cos(lonRads), Math.Sin(latRads), Math.Cos(latRads) * Math.Sin(lonRads));
                            wpd.height = Math.Max(wpd.celestialBody.pqsController.GetSurfaceHeight(radialVector) - wpd.celestialBody.pqsController.radius, 0.0);

                            // Add to waypoint data
                            waypointData[w] = wpd;
                            changed = true;
                        }
                        else
                        {
                            wpd = waypointData[w];
                        }

                        // Update values that change every frame
                        wpd.lastChecked = UnityEngine.Time.fixedTime;
                        wpd.distanceToActive = Util.GetDistanceToWaypoint(wpd);
                    }
                }

                // Remove unused waypoints
                foreach (KeyValuePair<Waypoint, WaypointData> p in waypointData.Where(p => p.Value.lastChecked != UnityEngine.Time.fixedTime).ToArray())
                {
                    changed = true;
                    waypointData.Remove(p.Key);
                }
            }
            else
            {
                changed = true;
                waypointData.Clear();
            }

            if (changed)
            {
                // Rebuild the by contract list
                waypointByContract.Clear();
                foreach (WaypointData wpd in waypointData.Values)
                {
                    if (!waypointByContract.ContainsKey(wpd.waypoint.contractReference))
                    {
                        waypointByContract[wpd.waypoint.contractReference] = new List<WaypointData>();
                    }
                    waypointByContract[wpd.waypoint.contractReference].Add(wpd);
                }

                // Rebuild the by Celestial Body list
                waypointByBody.Clear();
                foreach (WaypointData wpd in waypointData.Values)
                {
                    if (!waypointByBody.ContainsKey(wpd.celestialBody))
                    {
                        waypointByBody[wpd.celestialBody] = new List<WaypointData>();
                    }
                    waypointByBody[wpd.celestialBody].Add(wpd);
                }
            }
        }
    }
}
