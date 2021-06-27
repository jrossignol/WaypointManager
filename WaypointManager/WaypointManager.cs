using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using KSP;
using KSP.UI.Screens;
using Contracts;
using FinePrint;
using FinePrint.Utilities;

using ToolbarControl_NS;
using ClickThroughFix;
using static WaypointManager.RegisterToolbar;

namespace WaypointManager
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    class WaypointManager : MonoBehaviour
    {
        static List<Waypoint> uniqueWaypoints = new List<Waypoint>();

        private const float GUI_WIDTH = 380;
        internal const float SETTINGS_WIDTH = 280;

        public static WaypointManager Instance;

        //private ApplicationLauncherButton launcherButton = null;
        //private IButton toolbarButton;
        ToolbarControl toolbarControl;

        private static bool initialized = false;
        public bool showGUI = false;
        private bool showSettings = false;
        public bool visible = true;
        private bool stylesSetup = false;

        private GUIStyle headerButtonStyle;
        private GUIStyle labelStyle;
        private GUIStyle headingStyle;
        private GUIStyle tipStyle;

        private Vector2 scrollPosition;
        internal Rect settingsPosition;

        private Rect tooltipPosition;
        private List<string> toolTip = new List<string>();
        private List<double> toolTipTime = new List<double>();

        static Dictionary<string, Texture2D> bodyIcons = new Dictionary<string, Texture2D>();
        static Dictionary<CelestialBody, bool> hiddenBodies = new Dictionary<CelestialBody, bool>();

        void Start()
        {
            DontDestroyOnLoad(this);

            if (!initialized)
            {
                // Log version info
                var ainfoV = Attribute.GetCustomAttribute(typeof(WaypointManager).Assembly, typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
                Log.Info("WaypointManager " + ainfoV.InformationalVersion + " loading...");

                LoadTextures();
                LoadConfiguration();
                // LoadToolbar();
                SetupToolbar();
                GameEvents.onGameSceneLoadRequested.Add(new EventData<GameScenes>.OnEvent(OnGameSceneLoad));
                GameEvents.onHideUI.Add(new EventVoid.OnEvent(OnHideUI));
                GameEvents.onShowUI.Add(new EventVoid.OnEvent(OnShowUI));
                GameEvents.onPlanetariumTargetChanged.Add(new EventData<MapObject>.OnEvent(PlanetariumTargetChanged));

                Config.Load();

                Log.Info("WaypointManager " + ainfoV.InformationalVersion + " loaded.");

                Instance = this;
                initialized = true;
            }
            else
            {
                DestroyImmediate(this);
            }
        }

        void OnDestroy()
        {
            GameEvents.onHideUI.Remove(OnHideUI);
            GameEvents.onShowUI.Remove(OnShowUI);
            GameEvents.onPlanetariumTargetChanged.Remove(new EventData<MapObject>.OnEvent(PlanetariumTargetChanged));

            //
            //UnloadToolbar();
            TeardownToolbar();
            Config.Save();
        }

        private void LoadTextures()
        {
            Config.toolbarIcon = new Texture2D(2, 2);
            ToolbarControl.LoadImageFromFile(ref Config.toolbarIcon, "GameData/WaypointManager/PluginData/icons/toolbar");

            Config.addWaypointIcon = new Texture2D(2, 2);
            ToolbarControl.LoadImageFromFile(ref Config.addWaypointIcon, "GameData/WaypointManager/PluginData/icons/addWaypoint");
            Config.editWaypointIcon = new Texture2D(2, 2);
            ToolbarControl.LoadImageFromFile(ref Config.editWaypointIcon, "GameData/WaypointManager/PluginData/icons/editWaypoint");
            Config.deleteWaypointIcon = new Texture2D(2, 2);
            ToolbarControl.LoadImageFromFile(ref Config.deleteWaypointIcon, "GameData/WaypointManager/PluginData/icons/deleteWaypoint");
            Config.settingsIcon = new Texture2D(2, 2);
            ToolbarControl.LoadImageFromFile(ref Config.settingsIcon, "GameData/WaypointManager/PluginData/icons/settings");
            Config.closeIcon = new Texture2D(2, 2);
            ToolbarControl.LoadImageFromFile(ref Config.closeIcon, "GameData/WaypointManager/PluginData/icons/close");
        }

        internal const string MODID = "WaypointManager";
        internal const string MODNAME = "Waypoint Manager";

        private void SetupToolbar()
        {
            toolbarControl = gameObject.AddComponent<ToolbarControl>();
            toolbarControl.AddToAllToolbars(ToggleWindow, ToggleWindow,
                ApplicationLauncher.AppScenes.FLIGHT |
                ApplicationLauncher.AppScenes.MAPVIEW |
                ApplicationLauncher.AppScenes.TRACKSTATION,
                MODID,
                "waypointMgr",
                "WaypointManager/PluginData/icons/toolbar",
                "WaypointManager/PluginData/icons/toolbarSmall",
                MODNAME
            );
        }



        private void TeardownToolbar()
        {
            if (toolbarControl != null)
            {
                toolbarControl.OnDestroy();
                Destroy(toolbarControl);
            }

        }

        private void OnGameSceneLoad(GameScenes scene)
        {
            WaypointData.CacheWaypointData();
            if (scene != GameScenes.FLIGHT && scene != GameScenes.TRACKSTATION)
            {
                showGUI = false;
                showSettings = false;
                Config.Save();
            }
        }

        private void PlanetariumTargetChanged(MapObject mapObject)
        {
            CustomWaypointGUI.MapObject = mapObject;
        }

        /// <summary>
        /// Loads external config node data.
        /// </summary>
        void LoadConfiguration()
        {
            // Load all the celestial body configuration
            ConfigNode[] bodyConfig = GameDatabase.Instance.GetConfigNodes("WAYPOINT_MANAGER_BODIES");
            foreach (ConfigNode configNode in bodyConfig)
            {
                try
                {
                    string config = configNode.GetValue("name");

                    Log.Info("WaypointManager: Loading " + config + " icons.");
                    string url = configNode.GetValue("url");
                    if (url.Last() != '/')
                    {
                        url += '/';
                    }
                    if (Directory.Exists("GameData/" + url))
                    {

                        foreach (var str in Directory.GetFiles("GameData/" + url))
                        {
                            var icon = new Texture2D(2, 2);
                            ToolbarControl.LoadImageFromFile(ref icon, str);
                            //string name = icon.name.Substring(icon.name.LastIndexOf('/') + 1);
                            string name = Path.GetFileNameWithoutExtension(str);
                            bodyIcons[name] = icon;
                        }
                    }

#if false
                    foreach (GameDatabase.TextureInfo icon in GameDatabase.Instance.GetAllTexturesInFolder(url))
                    {
                        string name = icon.name.Substring(icon.name.LastIndexOf('/') + 1);
                        bodyIcons[name] = icon.texture;
                        Log.Info("WaypointManager: Loaded icon for " + name + ".");
                    }
#endif

                }
                catch (Exception e)
                {
                    Debug.LogError("WaypointManager: Exception when attempting to load Celestial Body configuration:");
                    Debug.LogException(e);
                }
            }

            // Extra stuff!
            GameDatabase.TextureInfo nyan = GameDatabase.Instance.databaseTexture.Where(t => t.name.Contains("WaypointManager/icons/Special/nyan")).FirstOrDefault();
            if (nyan != null && DateTime.Now.Month == 4 && DateTime.Now.Day == 1)
            {
                foreach (GameDatabase.TextureInfo texInfo in GameDatabase.Instance.databaseTexture.Where(t => t.name.StartsWith("Squad/Contracts/Icons/")))
                {
                    string name = texInfo.name.Replace("Squad/Contracts/Icons/", "");
                    if (!CustomWaypointGUI.forbiddenIcons.Contains(name))
                    {
                        texInfo.texture = nyan.texture;
                    }
                }
            }
        }

        private void ToggleWindow()
        {
            showGUI = !showGUI;
            if (!showGUI)
            {
                Config.Save();
            }
        }

        private void OnHideUI()
        {
            visible = false;
        }

        private void OnShowUI()
        {
            visible = true;
        }

        void OnGUI()
        {
            // Build the cache of waypoint data
            if (Event.current.type == EventType.Layout)
            {
                WaypointData.CacheWaypointData();
            }

            if (!stylesSetup)
            {
                // Set up the label style
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.padding = new RectOffset(0, 0, 0, 0);

                // Header buttons
                headerButtonStyle = new GUIStyle(GUI.skin.button);
                headerButtonStyle.alignment = TextAnchor.MiddleLeft;
                headerButtonStyle.clipping = TextClipping.Clip;

                // Headings
                headingStyle = new GUIStyle(GUI.skin.label);
                headingStyle.fontStyle = FontStyle.Bold;

                // Tooltips
                tipStyle = new GUIStyle(GUI.skin.box);
                tipStyle.wordWrap = true;
                tipStyle.stretchHeight = true;
                tipStyle.normal.textColor = Color.white;

                stylesSetup = true;
            }

            GUI.depth = 0;

            if (showGUI && visible && !ImportExport.helpDialogVisible)
            {
                var ainfoV = Attribute.GetCustomAttribute(GetType().Assembly, typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
                Config.mainWindowPos = ClickThruBlocker.GUILayoutWindow(
                    GetType().FullName.GetHashCode(),
                    Config.mainWindowPos,
                    WindowGUI,
                    "Waypoint Manager " + ainfoV.InformationalVersion);

                // Add the close icon
                if (GUI.Button(new Rect(Config.mainWindowPos.xMax - 18, Config.mainWindowPos.yMin + 2, 16, 16), Config.closeIcon, GUI.skin.label))
                {
                    showGUI = false;
                    HideSettings();
                }

                if (showSettings)
                {
                    // Default settings position
                    if (settingsPosition.xMin == settingsPosition.xMax)
                    {
                        settingsPosition = new Rect(Config.mainWindowPos.xMax + SETTINGS_WIDTH + 4 > Screen.width ?
                            Config.mainWindowPos.xMin - SETTINGS_WIDTH - 4 : Config.mainWindowPos.xMax, Config.mainWindowPos.yMin, SETTINGS_WIDTH + 4, 1);
                    }

                    settingsPosition = ClickThruBlocker.GUILayoutWindow(
                        GetType().FullName.GetHashCode() + 1,
                        settingsPosition,
                        SettingsGUI,
                        "Waypoint Manager Settings");

                    // Add the close icon
                    if (GUI.Button(new Rect(settingsPosition.xMax - 18, settingsPosition.yMin + 2, 16, 16), Config.closeIcon, GUI.skin.label))
                    {
                        HideSettings();
                    }
                }

                // Reset the position of the settings window
                if (!showSettings)
                {
                    settingsPosition.xMax = settingsPosition.xMin;
                }
            }

            // Display custom waypoint gui windows
            if (!ImportExport.helpDialogVisible)
                CustomWaypointGUI.OnGUI();

            // Draw any tooltips
            DrawToolTip();
        }

        protected void HideSettings()
        {
            Config.Save();
            showSettings = false;
        }

        protected void WindowGUI(int windowID)
        {
            GUILayout.BeginVertical(GUILayout.Width(GUI_WIDTH));

            // Output grouping selectors
            GUILayout.BeginHorizontal();
            GUILayout.Label("Group by: ", GUILayout.ExpandWidth(false));
            if (ContractSystem.Instance != null)
            {
                if (GUILayout.Toggle(Config.displayMode == Config.DisplayMode.CONTRACT, "Contract"))
                {
                    Config.displayMode = Config.DisplayMode.CONTRACT;
                }
            }
            else
            {
                Config.displayMode = Config.DisplayMode.CELESTIAL_BODY;
            }
            if (GUILayout.Toggle(Config.displayMode == Config.DisplayMode.CELESTIAL_BODY, "Celestial Body"))
            {
                Config.displayMode = Config.DisplayMode.CELESTIAL_BODY;
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent(Config.addWaypointIcon, "Create Custom Waypoint"), GUI.skin.label))
            {
                CustomWaypointGUI.AddWaypoint();
            }
            GUILayout.Space(4);


            if (GUILayout.Button(new GUIContent(Config.settingsIcon, "Settings"), GUI.skin.label))
            {
                showSettings = !showSettings;
                if (!showSettings)
                {
                    Config.Save();
                }
            }
            GUILayout.EndHorizontal();

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandWidth(true), GUILayout.Height(520));

            if (Config.displayMode == Config.DisplayMode.CONTRACT)
            {
                foreach (WaypointData.ContractContainer cc in WaypointData.ContractContainers)
                {
                    if (GUILayout.Button(cc.title, headerButtonStyle, GUILayout.MaxWidth(GUI_WIDTH - 24.0f)))
                    {
                        cc.hidden = !cc.hidden;
                    }

                    if (!cc.hidden)
                    {
                        foreach (WaypointData wpd in cc.waypointByContract.OrderBy(wp => wp.waypoint.name + wp.waypoint.index))
                        {
                            WaypointLineGUI(wpd);
                        }
                    }
                }
            }
            else
            {
                foreach (KeyValuePair<CelestialBody, List<WaypointData>> pair in WaypointData.WaypointByBody)
                {
                    CelestialBody b = pair.Key;
                    bool hidden = hiddenBodies.ContainsKey(b) && hiddenBodies[b];
                    if (GUILayout.Button(b.bodyName, headerButtonStyle, GUILayout.MaxWidth(GUI_WIDTH - 24.0f)))
                    {
                        hidden = !hidden;
                        hiddenBodies[b] = hidden;
                    }

                    if (!hidden)
                    {
                        foreach (WaypointData wpd in pair.Value.OrderBy(wp => wp.waypoint.name + wp.waypoint.index))
                        {
                            WaypointLineGUI(wpd);
                        }
                    }
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUI.DragWindow();

            SetToolTip(0);
        }

        protected void WaypointLineGUI(WaypointData wpd)
        {
            if (!wpd.waypoint.visible)
            {
                return;
            }

            GUILayout.BeginHorizontal(GUILayout.Height(32));

            // Contract icon
            GUILayout.Label(ContractIcon(wpd), GUILayout.ExpandWidth(false), GUILayout.Height(38), GUILayout.Width(38));
            GUILayout.Space(2);

            // Celestial body icon
            GUILayout.Label(CelestialBodyIcon(wpd.celestialBody.name), GUILayout.ExpandWidth(false));
            GUILayout.Space(2);

            GUILayout.BeginVertical();

            // Waypoint name, distance
            GUILayout.BeginHorizontal();
            string name = wpd.waypoint.name;
            if (wpd.waypoint.isClustered)
            {
                name += " " + StringUtilities.IntegerToGreek(wpd.waypoint.index);
            }
            GUILayout.Label(name, labelStyle, GUILayout.Height(16), GUILayout.Width(GUI_WIDTH - 240), GUILayout.ExpandWidth(false));
            if (FlightGlobals.currentMainBody == wpd.celestialBody)
            {
                GUILayout.Label("Dist: " + Util.PrintDistance(wpd), labelStyle, GUILayout.Height(16), GUILayout.ExpandWidth(false));
            }
            GUILayout.EndHorizontal();

            // Waypoint location
            GUILayout.BeginHorizontal();
            GUILayout.Label("Lat: " + Util.FormatCoordinate(wpd.waypoint.latitude, true), labelStyle, GUILayout.Height(16), GUILayout.Width(GUI_WIDTH / 2.0f - 72.0f), GUILayout.ExpandWidth(false));
            GUILayout.Label("Lon: " + Util.FormatCoordinate(wpd.waypoint.longitude, false), labelStyle, GUILayout.Height(16), GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();

            if (CustomWaypoints.Instance.IsCustom(wpd.waypoint))
            {
                GUILayout.BeginVertical();
                GUILayout.Space(8);
                GUILayout.BeginHorizontal();

                if (GUILayout.Button(new GUIContent(Config.editWaypointIcon, "Edit Waypoint"), GUI.skin.label))
                {
                    CustomWaypointGUI.EditWaypoint(wpd.waypoint);
                }
                if (GUILayout.Button(new GUIContent(Config.deleteWaypointIcon, "Delete Waypoint"), GUI.skin.label))
                {
                    CustomWaypointGUI.DeleteWaypoint(wpd.waypoint);
                }

                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            else
            {
#if false
                if (GUILayout.Button(new GUIContent(Config.editWaypointIcon, "Edit Stock Waypoint"), GUI.skin.label))
                {
                    CustomWaypointGUI.EditWaypoint(wpd.waypoint, true);
                }
#endif
                if (GUILayout.Button(new GUIContent(Config.deleteWaypointIcon, "Hide Stock Waypoint"), GUI.skin.label))
                {
                    CustomWaypointGUI.DeleteWaypoint(wpd.waypoint);
                }

            }

            // Active waypoint toggle
            GUILayout.BeginVertical();
            GUILayout.Space(8);
            bool isNavPoint = Util.IsNavPoint(wpd.waypoint);
            if (GUILayout.Toggle(isNavPoint, (string)null) != isNavPoint)
            {
                if (isNavPoint)
                {
                    NavWaypoint.fetch.Clear();
                    NavWaypoint.fetch.Deactivate();
                }
                else
                {
                    NavWaypoint.fetch.Setup(wpd.waypoint);
                    NavWaypoint.fetch.Activate();
                }
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(6);
        }

        protected GUIContent CelestialBodyIcon(string celestialBodyName)
        {
            if (bodyIcons.ContainsKey(celestialBodyName))
            {
                return new GUIContent(bodyIcons[celestialBodyName], celestialBodyName);
            }
            else
            {
                return new GUIContent(celestialBodyName + " ", celestialBodyName);
            }
        }

        protected GUIContent ContractIcon(WaypointData wpd)
        {
            Texture2D texture = Util.GetContractIcon(wpd.waypoint.id, wpd.waypoint.seed);
            return new GUIContent(texture, wpd.waypoint.contractReference != null ? wpd.waypoint.contractReference.Title : "No contract");
        }

        protected void SettingsGUI(int windowID)
        {
            GUILayout.BeginVertical(GUILayout.Width(SETTINGS_WIDTH));

            // Distance calculation method
            GUILayout.Label("Distance calculation", headingStyle);
            GUIContent guiContent = new GUIContent("Lateral", "Calculates distance as the horizontal distance only.  Useful if you're looking to hit a landing spot exactly.");
            if (GUILayout.Toggle(Config.distanceCalcMethod == Config.DistanceCalcMethod.LATERAL, guiContent))
            {
                Config.distanceCalcMethod = Config.DistanceCalcMethod.LATERAL;
            }
            guiContent = new GUIContent("Straight line", "Calculates distance in a direct line.");
            if (GUILayout.Toggle(Config.distanceCalcMethod == Config.DistanceCalcMethod.STRAIGHT_LINE, guiContent))
            {
                Config.distanceCalcMethod = Config.DistanceCalcMethod.STRAIGHT_LINE;
            }
            guiContent = new GUIContent("Compromise", "Uses lateral distance if the vessel and waypoint altitude are relatively close, otherwise uses straight line distance.");
            if (GUILayout.Toggle(Config.distanceCalcMethod == Config.DistanceCalcMethod.COMPROMISE, guiContent))
            {
                Config.distanceCalcMethod = Config.DistanceCalcMethod.COMPROMISE;
            }

            // In-Flight Waypoints :)
            GUILayout.Label("Waypoints to display in-flight", headingStyle);
            guiContent = new GUIContent("All", "Display all waypoints on the given celestial body while in flight.");
            if (GUILayout.Toggle(Config.waypointDisplay == Config.WaypointDisplay.ALL, guiContent))
            {
                Config.waypointDisplay = Config.WaypointDisplay.ALL;
            }
            guiContent = new GUIContent("Active", "Display only the active waypoint while in flight.");
            if (GUILayout.Toggle(Config.waypointDisplay == Config.WaypointDisplay.ACTIVE, guiContent))
            {
                Config.waypointDisplay = Config.WaypointDisplay.ACTIVE;
            }
            guiContent = new GUIContent("None", "Do not display any waypoints while in flight.");
            if (GUILayout.Toggle(Config.waypointDisplay == Config.WaypointDisplay.NONE, guiContent))
            {
                Config.waypointDisplay = Config.WaypointDisplay.NONE;
            }

            // HUD
            GUILayout.Label("Values to display below altimeter", headingStyle);
            if (GUILayout.Toggle(Config.hudDistance, "Distance to target") != Config.hudDistance)
            {
                Config.hudDistance = !Config.hudDistance;
            }
            if (GUILayout.Toggle(Config.hudTime, "Time to target") != Config.hudTime)
            {
                Config.hudTime = !Config.hudTime;
            }
            if (GUILayout.Toggle(Config.hudHeading, "Heading to target") != Config.hudHeading)
            {
                Config.hudHeading = !Config.hudHeading;
            }
            if (GUILayout.Toggle(Config.hudAngle, "Glide slope angles") != Config.hudAngle)
            {
                Config.hudAngle = !Config.hudAngle;
            }

            // Display style
            GUILayout.Label("Location display style", headingStyle);
            if (GUILayout.Toggle(!Config.displayDecimal, "Degrees/Minutes/Seconds") == Config.displayDecimal)
            {
                Config.displayDecimal = false;
            }
            if (GUILayout.Toggle(Config.displayDecimal, "Decimal") != Config.displayDecimal)
            {
                Config.displayDecimal = true;
            }

            // Opacity
            GUILayout.Label("Waypoint Opacity", headingStyle);
            GUILayout.BeginHorizontal();
            Config.opacity = GUILayout.HorizontalSlider(Config.opacity, 0.3f, 1.0f);
            GUILayout.Space(5);
            if (GUILayout.Button("Reset", GUILayout.Width(50)))
                Config.opacity = 1.0f;
            GUILayout.EndHorizontal();
            if (GUILayout.Button(new GUIContent("Export Custom Waypoints", "Exports the custom waypoints to GameData/WaypointManager/CustomWaypoints.cfg")))
            {
                CustomWaypoints.Export();
            }
            if (importExportWindow == null)
            {
                if (GUILayout.Button(new GUIContent("Import Custom Waypoints", "Imports the custom waypoints from GameData/WaypointManager/CustomWaypoints.cfg")))
                {
                    if (importExportWindow == null)
                        importExportWindow = gameObject.AddComponent<ImportExport>();
                    //CustomWaypoints.Import();
                }
            }
            else
            {
                if (GUILayout.Button(new GUIContent("Cancel Import of Custom Waypoints", "Cancels the import of custom waypoints from GameData/WaypointManager/CustomWaypoints.cfg")))
                    Destroy(importExportWindow);
            }

            GUILayout.Label("UI Scaling (" + (Config.scaling*100).ToString("F0") + "%)", headingStyle);
            GUILayout.BeginHorizontal();
            Config.scaling = GUILayout.HorizontalSlider(Config.scaling, 0.8f, 1.5f);
            GUILayout.Space(5);
            if (GUILayout.Button("Reset", GUILayout.Width(50)))
                Config.scaling = 1.0f;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUI.DragWindow();

            SetToolTip(1);
        }
        internal static MonoBehaviour importExportWindow = null;

        /// <summary>
        /// Set the current tooltip
        /// </summary>
        public void SetToolTip(int windowID)
        {
            while (toolTipTime.Count < windowID + 1)
            {
                toolTipTime.Add(0.0);
                toolTip.Add("");
            }

            if (Event.current.type == EventType.Repaint && GUI.tooltip != toolTip[windowID])
            {
                toolTipTime[windowID] = Time.fixedTime;
                toolTip[windowID] = GUI.tooltip;
            }
        }

        /// <summary>
        /// Draw tool tips.
        /// </summary>
        private void DrawToolTip()
        {
            for (int i = 0; i < toolTipTime.Count; i++)
            {
                if (!string.IsNullOrEmpty(toolTip[i]))
                {
                    if (Time.fixedTime > toolTipTime[i] + 0.5)
                    {
                        GUIContent tip = new GUIContent(toolTip[i]);

                        Vector2 textDimensions = GUI.skin.box.CalcSize(tip);
                        if (textDimensions.x > 240)
                        {
                            textDimensions.x = 240;
                            textDimensions.y = tipStyle.CalcHeight(tip, 240);
                        }
                        tooltipPosition.width = textDimensions.x;
                        tooltipPosition.height = textDimensions.y;
                        tooltipPosition.x = Event.current.mousePosition.x + tooltipPosition.width > Screen.width ?
                            Screen.width - tooltipPosition.width : Event.current.mousePosition.x;
                        tooltipPosition.y = Event.current.mousePosition.y + 20;

                        GUI.Label(tooltipPosition, tip, tipStyle);
                    }
                }
            }
        }
    }
}
