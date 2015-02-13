using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using FinePrint;
using FinePrint.Utilities;

namespace WaypointManager
{
    public static class CustomWaypointGUI
    {
        private const float ICON_PICKER_WIDTH = 302;
        private enum WindowMode
        {
            None,
            Add,
            Edit,
            Delete
        }
        
        // So, what is this random list of numbers?  It's a side effect from the awesome
        // design decision in KSP/FinePrint to make stuff based on a random seed.  There
        // is no way to externally provide a color for the waypoint, so instead we provide
        // the seeds that give us the colors we want.
        private static int[] seeds = new int[] {
            269, 316, 876, 9, 569, 159, 262, 822, 412, 972, 105, 665, 255, 358, 1375, 51,
            98, 1115, 248, 808, 398, 501, 91, 651, 241, 344, 904, 37, 597, 187, 747, 337,
            384, 487, 77, 180, 1197, 330, 890, 23, 583, 173, 276, 1293, 426, 16, 119, 679,
        };

        private static Rect wpWindowPos = new Rect(116f, 131f, 264f, 152f);
        private static Rect rmWindowPos = new Rect(116f, 131f, 280f, 80f);
        private static WindowMode windowMode = WindowMode.None;
        private static Waypoint template = new Waypoint();
        private static string latitude;
        private static string longitude;
        private static string altitude;
        private static Waypoint selectedWaypoint = null;
        private static Rect iconPickerPosition;
        private static bool showIconPicker = false;
        private static bool useTerrainHeight = false;
        private static bool recalcAltitude = false;
        private static GUIContent[] icons = null;
        private static GUIContent[] colors = null;

        private static bool mapLocationMode = false;

        private static int selectedIcon = 0;
        private static int selectedColor = 0;

        private static GUIStyle colorWheelStyle;
        private static GUIStyle colorLabelStyle;
        private static GUIStyle disabledText;

