using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using FinePrint;
using FinePrint.Utilities;
using ClickThroughFix;
using ToolbarControl_NS;

namespace WaypointManager
{
    public static class CustomWaypointGUI
    {
        // List of icons we don't want to look at in the Squad directory
        public static string[] forbiddenIcons = new string[] {
            "an", "ap", "default", "dn", "marker", "orbit", "pe", "vessel"
        };

        public static List<string> customIcons = new List<string>();

        internal const float ICON_PICKER_WIDTH = 302;
        private enum WindowMode
        {
            None,
            Add,
            Edit,
            Edit_Stock,
            Delete
        }
        private static string[] windowModeStr = {
            "None",
            "Add",
            "Edit",
            "Edit Stock",
            "Delete"
        };

        // So, what is this random list of numbers?  It's a side effect from the awesome
        // design decision in KSP/FinePrint to make stuff based on a random seed.  There
        // is no way to externally provide a color for the waypoint, so instead we provide
        // the seeds that give us the colors we want.
        private static int[] seeds = new int[] {
            269, 316, 876, 9, 569, 159, 262, 822, 412, 972, 105, 665, 255, 358, 1375, 51,
            98, 1115, 248, 808, 398, 501, 91, 651, 241, 344, 904, 37, 597, 187, 747, 337,
            384, 487, 77, 180, 1197, 330, 890, 23, 583, 173, 276, 1293, 426, 16, 119, 679,
        };

        private static MapObject mapObject;
        public static MapObject MapObject
        {
            get { return mapObject; }
            set
            {
                mapObject = value;
                if (mapObject != null)
                {
                    if (mapObject.type == global::MapObject.ObjectType.ManeuverNode)
                    {
                        mapObject = mapObject.maneuverNode.scaledSpaceTarget;
                    }

                    if (mapObject.type == global::MapObject.ObjectType.CelestialBody)
                    {
                        targetBody = mapObject.celestialBody;
                    }
                    else if (mapObject.type == global::MapObject.ObjectType.Vessel)
                    {
                        targetBody = mapObject.vessel.mainBody;
                    }

                    if (template != null)
                    {
                        template.celestialName = targetBody.name;
                    }
                }
            }
        }
        private static CelestialBody targetBody;

        private static Rect wpWindowPos = new Rect(116f, 131f, 264f, 152f);
        private static Rect rmWindowPos = new Rect(116f, 131f, 280f, 80f);
        private static Rect expWindowPos = new Rect(116f, 131f, 280f, 80f);
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

        private static bool showExportDialog = false;

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
                if (!MapView.MapIsEnabled)
                {
                    targetBody = v.mainBody;
                }
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
            if (!MapView.MapIsEnabled)
            {
                Vessel v = FlightGlobals.ActiveVessel;
                targetBody = v.mainBody;
            }
            AddWaypoint(latitude, longitude, Util.TerrainHeight(latitude, longitude, targetBody));
        }

        /// <summary>
        /// Interface for showing the add waypoint dialog.
        /// </summary>
        public static void AddWaypoint(double latitude, double longitude, double altitude)
        {
            if (windowMode == WindowMode.None)
            {
                wpWindowPos = new Rect((Screen.width - wpWindowPos.width) / 2.0f, (Screen.height - wpWindowPos.height) / 2.0f - 100f, wpWindowPos.width, wpWindowPos.height);
            }

            System.Random r = new System.Random();
            windowMode = WindowMode.Add;

            template.name = StringUtilities.GenerateSiteName(r.Next(), targetBody, false);
            template.celestialName = targetBody.name;
            CustomWaypointGUI.latitude = latitude.ToString();
            CustomWaypointGUI.longitude = longitude.ToString();
            CustomWaypointGUI.altitude = altitude.ToString();

            // Default values
            selectedIcon = (int)(r.NextDouble() * icons.Count());
            selectedColor = (int)(r.NextDouble() * seeds.Count());
            template.id = icons[selectedIcon].tooltip;
            template.seed = seeds[selectedColor];
        }

