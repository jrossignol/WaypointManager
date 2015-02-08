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
        private const float SETTINGS_WIDTH = 240;

        private ApplicationLauncherButton launcherButton = null;
        private bool initialized = false;
        private bool showGUI = false;
        private bool showSettings = false;
        private bool visible = true;
        private bool stylesSetup = false;

        private GUIStyle headerButtonStyle;
        private GUIStyle labelStyle;
        private GUIStyle headingStyle;
        private GUIStyle tipStyle;

        private Vector2 scrollPosition;
        private Rect settingsPosition;

        private Rect tooltipPosition;
        private string toolTip;
        private double toolTipTime;

        static Texture2D toolbarIcon;
        static Texture2D settingsIcon;
        static Texture2D closeIcon;

        static Dictionary<string, Texture2D> bodyIcons = new Dictionary<string, Texture2D>();
        static Dictionary<string, Dictionary<Color, Texture2D>> contractIcons = new Dictionary<string, Dictionary<Color, Texture2D>>();
        static Dictionary<Contract, bool> hiddenContracts = new Dictionary<Contract, bool>();
        static Dictionary<CelestialBody, bool> hiddenBodies = new Dictionary<CelestialBody, bool>();

        void Start()
        {
            DontDestroyOnLoad(this);

            if (!initialized)
            {
                LoadTextures();
                LoadConfiguration();

                GameEvents.onGUIApplicationLauncherReady.Add(new EventVoid.OnEvent(SetupToolbar));
                GameEvents.onGUIApplicationLauncherUnreadifying.Add(new EventData<GameScenes>.OnEvent(TeardownToolbar));
                GameEvents.onGameSceneLoadRequested.Add(new EventData<GameScenes>.OnEvent(OnGameSceneLoad));
                GameEvents.onHideUI.Add(new EventVoid.OnEvent(OnHideUI));
                GameEvents.onShowUI.Add(new EventVoid.OnEvent(OnShowUI));

                Config.Load();

                initialized = true;
            }
        }

        void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(new EventVoid.OnEvent(SetupToolbar));
            GameEvents.onGUIApplicationLauncherUnreadifying.Remove(new EventData<GameScenes>.OnEvent(TeardownToolbar));
            GameEvents.onHideUI.Remove(OnHideUI);
            GameEvents.onShowUI.Remove(OnShowUI);

            Config.Save();
        }

        private void LoadTextures()
        {
            toolbarIcon = GameDatabase.Instance.GetTexture("WaypointManager/icons/toolbar", false);
            settingsIcon = GameDatabase.Instance.GetTexture("WaypointManager/icons/settings", false);
            closeIcon = GameDatabase.Instance.GetTexture("WaypointManager/icons/close", false);
        }

        private void SetupToolbar()
        {
            if (launcherButton == null)
            {
                ApplicationLauncher.AppScenes visibleScenes = ApplicationLauncher.AppScenes.FLIGHT |
                    ApplicationLauncher.AppScenes.MAPVIEW |
                    ApplicationLauncher.AppScenes.TRACKSTATION;
                launcherButton = ApplicationLauncher.Instance.AddModApplication(ToggleWindow, ToggleWindow, null, null, null, null,
                    visibleScenes, toolbarIcon);
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

        private void OnGameSceneLoad(GameScenes scene)
        {
            if (scene != GameScenes.FLIGHT && scene != GameScenes.TRACKSTATION)
            {
                showGUI = false;
                showSettings = false;
                Config.Save();
            }
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
            if (showGUI && visible)
            {
                GUI.depth = 1;
                var ainfoV = Attribute.GetCustomAttribute(GetType().Assembly, typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
                Config.mainWindowPos = GUILayout.Window(
                    GetType().FullName.GetHashCode(),
                    Config.mainWindowPos,
                    WindowGUI,
                    "Waypoint Manager " + ainfoV.InformationalVersion);

                // Add the close icon
                GUI.depth = 0;
                if (GUI.Button(new Rect(Config.mainWindowPos.xMax - 18, Config.mainWindowPos.yMin + 2, 16, 16), closeIcon, GUI.skin.label))
                {
                    showGUI = false;
                    HideSettings();
                }

                if (showSettings)
                {
                    // Default settings position
                    if (settingsPosition.xMin == settingsPosition.xMax)
                    {
                        settingsPosition = new Rect(Config.mainWindowPos.xMax + SETTINGS_WIDTH > Screen.width ?
                            Config.mainWindowPos.xMin - SETTINGS_WIDTH : Config.mainWindowPos.xMax, Config.mainWindowPos.yMin, SETTINGS_WIDTH, 1);
                    }

                    GUI.depth = 1;
                    settingsPosition = GUILayout.Window(
                        GetType().FullName.GetHashCode() + 1,
                        settingsPosition,
                        SettingsGUI,
                        "Waypoint Manager Settings");

                    // Add the close icon
                    GUI.depth = 0;
                    if (GUI.Button(new Rect(settingsPosition.xMax - 18, settingsPosition.yMin + 2, 16, 16), closeIcon, GUI.skin.label))
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
        }

        protected void HideSettings()
        {
            Config.Save();
            showSettings = false;
        }

        protected void WindowGUI(int windowID)
        {
            // Build the cache of waypoint data
            if (Event.current.type == EventType.Layout)
            {
                WaypointData.CacheWaypointData();
            }

            GUILayout.BeginVertical(GUILayout.Width(300));

            if (!stylesSetup)
            {
                // Set up the label style
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.padding = new RectOffset(0, 0, 0, 0);

                // Header buttons
                headerButtonStyle = new GUIStyle(GUI.skin.button);
                headerButtonStyle.alignment = TextAnchor.MiddleLeft;

                // Headings
                headingStyle = new GUIStyle(GUI.skin.label);
                headingStyle.fontStyle = FontStyle.Bold;

                // Tooltips
                tipStyle = new GUIStyle(GUI.skin.box);
                tipStyle.wordWrap = true;
                tipStyle.stretchHeight = true;

                stylesSetup = true;
            }

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
            if (GUILayout.Button(settingsIcon, GUI.skin.label))
            {
                showSettings = !showSettings;
                if (!showSettings)
                {
                    Config.Save();
                }
            }
            GUILayout.EndHorizontal();

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(480));

            if (Config.displayMode == Config.DisplayMode.CONTRACT)
            {
                foreach (KeyValuePair<Contract, List<WaypointData>> pair in WaypointData.WaypointByContracts)
                {
                    Contract c = pair.Key;
                    bool hidden = hiddenContracts.ContainsKey(c) && hiddenContracts[c];
                    if (GUILayout.Button(c.Title, headerButtonStyle))
                    {
                        hidden = !hidden;
                        hiddenContracts[c] = hidden;
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
            else
            {
                foreach (KeyValuePair<CelestialBody, List<WaypointData>> pair in WaypointData.WaypointByBody)
                {
                    CelestialBody b = pair.Key;
                    bool hidden = hiddenBodies.ContainsKey(b) && hiddenBodies[b];
                    if (GUILayout.Button(b.name, headerButtonStyle))
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

            DrawToolTip();
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

            // Active waypoint toggle
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
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
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            DrawToolTip();
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
            // Check cache for texture
            Texture2D texture;
            Color color = SystemUtilities.RandomColor(wpd.waypoint.seed, 1.0f, 1.0f, 1.0f);
            if (!contractIcons.ContainsKey(wpd.waypoint.id))
            {
                contractIcons[wpd.waypoint.id] = new Dictionary<Color, Texture2D>();
            }
            if (!contractIcons[wpd.waypoint.id].ContainsKey(color))
            {
                Texture2D baseTexture = ContractDefs.textures[wpd.waypoint.id];
                texture = new Texture2D(baseTexture.width, baseTexture.height, TextureFormat.RGBA32, false);
                string path = (wpd.waypoint.id.Contains('/') ? "GameData/" : "GameData/Squad/Contracts/Icons/") + wpd.waypoint.id + ".png";
                texture.LoadImage(File.ReadAllBytes(path.Replace('/', '\\')));

                Color[] pixels = texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] *= color;
                }
                texture.SetPixels(pixels);
                texture.Compress(true);
                contractIcons[wpd.waypoint.id][color] = texture;
            }
            else
            {
                texture = contractIcons[wpd.waypoint.id][color];
            }

            return new GUIContent(texture, wpd.waypoint.contractReference.Title);
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

            // TODO - tool bars

            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        /// <summary>
        /// Draw tool tips.
        /// </summary>
        private void DrawToolTip()
        {
            if (!string.IsNullOrEmpty(GUI.tooltip))
            {
                if (Time.fixedTime > toolTipTime + 0.5)
                {
                    GUIContent tip = new GUIContent(GUI.tooltip);
                    GUI.depth = 0;

                    Vector2 textDimensions = GUI.skin.box.CalcSize(tip);
                    if (textDimensions.x > 180)
                    {
                        textDimensions.x = 180;
                        textDimensions.y = tipStyle.CalcHeight(tip, 180);
                    }
                    tooltipPosition.width = textDimensions.x;
                    tooltipPosition.height = textDimensions.y;
                    tooltipPosition.x = Event.current.mousePosition.x + tooltipPosition.width > Screen.width ?
                        Screen.width - tooltipPosition.width : Event.current.mousePosition.x;
                    tooltipPosition.y = Event.current.mousePosition.y + 20;

                    GUI.Label(tooltipPosition, tip, tipStyle);
                }
            }

            if (Event.current.type == EventType.Repaint && GUI.tooltip != toolTip)
            {
                toolTipTime = Time.fixedTime;
                toolTip = GUI.tooltip;
            }
        }
    }
}
