using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using FinePrint;
using FinePrint.Utilities;

namespace InFlightWaypoints
{
    [KSPAddon(KSPAddon.Startup.Flight, true)]
    class WaypointFlightRenderer : MonoBehaviour
    {
        // Store additional waypoint data
        protected class WaypointData
        {
            public Waypoint waypoint = null;
            public double height = 0.0;
            public double lastChecked = 0.0;
        }

        private Dictionary<Waypoint, WaypointData> waypointData = new Dictionary<Waypoint, WaypointData>();

        private const double MIN_TIME = 300;
        private const double MIN_DISTANCE = 25000;
        private const double MIN_SPEED = MIN_DISTANCE / MIN_TIME;
        private const double FADE_TIME = 20;

        void Start()
        {
            if (MapView.MapCamera.gameObject.GetComponent<WaypointFlightRenderer>() == null)
            {
                MapView.MapCamera.gameObject.AddComponent<WaypointFlightRenderer>();
            }
        }

        public void OnGUI()
        {
            if (HighLogic.LoadedSceneIsFlight && !MapView.MapIsEnabled)
            {
                if (WaypointManager.Instance() != null)
                {
                    CacheWaypointData();

                    foreach (WaypointData wpd in waypointData.Values)
                    {
                        DrawWaypoint(wpd);
                    }
                }
            }
        }

        // Updates the waypoint data cache
        protected void CacheWaypointData()
        {
            // Only handle the current celestial body
            CelestialBody celestialBody = FlightGlobals.currentMainBody;
            if (celestialBody == null)
            {
                waypointData.Clear();
                return;
            }

            // Add new waypoints
            foreach (Waypoint w in WaypointManager.Instance().AllWaypoints())
            {
                if (w != null && w.celestialName == celestialBody.name)
                {
                    if (!waypointData.ContainsKey(w))
                    {
                        WaypointData wpd = new WaypointData();
                        wpd.waypoint = w;

                        // Figure out the terrain height
                        double latRads = Math.PI / 180.0 * w.latitude;
                        double lonRads = Math.PI / 180.0 * w.longitude;
                        Vector3d radialVector = new Vector3d(Math.Cos(latRads) * Math.Cos(lonRads), Math.Sin(latRads), Math.Cos(latRads) * Math.Sin(lonRads));
                        wpd.height = Math.Max(celestialBody.pqsController.GetSurfaceHeight(radialVector) - celestialBody.pqsController.radius, 0.0);

                        // Add to waypoint data
                        waypointData[w] = wpd;
                    }
                    waypointData[w].lastChecked = UnityEngine.Time.fixedTime;
                }
            }

            // Remove unused waypoints
            foreach (KeyValuePair<Waypoint, WaypointData> p in waypointData.Where(p => p.Value.lastChecked != UnityEngine.Time.fixedTime).ToArray())
            {
                waypointData.Remove(p.Key);
            }
        }

        protected void DrawWaypoint(WaypointData wpd)
        {
            // Not our planet
            CelestialBody celestialBody = FlightGlobals.currentMainBody;
            if (celestialBody == null || wpd.waypoint.celestialName != celestialBody.name)
            {
                return;
            }

            // Only handle on repaint events
            if (Event.current.type == EventType.Repaint)
            {
                // Decide whether to actually draw the waypoint
                float alpha = 1.0f;
                if (FlightGlobals.ActiveVessel != null)
                {
                    // Figure out the distance to the waypoint
                    Vessel v = FlightGlobals.ActiveVessel;
                    Vector3d waypointLocation = celestialBody.GetRelSurfacePosition(wpd.waypoint.longitude, wpd.waypoint.latitude, wpd.waypoint.altitude);
                    Vector3d vesselLocation = celestialBody.GetRelSurfacePosition(v.longitude, v.latitude, v.altitude);
                    double distance = Vector3d.Distance(vesselLocation, waypointLocation);

                    // Get the distance to the waypoint at the current speed
                    double speed = v.srfSpeed < MIN_SPEED ? MIN_SPEED : v.srfSpeed;
                    double time = distance / speed; 

                    // More than two minutes away
                    if (time > MIN_TIME)
                    {
                        return;
                    }
                    else if (time >= MIN_TIME - FADE_TIME)
                    {
                        alpha = (float)((MIN_TIME - time) / FADE_TIME);
                    }
                }

                // Translate to scaled space
                Vector3d localSpacePoint = celestialBody.GetWorldSurfacePosition(wpd.waypoint.latitude, wpd.waypoint.longitude, wpd.height + wpd.waypoint.altitude);
                Vector3d scaledSpacePoint = ScaledSpace.LocalToScaledSpace(localSpacePoint);

                // Don't draw if it's behind the camera
                if (Vector3d.Dot(MapView.MapCamera.camera.transform.forward, scaledSpacePoint.normalized) < 0.0)
                {
                    return;
                }

                // Translate to screen position
                Vector3 screenPos = MapView.MapCamera.camera.WorldToScreenPoint(new Vector3((float)scaledSpacePoint.x, (float)scaledSpacePoint.y, (float)scaledSpacePoint.z));

                // Draw the marker at half-resolution (30 x 45) - that seems to match the one in the map view
                Rect markerRect = new Rect(screenPos.x - 15f, (float)Screen.height - screenPos.y - 45.0f, 30f, 45f);

                // Half-res for the icon too (16 x 16)
                Rect iconRect = new Rect(screenPos.x - 8f, (float)Screen.height - screenPos.y - 39.0f, 16f, 16f);

                // Draw the marker and icon
                Graphics.DrawTexture(markerRect, GameDatabase.Instance.GetTexture("Squad/Contracts/Icons/marker", false), new Rect(0.0f, 0.0f, 1f, 1f), 0, 0, 0, 0, new Color(0.5f, 0.5f, 0.5f, alpha * 0.5f));
                Graphics.DrawTexture(iconRect, ContractDefs.textures[wpd.waypoint.id], new Rect(0.0f, 0.0f, 1f, 1f), 0, 0, 0, 0, SystemUtilities.RandomColor(wpd.waypoint.seed, alpha));

                // Hint text!!
                if (iconRect.Contains(Event.current.mousePosition))
                {
                    string label = wpd.waypoint.name + (wpd.waypoint.isClustered ? (" " + StringUtilities.IntegerToGreek(wpd.waypoint.index)) : "");
                    if (wpd.waypoint.contractReference != null)
                    {
                        label += "\n" + wpd.waypoint.contractReference.Agent.Name;
                    }
                    float yoffset = label.Count(c => c == '\n') * 32.0f + 45.0f;
                    GUI.Label(new Rect(screenPos.x - 40f, (float)Screen.height - screenPos.y - yoffset, 80f, 32f), label, MapView.OrbitIconsTextSkin.label);
                }

            }
        }
    }
}
