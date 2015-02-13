using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using KSP;
using Contracts;
using FinePrint;
using FinePrint.Utilities;

namespace WaypointManager
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    class WaypointManager : MonoBehaviour
    {
        private const float GUI_WIDTH = 360;
        private const float SETTINGS_WIDTH = 280;

        public static WaypointManager Instance;

        private ApplicationLauncherButton launcherButton = null;
        private IButton toolbarButton;

        private bool initialized = false;
        public bool showGUI = false;
        private bool showSettings = false;
        public bool visible = true;
        private bool stylesSetup = false;

        private GUIStyle headerButtonStyle;
        private GUIStyle labelStyle;
        private GUIStyle headingStyle;
        private GUIStyle tipStyle;

        private Vector2 scrollPosition;
        private Rect settingsPosition;

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
                LoadTextures();
                LoadConfiguration();
                LoadToolbar();

                GameEvents.onGUIApplicationLauncherReady.Add(new EventVoid.OnEvent(SetupToolbar));
                GameEvents.onGUIApplicationLauncherUnreadifying.Add(new EventData<GameScenes>.OnEvent(TeardownToolbar));
                GameEvents.onGameSceneLoadRequested.Add(new EventData<GameScenes>.OnEvent(OnGameSceneLoad));
                GameEvents.onHideUI.Add(new EventVoid.OnEvent(OnHideUI));
                GameEvents.onShowUI.Add(new EventVoid.OnEvent(OnShowUI));
                GameEvents.onPlanetariumTargetChanged.Add(new EventData<MapObject>.OnEvent(PlanetariumTargetChanged));

                Config.Load();

                Instance = this;
                initialized = true;
            }
        }

        void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(new EventVoid.OnEvent(SetupToolbar));
            GameEvents.onGUIApplicationLauncherUnreadifying.Remove(new EventData<GameScenes>.OnEvent(TeardownToolbar));
            GameEvents.onHideUI.Remove(OnHideUI);
            GameEvents.onShowUI.Remove(OnShowUI);
            GameEvents.onPlanetariumTargetChanged.Remove(new EventData<MapObject>.OnEvent(PlanetariumTargetChanged));

            UnloadToolbar();

            Config.Save();
        }

        private void LoadTextures()
        {
            Config.toolbarIcon = GameDatabase.Instance.GetTexture("WaypointManager/icons/toolbar", false);
            Config.addWaypointIcon = GameDatabase.Instance.GetTexture("WaypointManager/icons/addWaypoint", false);
            Config.editWaypointIcon = GameDatabase.Instance.GetTexture("WaypointManager/icons/editWaypoint", false);
            Config.deleteWaypointIcon = GameDatabase.Instance.GetTexture("WaypointManager/icons/deleteWaypoint", false);
            Config.settingsIcon = GameDatabase.Instance.GetTexture("WaypointManager/icons/settings", false);
            Config.closeIcon = GameDatabase.Instance.GetTexture("WaypointManager/icons/close", false);
        }

        private void SetupToolbar()
        {
            if (launcherButton == null && Config.useStockToolbar)
            {
                ApplicationLauncher.AppScenes visibleScenes = ApplicationLauncher.AppScenes.FLIGHT |
                    ApplicationLauncher.AppScenes.MAPVIEW |
                    ApplicationLauncher.AppScenes.TRACKSTATION;
                launcherButton = ApplicationLauncher.Instance.AddModApplication(ToggleWindow, ToggleWindow, null, null, null, null,
                    visibleScenes, Config.toolbarIcon);
            }
        }

        private void TeardownToolbar(GameScenes scene)
        {
            if (launcherButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(launcherButton);
                launcherButton = null;
            }
        }

        private void LoadToolbar()
        {
            if (ToolbarManager.ToolbarAvailable)
            {
                toolbarButton = ToolbarManager.Instance.add("WaypointManager", "button");
                toolbarButton.TexturePath = "WaypointManager/icons/toolbarSmall";
                toolbarButton.ToolTip = "Waypoint Manager";
                toolbarButton.Visibility = new GameScenesVisibility(GameScenes.FLIGHT, GameScenes.TRACKSTATION);
                toolbarButton.OnClick += (e) =>
                {
                    ToggleWindow();
                };
            }
        }

        private void UnloadToolbar()
        {
            toolbarButton.Destroy();
            toolbarButton = null;
        }

        private void OnGameSceneLoad(GameScenes scene)
        {
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

                    Debug.Log("WaypointManager: Loading " + config +" icons.");
                    string url = configNode.GetValue("url");
                    if (url.Last() != '/')
                    {
                        url += '/';
                    }
                    foreach (GameDatabase.TextureInfo icon in GameDatabase.Instance.GetAllTexturesInFolder(url))
                    {
                        string name = icon.name.Substring(icon.name.LastIndexOf('/') + 1);
                        bodyIcons[name] = icon.texture;
                        Debug.Log("WaypointManager: Loaded icon for " + name + ".");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("WaypointManager: Exception when attempting to load Celestial Body configuration:");
                    Debug.LogException(e);
                }
            }

            // Get all the directories for icons
            ConfigNode[] iconConfig = GameDatabase.Instance.GetConfigNodes("WAYPOINT_MANAGER_ICONS");
            foreach (ConfigNode configNode in iconConfig)
            {
                string dir = configNode.GetValue("url");

                // The FinePrint logic is such that it will only look in Squad/Contracts/Icons for icons.
                // Cheat this by hacking the path in the game database.
                foreach (GameDatabase.TextureInfo texInfo in GameDatabase.Instance.databaseTexture.Where(t => t.name.StartsWith(dir)))
                {
                    texInfo.name = "Squad/Contracts/Icons/" + texInfo.name;
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

            if (showGUI && visible)
            {
                var ainfoV = Attribute.GetCustomAttribute(GetType().Assembly, typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
                Config.mainWindowPos = GUILayout.Window(
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
                            Config.mainWindowPos.xMin - SETTINGS_WIDTH - 4: Config.mainWindowPos.xMax, Config.mainWindowPos.yMin, SETTINGS_WIDTH + 4, 1);
                    }

                    settingsPosition = GUILayout.Window(
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
            if (GUILayout.Toggle(Config.displayMode == Config.DisplayMode.CONTRACT, "Contract"))
            {
                Config.displayMode = Config.DisplayMode.CONTRACT;
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
                foreach (WaypointData.ContractContainer cc in WaypointData.WaypointByContracts)
                {
                    Contract c = cc.contract;
                    string title = (c != null ? c.Title : "No contract");
                    if (GUILayout.Button(title, headerButtonStyle, GUILayout.MaxWidth(GUI_WIDTH - 24.0f)))
                    {
                        cc.hidden = !cc.hidden;
                    }

                    if (!cc.hidden)
                    {
                        foreach (WaypointData wpd in cc.waypointByContract)
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
                    if (GUILayout.Button(b.name, headerButtonStyle, GUILayout.MaxWidth(GUI_WIDTH - 24.0f)))
                    {
                        hidden = !hidden;
                        hiddenBodies[b] = hidden;
                    }

                    if (!hidden)
                    {
                        foreach (WaypointData wpd in pair.Value)
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
            GUILayout.BeginHorizontal(GUILayout.Height(32));

            // Contract icon
            GUILayout.Label(ContractIcon(wpd), GUILayout.ExpandWidth(false));
            GUILayout.Space(2);
            
            // Celestial body icon
            GUILayout.Label(CelestialBodyIcon(wpd.celestialBody.name), GUILayout.ExpandWidth(false));
            GUILayout.Space(2);

            GUILayout.BeginVertical();

            // Waypoint name
            GUILayout.Label(wpd.waypoint.name, labelStyle, GUILayout.Height(16), GUILayout.ExpandWidth(false));

            // Waypoint distance
            GUILayout.Label("Distance: " + Util.PrintDistance(wpd), labelStyle, GUILayout.Height(16), GUILayout.ExpandWidth(false));

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

            // Active waypoint toggle
            GUILayout.BeginVertical();
            GUILayout.Space(8);
            bool isNavPoint = Util.IsNavPoint(wpd.waypoint);
            if (GUILayout.Toggle(isNavPoint, (string)null) != isNavPoint)
            {
                if (isNavPoint)
                {
                    FinePrint.WaypointManager.clearNavPoint();
                }
                else
                {
                    FinePrint.WaypointManager.setupNavPoint(wpd.waypoint);
                    FinePrint.WaypointManager.activateNavPoint();
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

            // Toolbar
            GUILayout.Label("Toolbar Display", headingStyle);
            if (GUILayout.Toggle(Config.useStockToolbar, "Show icon in stock toolbar") != Config.useStockToolbar)
            {
                Config.useStockToolbar = !Config.useStockToolbar;
                if (Config.useStockToolbar)
                {
                    SetupToolbar();
                }
                else
                {
                    TeardownToolbar(GameScenes.FLIGHT);
                }
            }

            GUILayout.EndVertical();

            GUI.DragWindow();

            SetToolTip(1);
        }

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
