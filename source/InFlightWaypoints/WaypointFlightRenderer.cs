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
        private GUIStyle NameStyle = null;
        private GUIStyle ValueStyle = null;
        private string[] UNITS = { "m", "km", "Mm", "Gm", "Tm" };

        private bool visible = true;
        private Waypoint selectedWaypoint = null;
        private string waypointName = "";
        private Rect windowPos;
        private bool newClick = false;

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

                // Destroy this object - otherwise we'll have two
                Destroy(this);
            }

            GameEvents.onHideUI.Add(new EventVoid.OnEvent(OnHideUI));
            GameEvents.onShowUI.Add(new EventVoid.OnEvent(OnShowUI));
        }

        protected void OnDestroy()
        {
            GameEvents.onHideUI.Remove(OnHideUI);
            GameEvents.onShowUI.Remove(OnShowUI);
        }

        public void OnHideUI()
        {
            visible = false;
        }

        public void OnShowUI()
        {
            visible = true;
        }

        public void OnGUI()
        {
            if (HighLogic.LoadedSceneIsFlight && !MapView.MapIsEnabled && visible)
            {
                SetupStyles();

                if (WaypointManager.Instance() != null)
                {
                    if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
                    {
                        newClick = true;
                    }

                    CacheWaypointData();

                    foreach (WaypointData wpd in waypointData.Values)
                    {
                        DrawWaypoint(wpd);
                    }

                    ShowNavigationWindow();
                }
            }
        }

        // Styles taken directly from Kerbal Engineer Redux - because they look great and this will
        // make our display consistent with that
        protected void SetupStyles()
        {
            if (NameStyle != null)
            {
                return;
            }

            NameStyle = new GUIStyle(HighLogic.Skin.label)
            {
                normal =
                {
                    textColor = Color.white
                },
                margin = new RectOffset(),
                padding = new RectOffset(5, 0, 0, 0),
                alignment = TextAnchor.MiddleRight,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                fixedHeight = 20.0f
            };

            ValueStyle = new GUIStyle(HighLogic.Skin.label)
            {
                margin = new RectOffset(),
                padding = new RectOffset(0, 5, 0, 0),
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                fixedHeight = 20.0f
            };
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
                if (w != null && w.celestialName == celestialBody.name && w.isNavigatable)
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

            // Figure out waypoint label
            string label = wpd.waypoint.name + (wpd.waypoint.isClustered ? (" " + StringUtilities.IntegerToGreek(wpd.waypoint.index)) : "");

            // Decide whether to actually draw the waypoint
            float alpha = 1.0f;
            if (FlightGlobals.ActiveVessel != null)
            {
                // Figure out the distance to the waypoint
                Vessel v = FlightGlobals.ActiveVessel;
                Vector3d waypointLocation = celestialBody.GetRelSurfacePosition(wpd.waypoint.longitude, wpd.waypoint.latitude, wpd.height + wpd.waypoint.altitude);
                Vector3d vesselLocation = celestialBody.GetRelSurfacePosition(v.longitude, v.latitude, v.altitude);
                double distance = Vector3d.Distance(vesselLocation, waypointLocation);

                // Get the distance to the waypoint at the current speed
                double speed = v.srfSpeed < MIN_SPEED ? MIN_SPEED : v.srfSpeed;
                double time = distance / speed;

                // Only change alpha if the waypoint isn't the nav point
                if (!IsNavPoint(wpd.waypoint))
                {
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
                // Draw the distance information to the nav point
                else
                {
                    int unit = 0;
                    while (unit < 4 && distance >= 10000.0)
                    {
                        distance /= 1000.0;
                        unit++;
                    }
                    // Draw the distance to waypoint text
                    if (Event.current.type == EventType.Repaint)
                    {
                        GUI.Label(new Rect((float)Screen.width / 2.0f - 168f, 72f, 240f, 32f), "Distance to " + label + ":", NameStyle);
                        GUI.Label(new Rect((float)Screen.width / 2.0f + 88f, 72f, 160f, 32f), distance.ToString("N1") + " " + UNITS[unit], ValueStyle);
                    }
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

            // Set the window position relative to the selected waypoint
            if (selectedWaypoint == wpd.waypoint)
            {
                windowPos = new Rect(markerRect.xMin - 97, markerRect.yMax + 12, 224, 60);
            }

            // Handling clicking on the waypoint
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                if (markerRect.Contains(Event.current.mousePosition))
                {
                    selectedWaypoint = wpd.waypoint;
                    windowPos = new Rect(markerRect.xMin - 97, markerRect.yMax + 12, 224, 60);
                    waypointName = label;
                    newClick = false;
                }
                else if (newClick)
                {
                    selectedWaypoint = null;
                }
            }

            // Only handle on repaint events
            if (Event.current.type == EventType.Repaint)
            {
                // Half-res for the icon too (16 x 16)
                Rect iconRect = new Rect(screenPos.x - 8f, (float)Screen.height - screenPos.y - 39.0f, 16f, 16f);

                // Draw the marker and icon
                Graphics.DrawTexture(markerRect, GameDatabase.Instance.GetTexture("Squad/Contracts/Icons/marker", false), new Rect(0.0f, 0.0f, 1f, 1f), 0, 0, 0, 0, new Color(0.5f, 0.5f, 0.5f, alpha * 0.5f));
                Graphics.DrawTexture(iconRect, ContractDefs.textures[wpd.waypoint.id], new Rect(0.0f, 0.0f, 1f, 1f), 0, 0, 0, 0, SystemUtilities.RandomColor(wpd.waypoint.seed, alpha));

                // Hint text!
                if (iconRect.Contains(Event.current.mousePosition))
                {
                    // Add agency to label
                    if (wpd.waypoint.contractReference != null)
                    {
                        label += "\n" + wpd.waypoint.contractReference.Agent.Name;
                    }
                    float yoffset = label.Count(c => c == '\n') * 32.0f + 45.0f;
                    GUI.Label(new Rect(screenPos.x - 40f, (float)Screen.height - screenPos.y - yoffset, 80f, 32f), label, MapView.OrbitIconsTextSkin.label);
                }
            }
        }

        private void ShowNavigationWindow()
        {
            if (selectedWaypoint != null)
            {
                GUI.skin = HighLogic.Skin;
                windowPos = GUILayout.Window(10, windowPos, NavigationWindow, waypointName, GUILayout.MinWidth(224));
            }
        }

        private void NavigationWindow(int windowID)
        {
            GUILayout.BeginVertical();
            if (!IsNavPoint(selectedWaypoint))
            {
                if (GUILayout.Button("Activate Navigation", HighLogic.Skin.button, GUILayout.ExpandWidth(true)))
                {
                    WaypointManager.setupNavPoint(selectedWaypoint);
                    WaypointManager.activateNavPoint();
                    selectedWaypoint = null;
                }
            }
            else
            {
                if (GUILayout.Button("Deactivate Navigation", HighLogic.Skin.button, GUILayout.ExpandWidth(true)))
                {
                    WaypointManager.clearNavPoint();
                    selectedWaypoint = null;
                }

            }
            GUILayout.EndVertical();
        }


        protected bool IsNavPoint(Waypoint waypoint)
        {
            NavWaypoint navPoint = WaypointManager.navWaypoint;
            if (navPoint == null || WaypointManager.Instance() == null || !WaypointManager.navIsActive())
            {
                return false;
            }

            return navPoint.latitude == waypoint.latitude && navPoint.longitude == waypoint.longitude;

        }
    }
}
