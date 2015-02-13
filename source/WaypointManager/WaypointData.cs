using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Contracts;
using FinePrint;

namespace WaypointManager
{
    /// <summary>
    /// Stores additional waypoint data
    /// </summary>
    public class WaypointData
    {
        public class ContractContainer
        {
            public Contract contract;
            public bool hidden = false;
            public List<WaypointData> waypointByContract = new List<WaypointData>();

            public ContractContainer(Contract c)
            {
                contract = c;
            }
        }

        public Waypoint waypoint = null;
        public double distanceToActive = 0.0;
        public double lastChecked = 0.0;
        public CelestialBody celestialBody = null;
        public bool isOccluded = false;
        public double heading;

        private static double lastCacheUpdate = 0.0;

        private static Dictionary<Waypoint, WaypointData> waypointData = new Dictionary<Waypoint, WaypointData>();
        private static Dictionary<Contract, ContractContainer> waypointByContract = new Dictionary<Contract, ContractContainer>();
        private static Dictionary<CelestialBody, List<WaypointData>> waypointByBody = new Dictionary<CelestialBody, List<WaypointData>>();
        private static ContractContainer customWaypoints = new ContractContainer(null);

        /// <summary>
        /// Gets all waypoint data items as an enumeration.
        /// </summary>
        public static IEnumerable<WaypointData> Waypoints { get { return waypointData.Values.AsEnumerable(); } }

        /// <summary>
        /// Gets all waypoint data keyed by contract.
        /// </summary>
        public static IEnumerable<ContractContainer> WaypointByContracts
        {
            get
            {
                foreach (ContractContainer cc in waypointByContract.Values)
                {
                    yield return cc;
                }
                if (customWaypoints.waypointByContract.Count != 0)
                {
                    yield return customWaypoints;
                }
            }
        }

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
                            wpd.waypoint.height = Util.WaypointHeight(w, wpd.celestialBody);

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
                        if (FlightGlobals.ActiveVessel != null && wpd.celestialBody == FlightGlobals.ActiveVessel.mainBody)
                        {
                            wpd.distanceToActive = Util.GetDistanceToWaypoint(wpd);

                            // Get information about whether the waypoint is occluded
                            Vector3 pos = wpd.celestialBody.GetWorldSurfacePosition(wpd.waypoint.latitude, wpd.waypoint.longitude, wpd.waypoint.height + wpd.waypoint.altitude);
                            wpd.isOccluded = IsOccluded(wpd.celestialBody, FlightCamera.fetch.transform.position, pos, wpd.waypoint.height + wpd.waypoint.altitude);

                            Vector3 vHeading = FlightGlobals.ActiveVessel.transform.up;

                            double vesselLat = FlightGlobals.ActiveVessel.latitude / 180.0 * Math.PI;
                            double vesselLon = FlightGlobals.ActiveVessel.longitude / 180.0 * Math.PI;
                            double wpLat = wpd.waypoint.latitude / 180.0 * Math.PI;
                            double wpLon = wpd.waypoint.longitude / 180.0 * Math.PI;

                            double y = Math.Sin(wpLon - vesselLon) * Math.Cos(wpLat);
                            double x = (Math.Cos(vesselLat) * Math.Sin(wpLat)) - (Math.Sin(vesselLat) * Math.Cos(wpLat) * Math.Cos(wpLon - vesselLon));
                            double requiredHeading = Math.Atan2(y, x) * 180.0 / Math.PI;
                            wpd.heading = (requiredHeading + 360.0) % 360.0;
                        }
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
                // Clear the by contract list
                foreach (ContractContainer cc in waypointByContract.Values)
                {
                    cc.waypointByContract.Clear();
                }

                // Rebuild the by contract list
                customWaypoints.waypointByContract.Clear();
                foreach (WaypointData wpd in waypointData.Values)
                {
                    if (wpd.waypoint.contractReference != null)
                    {
                        if (!waypointByContract.ContainsKey(wpd.waypoint.contractReference))
                        {
                            waypointByContract[wpd.waypoint.contractReference] = new ContractContainer(wpd.waypoint.contractReference);
                        }
                        waypointByContract[wpd.waypoint.contractReference].waypointByContract.Add(wpd);
                    }
                    else
                    {
                        customWaypoints.waypointByContract.Add(wpd);
                    }
                }

                // Remove any unused contracts
                foreach (ContractContainer cc in waypointByContract.Values.ToList())
                {
                    if (cc.waypointByContract.Count == 0)
                    {
                        waypointByContract.Remove(cc.contract);
                    }
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

        /// <summary>
        /// Checks if the camera can view the given point.
        /// </summary>
        /// <param name="body">The celestial body to check.</param>
        /// <param name="camera">Camera position</param>
        /// <param name="point">Waypoint position</param>
        /// <returns>Whether the waypoint is considered occluded</returns>
        private static bool IsOccluded(CelestialBody body, Vector3 camera, Vector3 point, double altitude)
        {
            // Really quick and dirty calculation for occlusion - use the cosine law to get the angle formed by BPC.
            // If the angle is < 90, then it is occluded.
            // Gets more complicated once we start taking altitude into effect
            Vector3 PC = (camera - point).normalized;
            Vector3 PB = (body.transform.position - point).normalized;

            double sinRefAngle = body.Radius / (body.Radius + altitude);
            return Vector3.Dot(PC, PB) > Math.Sqrt(1 - sinRefAngle * sinRefAngle) + 0.05; // Give a little grace
        }
    }
}
