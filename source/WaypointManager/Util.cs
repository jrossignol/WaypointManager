using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using FinePrint;

namespace WaypointManager
{
    /// <summary>
    /// Utility methods for WaypointManager.
    /// </summary>
    public static class Util
    {
        private static string[] UNITS = { "m", "km", "Mm", "Gm", "Tm" };

        /// <summary>
        /// Gets the  distance in meters from the activeVessel to the given waypoint.
        /// </summary>
        /// <param name="wpd">Activated waypoint</param>
        /// <returns>Distance in meters</returns>
        public static double GetDistanceToWaypoint(WaypointData wpd)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            CelestialBody celestialBody = v.mainBody;

            // Simple distance
            if (Config.distanceCalcMethod == Config.DistanceCalcMethod.STRAIGHT_LINE || celestialBody != wpd.celestialBody)
            {
                return GetStraightDistance(wpd);
            }

            // Use the haversine formula to calculate great circle distance.
            double sin1 = Math.Sin(Math.PI / 180.0 * (v.latitude - wpd.waypoint.latitude) / 2);
            double sin2 = Math.Sin(Math.PI / 180.0 * (v.longitude - wpd.waypoint.longitude) / 2);
            double cos1 = Math.Cos(Math.PI / 180.0 * wpd.waypoint.latitude);
            double cos2 = Math.Cos(Math.PI / 180.0 * v.latitude);

            double lateralDist = 2 * (celestialBody.Radius + wpd.height + wpd.waypoint.altitude) *
                Math.Asin(Math.Sqrt(sin1 * sin1 + cos1 * cos2 * sin2 * sin2));
            double heightDist = Math.Abs(wpd.waypoint.altitude + wpd.height - v.terrainAltitude);

            if (Config.distanceCalcMethod == Config.DistanceCalcMethod.LATERAL || heightDist <= lateralDist / 2.0)
            {
                return lateralDist;
            }
            else
            {
                // Get the ratio to use in our formula
                double x = (heightDist - lateralDist / 2.0) / lateralDist;

                // x / (x + 1) starts at 0 when x = 0, and increases to 1
                return (x / (x + 1)) * heightDist + lateralDist;
            }
        }

        public static double GetStraightDistance(WaypointData wpd)
        {
            Vessel v = FlightGlobals.ActiveVessel;

            Vector3 wpPosition = wpd.celestialBody.GetWorldSurfacePosition(wpd.waypoint.latitude, wpd.waypoint.longitude, wpd.height + wpd.waypoint.altitude);
            return Vector3.Distance(wpPosition, v.transform.position);
        }

        /// <summary>
        /// Gets the printable distance to the waypoint.
        /// </summary>
        /// <param name="wpd">WaypointData object</param>
        /// <returns>The distance and unit for screen output</returns>
        public static string PrintDistance(WaypointData wpd)
        {
            int unit = 0;
            double distance = wpd.distanceToActive;
            while (unit < 4 && distance >= 10000.0)
            {
                distance /= 1000.0;
                unit++;
            }

            return distance.ToString("N1") + " " + UNITS[unit];
        }

        /// <summary>
        /// Gets the celestial body for the given name.
        /// </summary>
        /// <param name="name">Name of the celestial body</param>
        /// <returns>The CelestialBody object</returns>
        public static CelestialBody GetBody(string name)
        {
            return FlightGlobals.Bodies.Where(b => b.name == name).FirstOrDefault();
        }

        /// <summary>
        /// Checks if the given waypoint is the nav waypoint.
        /// </summary>
        /// <param name="waypoint"></param>
        /// <returns></returns>
        public static bool IsNavPoint(Waypoint waypoint)
        {
            NavWaypoint navPoint = FinePrint.WaypointManager.navWaypoint;
            if (navPoint == null || FinePrint.WaypointManager.Instance() == null || !FinePrint.WaypointManager.navIsActive())
            {
                return false;
            }

            return navPoint.latitude == waypoint.latitude && navPoint.longitude == waypoint.longitude;

        }
    }
}