        /// <summary>
        /// Interface for showing the edit waypoint dialog.
        /// </summary>
        public static void EditWaypoint(Waypoint waypoint, bool stock = false)
        {
            if (windowMode == WindowMode.None)
            {
                wpWindowPos = new Rect((Screen.width - wpWindowPos.width) / 2.0f, (Screen.height - wpWindowPos.height) / 2.0f - 100f, wpWindowPos.width, wpWindowPos.height);
            }

            if (stock)
                windowMode = WindowMode.Edit_Stock;
            else
                windowMode = WindowMode.Edit;
            selectedWaypoint = waypoint;

            template.name = waypoint.name;
            template.celestialName = waypoint.celestialName;
            latitude = waypoint.latitude.ToString();
            longitude = waypoint.longitude.ToString();
            altitude = (waypoint.altitude + Util.WaypointHeight(waypoint, targetBody)).ToString();
            template.id = waypoint.id;
            template.seed = waypoint.seed;
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

        public static void ShowExportDialog()
        {
            showExportDialog = true;

            // Default values
            expWindowPos = new Rect((Screen.width - expWindowPos.width) / 2.0f, (Screen.height - expWindowPos.height) / 2.0f, expWindowPos.width, expWindowPos.height);
        }

        public static void OnGUI()
        {
            // Initialize icon list
            if (icons == null)
            {
                List<GUIContent> content = new List<GUIContent>();

                // Get all the stock icons
                foreach (GameDatabase.TextureInfo texInfo in GameDatabase.Instance.databaseTexture.Where(t => t.name.StartsWith("Squad/Contracts/Icons/")))
                {
                    string name = texInfo.name.Replace("Squad/Contracts/Icons/", "");
                    if (forbiddenIcons.Contains(name))
                    {
                        continue;
                    }

                    content.Add(new GUIContent(ContractDefs.sprites[name].texture, name));
                }

                // Get all the directories for custom icons
                ConfigNode[] iconConfig = GameDatabase.Instance.GetConfigNodes("WAYPOINT_MANAGER_ICONS");
                foreach (ConfigNode configNode in iconConfig)
                {
                    string dir = configNode.GetValue("url");
                    if (Directory.Exists("GameData/" + dir))
                    {
                        foreach (var str in Directory.GetFiles("GameData/" + dir))
                        {
                            var icon = new Texture2D(2, 2);
                            ToolbarControl.LoadImageFromFile(ref icon, str);
                            content.Add(new GUIContent(icon, str));
                        }
                    }
#if false
                    foreach (GameDatabase.TextureInfo texInfo in GameDatabase.Instance.databaseTexture.Where(t => t.name.StartsWith(dir)))
                    {
                        content.Add(new GUIContent(texInfo.texture, texInfo.name));
                    }
#endif
                }

                // Add custom icons
                foreach (string icon in customIcons)
                {
                    foreach (GameDatabase.TextureInfo texInfo in GameDatabase.Instance.databaseTexture)
                    {
                        if (texInfo.name == icon)
                        {
                            content.Add(new GUIContent(texInfo.texture, texInfo.name));
                            break;
                        }
                    }
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


            if (WaypointManager.Instance != null && WaypointManager.Instance.visible && !ImportExport.helpDialogVisible)
            {
                if (windowMode != WindowMode.None && windowMode != WindowMode.Delete)
                {
                    wpWindowPos = ClickThruBlocker.GUILayoutWindow(
                        typeof(WaypointManager).FullName.GetHashCode() + 2,
                        wpWindowPos,
                        WindowGUI,
                        windowModeStr[(int)windowMode] + " Waypoint",
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

                        iconPickerPosition = ClickThruBlocker.GUILayoutWindow(
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
                    rmWindowPos = ClickThruBlocker.GUILayoutWindow(
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

                if (showExportDialog)
                {
                    expWindowPos = ClickThruBlocker.GUILayoutWindow(
                        typeof(WaypointManager).FullName.GetHashCode() + 3,
                        expWindowPos,
                        ExportGUI,
                        "Overwrite export file?");

                    // Add the close icon
                    if (GUI.Button(new Rect(expWindowPos.xMax - 18, expWindowPos.yMin + 2, 16, 16), Config.closeIcon, GUI.skin.label))
                    {
                        showExportDialog = false;
                    }
                }

                if (mapLocationMode)
                {
                    PlaceWaypointAtCursor();

                    // Lock the waypoint if the user clicks
                    if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
                    {
                        mapLocationMode = false;
                    }
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

        private static void ExportGUI(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Overwrite custom waypoint file '" + CustomWaypoints.CustomWaypointsFileName + "'?");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes"))
            {
                CustomWaypoints.DoExport();
                showExportDialog = false;
            }
            if (GUILayout.Button("No"))
            {
                showExportDialog = false;
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
            GUILayout.Label("Latitude", GUILayout.Width(68));
            GUILayout.Label("Longitude", GUILayout.Width(68));
            GUILayout.EndVertical();

            GUILayout.Space(4);

            string val;
            float floatVal;
            GUILayout.BeginVertical();
            val = GUILayout.TextField(latitude, GUILayout.Width(140));
            if (float.TryParse(val, out floatVal))
            {
                latitude = val;
                recalcAltitude = true;
            }
            val = GUILayout.TextField(longitude, GUILayout.Width(140));
            if (float.TryParse(val, out floatVal))
            {
                longitude = val;
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

            if (HighLogic.LoadedScene == GameScenes.FLIGHT && MapView.MapIsEnabled || HighLogic.LoadedScene == GameScenes.TRACKSTATION)
            {
                string label = mapLocationMode ? "Cancel Set Location" : "Set Location on Map";
                if (GUILayout.Button(new GUIContent(label, "Set the location by clicking on the map.")))
                {
                    mapLocationMode = !mapLocationMode;
                }
            }
            else
            {
                mapLocationMode = false;
            }

            GUILayout.BeginHorizontal();
            bool save = GUILayout.Button("Save");
            bool apply = GUILayout.Button("Apply");
            bool cancel = GUILayout.Button("Cancel");
            if (save || apply)
            {
                template.latitude = double.Parse(latitude);
                template.longitude = double.Parse(longitude);
                template.height = Util.WaypointHeight(template, targetBody);
                template.altitude = double.Parse(altitude) - template.height;
                if (windowMode == WindowMode.Add)
                {
                    CustomWaypoints.AddWaypoint(template);
                    selectedWaypoint = template;
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
            }
            if (save || cancel)
            {
                windowMode = WindowMode.None;
            }
            else if (apply)
            {
                EditWaypoint(selectedWaypoint);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUI.DragWindow();

            if (useTerrainHeight && recalcAltitude)
            {
                recalcAltitude = false;
                altitude = Util.TerrainHeight(double.Parse(latitude), double.Parse(longitude), targetBody).ToString();
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
                Util.DrawWaypoint(targetBody, double.Parse(latitude), double.Parse(longitude), double.Parse(altitude), template.id, template.seed);
            }
        }

        private static void PlaceWaypointAtCursor()
        {
            if (targetBody.pqsController == null)
            {
                return;
            }

            Ray mouseRay = PlanetariumCamera.Camera.ScreenPointToRay(Input.mousePosition);
            mouseRay.origin = ScaledSpace.ScaledToLocalSpace(mouseRay.origin);
            var bodyToOrigin = mouseRay.origin - targetBody.position;
            double curRadius = targetBody.pqsController.radiusMax;
            double lastRadius = 0;
            int loops = 0;
            while (loops < 50)
            {
                Vector3d relSurfacePosition;
                if (PQS.LineSphereIntersection(bodyToOrigin, mouseRay.direction, curRadius, out relSurfacePosition))
                {
                    var surfacePoint = targetBody.position + relSurfacePosition;
                    double alt = targetBody.pqsController.GetSurfaceHeight(
                        QuaternionD.AngleAxis(targetBody.GetLongitude(surfacePoint), Vector3d.down) * QuaternionD.AngleAxis(targetBody.GetLatitude(surfacePoint), Vector3d.forward) * Vector3d.right);
                    double error = Math.Abs(curRadius - alt);
                    if (error < (targetBody.pqsController.radiusMax - targetBody.pqsController.radiusMin) / 100)
                    {
                        latitude = targetBody.GetLatitude(surfacePoint).ToString();
                        longitude = targetBody.GetLongitude(surfacePoint).ToString();
                        return;
                    }
                    else
                    {
                        lastRadius = curRadius;
                        curRadius = alt;
                        loops++;
                    }
                }
                else
                {
                    if (loops == 0)
                    {
                        break;
                    }
                    // Went too low, needs to try higher
                    else
                    {
                        curRadius = (lastRadius * 9 + curRadius) / 10;
                        loops++;
                    }
                }
            }
        }
    }
}