        /// <summary>
        /// Interface for showing the add waypoint dialog.
        /// </summary>
        public static void AddWaypoint()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v != null)
            {
                AddWaypoint(v.latitude, v.longitude, v.altitude);
            }
            else
            {
                AddWaypoint(0.0, 0.0, 0.0);
            }
        }
        
        /// <summary>
        /// Interface for showing the add waypoint dialog.
        /// </summary>
        public static void AddWaypoint(double latitude, double longitude)
        {
            AddWaypoint(latitude, longitude, Util.TerrainHeight(latitude, longitude, FlightGlobals.currentMainBody));
        }

        /// <summary>
        /// Interface for showing the add waypoint dialog.
        /// </summary>
        public static void AddWaypoint(double latitude, double longitude, double altitude)
        {
            System.Random r = new System.Random();
            windowMode = WindowMode.Add;

            template.name = "Waypoint Name";
            template.celestialName = FlightGlobals.currentMainBody.name;
            CustomWaypointGUI.latitude = latitude.ToString();
            CustomWaypointGUI.longitude = longitude.ToString();
            CustomWaypointGUI.altitude = altitude.ToString();

            // Default values
            wpWindowPos = new Rect((Screen.width - wpWindowPos.width) / 2.0f, (Screen.height - wpWindowPos.height) / 2.0f - 100f, wpWindowPos.width, wpWindowPos.height);
            selectedIcon = (int)(r.NextDouble() * icons.Count());
            selectedColor = (int)(r.NextDouble() * seeds.Count());
            template.id = icons[selectedIcon].tooltip;
            template.seed = seeds[selectedColor];
        }

        /// <summary>
        /// Interface for showing the edit waypoint dialog.
        /// </summary>
        public static void EditWaypoint(Waypoint waypoint)
        {
            windowMode = WindowMode.Edit;
            selectedWaypoint = waypoint;

            template.name = waypoint.name;
            template.celestialName = waypoint.celestialName;
            latitude = waypoint.latitude.ToString();
            longitude = waypoint.longitude.ToString();
            altitude = (waypoint.altitude + Util.WaypointHeight(waypoint, Util.GetBody(waypoint.celestialName))).ToString();
            template.id = waypoint.id;
            template.seed = waypoint.seed;

            // Default values
            wpWindowPos = new Rect((Screen.width - wpWindowPos.width) / 2.0f, (Screen.height - wpWindowPos.height) / 2.0f - 100f, wpWindowPos.width, wpWindowPos.height);
        }

        /// <summary>
        /// Interface for showing the delete waypoint dialog.
        /// </summary>
        public static void DeleteWaypoint(Waypoint waypoint)
        {
            windowMode = WindowMode.Delete;
            selectedWaypoint = waypoint;

            // Default values
            rmWindowPos = new Rect((Screen.width - rmWindowPos.width) / 2.0f, (Screen.height - rmWindowPos.height) / 2.0f, rmWindowPos.width, rmWindowPos.height);
        }

        public static void OnGUI()
        {
            // Initialize icon list
            if (icons == null)
            {
                List<GUIContent> content = new List<GUIContent>();

                // List of icons we don't want to look at in the Squad directory
                string[] forbidden = new string[] {
                    "an", "ap", "default", "dn", "marker", "orbit", "pe"
                };

                foreach (GameDatabase.TextureInfo texInfo in GameDatabase.Instance.databaseTexture.Where(t => t.name.StartsWith("Squad/Contracts/Icons/")))
                {
                    string name = texInfo.name.Replace("Squad/Contracts/Icons/", "");
                    if (forbidden.Contains(name))
                    {
                        continue;
                    }

                    content.Add(new GUIContent(ContractDefs.textures[name], name));
                }

                icons = content.ToArray();
            }

            // Initialize color list
            if (colors == null)
            {
                List<GUIContent> content = new List<GUIContent>();

                foreach (int seed in seeds)
                {
                    Color color = SystemUtilities.RandomColor(seed, 1.0f, 1.0f, 1.0f);
                    Texture2D texture = new Texture2D(6, 12, TextureFormat.RGBA32, false);

                    Color[] pixels = new Color[6 * 16];
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        pixels[i] = color;
                    }
                    texture.SetPixels(pixels);
                    texture.Compress(true);

                    content.Add(new GUIContent(texture));
                }

                colors = content.ToArray();

                // Set the styles used
                colorWheelStyle = new GUIStyle(GUI.skin.label);
                colorWheelStyle.padding = new RectOffset(0, 0, 2, 2);
                colorWheelStyle.margin = new RectOffset(0, -1, 0, 0);

                colorLabelStyle = new GUIStyle(GUI.skin.label);
                colorLabelStyle.padding = new RectOffset(0, 0, 0, 0);
                colorLabelStyle.margin = new RectOffset(4, 4, 6, 6);
                colorLabelStyle.stretchWidth = true;
                colorLabelStyle.fixedHeight = 12;

                disabledText = new GUIStyle(GUI.skin.textField);
                disabledText.normal.textColor = Color.gray;
            }

            if (windowMode != WindowMode.None && windowMode != WindowMode.Delete)
            {
                wpWindowPos = GUILayout.Window(
                    typeof(WaypointManager).FullName.GetHashCode() + 2,
                    wpWindowPos,
                    WindowGUI,
                    windowMode.ToString() + " Waypoint",
                    GUILayout.Height(1), GUILayout.ExpandHeight(true));

                // Add the close icon
                if (GUI.Button(new Rect(wpWindowPos.xMax - 18, wpWindowPos.yMin + 2, 16, 16), Config.closeIcon, GUI.skin.label))
                {
                    windowMode = WindowMode.None;
                }

                if (showIconPicker)
                {
                    // Default iconPicker position
                    if (iconPickerPosition.xMin == iconPickerPosition.xMax)
                    {
                        iconPickerPosition = new Rect((Screen.width - ICON_PICKER_WIDTH) / 2.0f, wpWindowPos.yMax, ICON_PICKER_WIDTH, 1);
                    }

                    iconPickerPosition = GUILayout.Window(
                        typeof(WaypointManager).FullName.GetHashCode() + 3,
                        iconPickerPosition,
                        IconPickerGUI,
                        "Icon Selector");

                    // Add the close icon
                    if (GUI.Button(new Rect(iconPickerPosition.xMax - 18, iconPickerPosition.yMin + 2, 16, 16), Config.closeIcon, GUI.skin.label))
                    {
                        showIconPicker = false;
                    }
                }

                // Reset the position of the iconPicker window
                if (!showIconPicker)
                {
                    iconPickerPosition.xMax = iconPickerPosition.xMin;
                }
            }
            else if (windowMode == WindowMode.Delete)
            {
                rmWindowPos = GUILayout.Window(
                    typeof(WaypointManager).FullName.GetHashCode() + 2,
                    rmWindowPos,
                    DeleteGUI,
                    windowMode.ToString() + " Waypoint");

                // Add the close icon
                if (GUI.Button(new Rect(rmWindowPos.xMax - 18, rmWindowPos.yMin + 2, 16, 16), Config.closeIcon, GUI.skin.label))
                {
                    windowMode = WindowMode.None;
                }
            }
        }

        private static void DeleteGUI(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Delete custom waypoint '" + selectedWaypoint.name + "'?");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes"))
            {
                CustomWaypoints.RemoveWaypoint(selectedWaypoint);
                windowMode = WindowMode.None;
            }
            if (GUILayout.Button("No"))
            {
                windowMode = WindowMode.None;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        private static void WindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            template.name = GUILayout.TextField(template.name);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("Longitude", GUILayout.Width(68));
            GUILayout.Label("Latitude", GUILayout.Width(68));
            GUILayout.EndVertical();

            GUILayout.Space(4);

            string val;
            float floatVal;
            GUILayout.BeginVertical();
            val = GUILayout.TextField(longitude, GUILayout.Width(140));
            if (float.TryParse(val, out floatVal))
            {
                longitude = val;
                recalcAltitude = true;
            }
            val = GUILayout.TextField(latitude, GUILayout.Width(140));
            if (float.TryParse(val, out floatVal))
            {
                latitude = val;
                recalcAltitude = true;
            }

            GUILayout.EndVertical();

            GUILayout.Space(4);

            GUILayout.BeginVertical();
            if (GUILayout.Button(Util.GetContractIcon(template.id, template.seed)))
            {
                showIconPicker = !showIconPicker;

                selectedIcon = Array.IndexOf(icons, icons.Where(c => c.tooltip == template.id).First());
                selectedColor = Array.IndexOf(seeds, template.seed);
                if (selectedIcon == -1)
                {
                    selectedIcon = 0;
                }
                if (selectedColor == -1)
                {
                    selectedColor = 0;
                }
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(80);
            if (GUILayout.Toggle(useTerrainHeight, new GUIContent("Use terrain height for altitude", "Automatically set the altitude to ground level.")) != useTerrainHeight)
            {
                useTerrainHeight = !useTerrainHeight;
                recalcAltitude = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            GUILayout.Label("Altitude", GUILayout.Width(72));
            val = GUILayout.TextField(altitude, useTerrainHeight ? disabledText : GUI.skin.textField, GUILayout.Width(140));
            if (!useTerrainHeight && float.TryParse(val, out floatVal))
            {
                altitude = val;
            }
            GUILayout.EndHorizontal();

            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                if (GUILayout.Button(new GUIContent("Use Active Vessel Location", "Set the location parameters to that of the currently active vessel.")))
                {
                    latitude = FlightGlobals.ActiveVessel.latitude.ToString();
                    longitude = FlightGlobals.ActiveVessel.longitude.ToString();
                    altitude = FlightGlobals.ActiveVessel.altitude.ToString();
                    recalcAltitude = true;
                }
            }

/*            if (HighLogic.LoadedScene == GameScenes.FLIGHT && MapView.MapIsEnabled)
            {
                string label = mapLocationMode ? "Cancel Set Location" : "Set Location";
                if (GUILayout.Button(new GUIContent(label, "Set the location by clicking on the map.")))
                {
                    mapLocationMode = !mapLocationMode;
                }
            }*/

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
            {
                template.latitude = double.Parse(latitude);
                template.longitude = double.Parse(longitude);
                template.height = Util.WaypointHeight(template, Util.GetBody(template.celestialName));
                template.altitude = double.Parse(altitude) - template.height;
                if (windowMode == WindowMode.Add)
                {
                    CustomWaypoints.AddWaypoint(template);
                    template = new Waypoint();
                }
                else
                {
                    selectedWaypoint.id = template.id;
                    selectedWaypoint.name = template.name;
                    selectedWaypoint.latitude = template.latitude;
                    selectedWaypoint.longitude = template.longitude;
                    selectedWaypoint.altitude = template.altitude;
                    selectedWaypoint.height = template.height;
                    selectedWaypoint.seed = template.seed;
                }

                windowMode = WindowMode.None;
            }
            if (GUILayout.Button("Cancel"))
            {
                windowMode = WindowMode.None;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUI.DragWindow();

            if (useTerrainHeight && recalcAltitude)
            {
                recalcAltitude = false;
                altitude = Util.TerrainHeight(double.Parse(latitude), double.Parse(longitude), FlightGlobals.currentMainBody).ToString();
            }

            WaypointManager.Instance.SetToolTip(windowID - typeof(WaypointManager).FullName.GetHashCode());
        }

        private static void IconPickerGUI(int windowID)
        {
            GUILayout.BeginVertical(GUILayout.Width(ICON_PICKER_WIDTH));
            selectedIcon = GUILayout.SelectionGrid(selectedIcon, icons, 4);

            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            colorLabelStyle.normal.background = colors[selectedColor].image as Texture2D;
            GUILayout.Label(colors[selectedColor], colorLabelStyle, GUILayout.Width(288));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            foreach (GUIContent color in colors)
            {
                GUILayout.Label(color, colorWheelStyle, GUILayout.Width(6));
            }
            GUILayout.Space(4);
            GUILayout.EndHorizontal();
            selectedColor = (int)GUILayout.HorizontalSlider((int)selectedColor, 0, 47);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("OK"))
            {
                showIconPicker = false;
                template.id = icons[selectedIcon].tooltip;
                template.seed = seeds[selectedColor];
            }
            if (GUILayout.Button("Cancel"))
            {
                showIconPicker = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUI.DragWindow();

            WaypointManager.Instance.SetToolTip(windowID - typeof(WaypointManager).FullName.GetHashCode());
        }

        /// <summary>
        /// Draw the marker for waypoint that is in the process of being added
        /// </summary>
        public static void DrawMarker()
        {
            // Only handle on repaint events
            if (windowMode == WindowMode.Add && Event.current.type == EventType.Repaint)
            {
                // Translate to screen position
                Vector3d localSpacePoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(Double.Parse(latitude), Double.Parse(longitude), double.Parse(altitude));
                Vector3d scaledSpacePoint = ScaledSpace.LocalToScaledSpace(localSpacePoint);
                Vector3 screenPos = MapView.MapCamera.camera.WorldToScreenPoint(new Vector3((float)scaledSpacePoint.x, (float)scaledSpacePoint.y, (float)scaledSpacePoint.z));

                // Don't draw if it's behind the camera
                Camera camera = MapView.MapIsEnabled ? PlanetariumCamera.Camera : FlightCamera.fetch.mainCamera;
                Vector3 cameraPos = ScaledSpace.ScaledToLocalSpace(camera.transform.position);
                if (Vector3d.Dot(camera.transform.forward, scaledSpacePoint.normalized) < 0.0)
                {
                    return;
                }

                // Draw the marker at half-resolution (30 x 45) - that seems to match the one in the map view
                Rect markerRect = new Rect(screenPos.x - 15f, (float)Screen.height - screenPos.y - 45.0f, 30f, 45f);

                // Half-res for the icon too (16 x 16)
                Rect iconRect = new Rect(screenPos.x - 8f, (float)Screen.height - screenPos.y - 39.0f, 16f, 16f);

                // Draw the marker
                bool occluded = WaypointData.IsOccluded(FlightGlobals.currentMainBody, cameraPos, localSpacePoint, double.Parse(altitude));
                if (!occluded)
                {
                    Graphics.DrawTexture(markerRect, GameDatabase.Instance.GetTexture("Squad/Contracts/Icons/marker", false), new Rect(0.0f, 0.0f, 1f, 1f), 0, 0, 0, 0, new Color(0.5f, 0.5f, 0.5f, 0.5f));
                }

                // Draw the icon
                Graphics.DrawTexture(iconRect, ContractDefs.textures[template.id], new Rect(0.0f, 0.0f, 1f, 1f), 0, 0, 0, 0, SystemUtilities.RandomColor(template.seed, occluded ? 0.3f : 1.0f));
            }
        }
    }
}
