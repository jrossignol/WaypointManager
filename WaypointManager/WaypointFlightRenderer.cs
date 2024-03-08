using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using KSP.UI.Screens;
using Contracts;
using FinePrint;
using FinePrint.Utilities;
using ClickThroughFix;
using static WaypointManager.RegisterToolbar;
using static FinePrint.ContractDefs;

namespace WaypointManager
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    class WaypointFlightRenderer : MonoBehaviour
    {
        private static GUIStyle nameStyle = null;
        private static GUIStyle valueStyle = null;
        private static GUIStyle hintTextStyle = null;

        private bool visible = true;
        private Waypoint selectedWaypoint = null;
        private string waypointName = "";
        private Rect windowPos;
        private bool newClick = false;
        private AltimeterSliderButtons asb = null;
        private RectTransform asbRectTransform = null;

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

            GameEvents.onGameSceneLoadRequested.Add(new EventData<GameScenes>.OnEvent(OnGameSceneLoadRequested));
            GameEvents.onHideUI.Add(new EventVoid.OnEvent(OnHideUI));
            GameEvents.onShowUI.Add(new EventVoid.OnEvent(OnShowUI));
        }

        protected void OnDestroy()
        {
            GameEvents.onGameSceneLoadRequested.Remove(new EventData<GameScenes>.OnEvent(OnGameSceneLoadRequested));
            GameEvents.onHideUI.Remove(OnHideUI);
            GameEvents.onShowUI.Remove(OnShowUI);
        }

        public void OnGameSceneLoadRequested(GameScenes gameScene)
        {
            asb = null;
        }

        public void OnHideUI()
        {
            visible = false;
        }

        public void OnShowUI()
        {
            visible = true;
        }

        new bool enabled = false;

        bool drag = false;
        float offset_x;
        float offset_y;

        void OnMouseDown()
        {
            if (!drag)
            {
                offset_x = Input.mousePosition.x - Config.displayBox.x;
                offset_y = (Screen.height - Input.mousePosition.y) - Config.displayBox.y;
            }
        }

        void OnMouseUp()
        {
            Config.Save();
        }

        void Update()
        {
            if (drag)
            {
                Config.boxLeft = Math.Max(0, Input.mousePosition.x - offset_x);
                Config.boxLeft = Math.Min(Config.boxLeft, Screen.width - boxWidth);

                Config.boxTop = Math.Max(0, (Screen.height - Input.mousePosition.y) - offset_y);
                Config.boxTop = Math.Min(Config.boxTop, Screen.height - fullBoxHeight);
            }
            if (drag || Config.displayBox.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
            {
                bool b = Input.GetMouseButton(0) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));

                if (!drag && b)
                {
                    OnMouseDown();
                }
                else
                    if (drag && !b)
                    OnMouseUp();
                drag = b;

            }
            else drag = false;
        }

        public void OnGUI()
        {
            if (visible && !ImportExport.helpDialogVisible)
            {
                if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
                {
                    newClick = true;
                }

                if (HighLogic.LoadedScene == GameScenes.FLIGHT || HighLogic.LoadedScene == GameScenes.TRACKSTATION)
                {
                    // Draw the marker for custom waypoints that are currently being created
                    CustomWaypointGUI.DrawMarker();
                }

                if (HighLogic.LoadedSceneIsFlight && !MapView.MapIsEnabled)
                {
                    SetupStyles();

                    WaypointData.CacheWaypointData();

                    foreach (WaypointData wpd in WaypointData.Waypoints)
                    {
                        DrawWaypoint(wpd);
                    }
                }
                else cnt = 0;

                if (HighLogic.LoadedSceneIsFlight && (!MapView.MapIsEnabled || ContractSystem.Instance == null))
                {
                    ShowNavigationWindow();
                }
            }
        }

        static float OldUIScale = 0;
        static float oldScaling = 0;
        static float finalScaling;

        static float boxHeight;

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];

            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }

        // cnt is needed becuase KSP/Unity is stupid and loses the background texture between scenes
        // This makes sure that at least 2 cycles are done before it stops recreationg the texture
        static int cnt = 0;

        // Styles taken directly from Kerbal Engineer Redux - because they look great and this will
        // make our display consistent with that
        internal static void SetupStyles(bool forceUpdate = false)
        {
            if (OldUIScale == GameSettings.UI_SCALE && oldScaling == Config.scaling && !forceUpdate)
            {
                if (cnt++ >1)
                    return;
            }
            OldUIScale = GameSettings.UI_SCALE;
            oldScaling = Config.scaling;

            finalScaling = GameSettings.UI_SCALE * Config.scaling;

            boxHeight = 20f * finalScaling;
            Texture2D backgroundTexture = MakeTex(2, 2, new Color(Config.backgroundGrey, Config.backgroundGrey, Config.backgroundGrey, Config.displayOpacity));
            nameStyle = new GUIStyle(HighLogic.Skin.label)
            {
                normal =
                {
                    textColor = Color.white,
                    background = backgroundTexture
                },
                margin = new RectOffset(),
                padding = new RectOffset(5, 0, 0, 0),
                alignment = TextAnchor.MiddleLeft,
                fontSize = (int)(11f * finalScaling),
                fontStyle = FontStyle.Bold,
                fixedHeight = 20.0f * finalScaling
            };

            valueStyle = new GUIStyle(HighLogic.Skin.label)
            {
                normal =
                {
                    textColor = Color.green,
                    background = backgroundTexture
                },
                margin = new RectOffset(),
                padding = new RectOffset(0, 5, 0, 0),
                alignment = TextAnchor.MiddleRight,
                fontSize = (int)(11f * finalScaling),
                fontStyle = FontStyle.Normal,
                fixedHeight = 20.0f * finalScaling
            };

            hintTextStyle = new GUIStyle(HighLogic.Skin.box)
            {
                normal =
                {
                    textColor = Color.white,
                    background = backgroundTexture
                },
                padding = new RectOffset(4, 4, 7, 4),
                font = HighLogic.Skin.box.font,
                fontSize = (int)(13 * finalScaling),
                fontStyle = FontStyle.Normal,
                fixedWidth = 0,
                fixedHeight = 0,
                stretchHeight = true,
                stretchWidth = true
            };
        }


        protected void DrawWaypoint(WaypointData wpd)
        {
            // Not our planet
            CelestialBody celestialBody = FlightGlobals.currentMainBody;
            if (celestialBody == null || wpd.waypoint.celestialName != celestialBody.name)
            {
                return;
            }

            // Check if the waypoint should be visible
            if (!wpd.waypoint.visible)
            {
                return;
            }

            // Figure out waypoint label
            string label = wpd.waypoint.name + (wpd.waypoint.isClustered ? (" " + StringUtilities.IntegerToGreek(wpd.waypoint.index)) : "");

            // Set the alpha and do a nice fade
            wpd.SetAlpha();

            // Decide whether to actually draw the waypoint
            if (FlightGlobals.ActiveVessel != null)
            {
                // Figure out the distance to the waypoint
                Vessel v = FlightGlobals.ActiveVessel;

                // Only change alpha if the waypoint isn't the nav point
                if (!Util.IsNavPoint(wpd.waypoint))
                {
                    // Get the distance to the waypoint at the current speed
                    double speed = v.srfSpeed < MIN_SPEED ? MIN_SPEED : v.srfSpeed;
                    double directTime = Util.GetStraightDistance(wpd) / speed;

                    // More than two minutes away
                    if (directTime > MIN_TIME || Config.waypointDisplay != Config.WaypointDisplay.ALL)
                    {
                        return;
                    }
                    else if (directTime >= MIN_TIME - FADE_TIME)
                    {
                        wpd.currentAlpha = (float)((MIN_TIME - directTime) / FADE_TIME) * Config.opacity;
                    }
                }
                // Draw the distance information to the nav point
                else
                {
                    // Draw the distance to waypoint text
                    if (Event.current.type == EventType.Repaint)
                    {
                        if (asb == null)
                        {
                            asb = UnityEngine.Object.FindObjectOfType<AltimeterSliderButtons>();
                            asbRectTransform = asb.GetComponent<RectTransform>();
                        }


                        fullBoxHeight = 0;
                        boxWidth = 0;
                        string timeToWP = GetTimeToWaypoint(wpd);
                        if (Config.hudDistance)
                        {
                            ShowText("Distance to " + label + ":", v.state != Vessel.State.DEAD ? Util.PrintDistance(wpd) : "N/A");
                        }

                        if (timeToWP != null && Config.hudTime)
                        {
                            ShowText("ETA to " + label + ":", v.state != Vessel.State.DEAD ? timeToWP : "N/A");
                        }

                        if (Config.hudHeading)
                        {
                            ShowText("Heading to " + label + ":", v.state != Vessel.State.DEAD ? wpd.heading.ToString("N1") : "N/A");
                        }

                        if (Config.hudAngle && v.mainBody == wpd.celestialBody)
                        {
                            double distance = Util.GetLateralDistance(wpd);
                            double heightDist = wpd.waypoint.altitude + wpd.waypoint.height - v.altitude;
                            double angle = Math.Atan2(heightDist, distance) * 180.0 / Math.PI;

                            ShowText("Angle to " + label + ":", v.state != Vessel.State.DEAD ? angle.ToString("N2") : "N/A");

                            if (v.srfSpeed >= 0.1)
                            {
                                double velAngle = 90 - Math.Acos(Vector3d.Dot(v.srf_velocity.normalized, v.upAxis)) * 180.0 / Math.PI;

                                ShowText("Velocity pitch angle:", v.state != Vessel.State.DEAD ? velAngle.ToString("N2") : "N/A");
                            }
                        }
                        Config.displayBox = new Rect(Config.boxLeft, Config.boxTop, boxWidth, fullBoxHeight);
                    }
                }
            }

            // Don't draw the waypoint
            if (Config.waypointDisplay == Config.WaypointDisplay.NONE)
            {
                return;
            }

            // Translate to scaled space
            Vector3d localSpacePoint = celestialBody.GetWorldSurfacePosition(wpd.waypoint.latitude, wpd.waypoint.longitude, wpd.waypoint.height + wpd.waypoint.altitude);
            Vector3d scaledSpacePoint = ScaledSpace.LocalToScaledSpace(localSpacePoint);

            // Don't draw if it's behind the camera
            if (Vector3d.Dot(PlanetariumCamera.Camera.transform.forward, scaledSpacePoint.normalized) < 0.0)
            {
                return;
            }

            // Translate to screen position
            Vector3 screenPos = PlanetariumCamera.Camera.WorldToScreenPoint(new Vector3((float)scaledSpacePoint.x, (float)scaledSpacePoint.y, (float)scaledSpacePoint.z));

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

                // Draw the marker
                Graphics.DrawTexture(markerRect, GameDatabase.Instance.GetTexture("Squad/Contracts/Icons/marker", false), new Rect(0.0f, 0.0f, 1f, 1f), 0, 0, 0, 0, new Color(0.5f, 0.5f, 0.5f, 0.5f * (wpd.currentAlpha - 0.3f) / 0.7f));

                // Draw the icon, but support blinking
                if (!Util.IsNavPoint(wpd.waypoint) || !NavWaypoint.fetch.IsBlinking || (int)((Time.fixedTime - (int)Time.fixedTime) * 4) % 2 == 0)
                {
                    Graphics.DrawTexture(iconRect, /*ContractDefs.sprites[wpd.waypoint.id].texture */
                        Util.GetContractIcon(wpd.waypoint.id, wpd.waypoint.seed), new Rect(0.0f, 0.0f, 1f, 1f), 0, 0, 0, 0, SystemUtilities.RandomColor(wpd.waypoint.seed, wpd.currentAlpha));
                }

                // Hint text!
                if (iconRect.Contains(Event.current.mousePosition))
                {
                    // Add agency to label
                    if (wpd.waypoint.contractReference != null)
                    {
                        label += "\n" + wpd.waypoint.contractReference.Agent.Name;
                    }
                    float width = 240f;
                    float height = hintTextStyle.CalcHeight(new GUIContent(label), width);
                    float yoffset = height + 48.0f;
                    GUI.Box(new Rect(screenPos.x - width / 2.0f, (float)Screen.height - screenPos.y - yoffset, width, height), label, hintTextStyle);
                }
            }
        }


        float boxWidth = 0;
        float fullBoxHeight = 0;


        private void ShowText(string leftText, string rightText)
        {
            GUIContent leftTmp = new GUIContent(leftText);
            GUIContent rightTmp = new GUIContent(rightText);

            Vector2 leftLabelBoxSize = nameStyle.CalcSize(leftTmp);
            Vector2 rightLabelBoxSize = valueStyle.CalcSize(rightTmp);

            if (Config.boxLeft < 0)
            {
                Config.boxLeft = (Screen.width - leftLabelBoxSize.x - rightLabelBoxSize.x - 5) / 2;
            }
            if (Config.boxTop < 0)
            {
                Config.boxTop = Math.Max(0, Screen.height / 2.0f - asbRectTransform.position.y + asbRectTransform.sizeDelta.y * 0.5f + 20);
            }

            GUI.Label(new Rect(Config.boxLeft, Config.boxTop + fullBoxHeight, leftLabelBoxSize.x + 5f, boxHeight), leftText, nameStyle);
            GUI.Label(new Rect(Config.boxLeft + leftLabelBoxSize.x + 5, Config.boxTop + fullBoxHeight, rightLabelBoxSize.x, boxHeight), rightText, valueStyle);

            fullBoxHeight += boxHeight; // - 2;
            boxWidth = Math.Max(boxWidth, leftLabelBoxSize.x + 5 + rightLabelBoxSize.x);
        }

        private void ShowNavigationWindow()
        {
            if (selectedWaypoint != null)
            {
                GUI.skin = HighLogic.Skin;
                windowPos = ClickThruBlocker.GUILayoutWindow(10, windowPos, NavigationWindow, waypointName, GUILayout.MinWidth(224));
            }
        }

        private void NavigationWindow(int windowID)
        {
            if (selectedWaypoint == null)
            {
                return;
            }

            GUILayout.BeginVertical();
            if (!Util.IsNavPoint(selectedWaypoint))
            {
                if (GUILayout.Button("Activate Navigation", HighLogic.Skin.button, GUILayout.ExpandWidth(true)))
                {
                    NavWaypoint.fetch.Setup(selectedWaypoint);
                    NavWaypoint.fetch.Activate();
                    selectedWaypoint = null;
                }
            }
            else
            {
                if (GUILayout.Button("Deactivate Navigation", HighLogic.Skin.button, GUILayout.ExpandWidth(true)))
                {
                    NavWaypoint.fetch.Clear();
                    NavWaypoint.fetch.Deactivate();
                    selectedWaypoint = null;
                }

            }
            if (CustomWaypoints.Instance.IsCustom(selectedWaypoint))
            {
                if (GUILayout.Button("Edit Custom Waypoint", HighLogic.Skin.button, GUILayout.ExpandWidth(true)))
                {
                    CustomWaypointGUI.EditWaypoint(selectedWaypoint);
                    selectedWaypoint = null;
                }
                if (GUILayout.Button("Delete Custom Waypoint", HighLogic.Skin.button, GUILayout.ExpandWidth(true)))
                {
                    CustomWaypointGUI.DeleteWaypoint(selectedWaypoint);
                    selectedWaypoint = null;
                }
            }
            GUILayout.EndVertical();
        }

        /// <summary>
        /// Calculates the time to the distance based on the vessels srfSpeed and transform it to a readable string.
        /// </summary>
        /// <param name="waypoint">The waypoint</param>
        /// <param name="distance">Distance in meters</param>
        /// <returns></returns>
        protected string GetTimeToWaypoint(WaypointData wpd)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v.srfSpeed < 0.1)
            {
                return null;
            }

            double time = (wpd.distanceToActive / v.horizontalSrfSpeed);

            uint SecondsPerYear = (uint)KSPUtil.dateTimeFormatter.Year;
            uint SecondsPerDay = (uint)KSPUtil.dateTimeFormatter.Day;
            uint SecondsPerHour = (uint)KSPUtil.dateTimeFormatter.Hour;
            uint SecondsPerMinute = (uint)KSPUtil.dateTimeFormatter.Minute;

            int years = (int)(time / SecondsPerYear);
            time -= years * SecondsPerYear;

            int days = (int)(time / SecondsPerDay);
            time -= days * SecondsPerDay;

            int hours = (int)(time / SecondsPerHour);
            time -= hours * SecondsPerHour;

            int minutes = (int)(time / SecondsPerMinute);
            time -= minutes * SecondsPerMinute;

            int seconds = (int)(time);

            string output = "";
            if (years != 0)
            {
                output += years + "y";
            }
            if (days != 0)
            {
                if (output.Length != 0) output += ", ";
                output += days + "d";
            }
            if (hours != 0 || minutes != 0 || seconds != 0 || output.Length == 0)
            {
                if (output.Length != 0) output += ", ";
                output += hours.ToString("D2") + ":" + minutes.ToString("D2") + ":" + seconds.ToString("D2");
            }

            return output;
        }

        public void HandleClick(WaypointData wpd)
        {
            // Translate to screen position
            Vector3d localSpacePoint = wpd.celestialBody.GetWorldSurfacePosition(wpd.waypoint.latitude, wpd.waypoint.longitude, wpd.waypoint.altitude);
            Vector3d scaledSpacePoint = ScaledSpace.LocalToScaledSpace(localSpacePoint);
            Vector3 screenPos = PlanetariumCamera.Camera.WorldToScreenPoint(new Vector3((float)scaledSpacePoint.x, (float)scaledSpacePoint.y, (float)scaledSpacePoint.z));

            Rect markerRect = new Rect(screenPos.x - 15f, (float)Screen.height - screenPos.y - 45.0f, 30f, 45f);

            if (markerRect.Contains(Event.current.mousePosition))
            {
                selectedWaypoint = wpd.waypoint;
                windowPos = new Rect(markerRect.xMin - 97, markerRect.yMax + 12, 224, 60);
                waypointName = wpd.waypoint.name + (wpd.waypoint.isClustered ? (" " + StringUtilities.IntegerToGreek(wpd.waypoint.index)) : "");
                newClick = false;
            }
            else if (newClick)
            {
                selectedWaypoint = null;
            }
        }

        public void HintText(WaypointData wpd)
        {
            // Translate to screen position
            Vector3d localSpacePoint = wpd.celestialBody.GetWorldSurfacePosition(wpd.waypoint.latitude, wpd.waypoint.longitude, wpd.waypoint.altitude);
            Vector3d scaledSpacePoint = ScaledSpace.LocalToScaledSpace(localSpacePoint);
            Vector3 screenPos = PlanetariumCamera.Camera.WorldToScreenPoint(new Vector3((float)scaledSpacePoint.x, (float)scaledSpacePoint.y, (float)scaledSpacePoint.z));

            Rect iconRect = new Rect(screenPos.x - 8f, (float)Screen.height - screenPos.y - 39.0f, 16f, 16f);

            // Hint text!
            if (iconRect.Contains(Event.current.mousePosition))
            {
                string label = wpd.waypoint.name + (wpd.waypoint.isClustered ? (" " + StringUtilities.IntegerToGreek(wpd.waypoint.index)) : "");
                float width = 240f;
                float height = hintTextStyle.CalcHeight(new GUIContent(label), width);
                float yoffset = height + 48.0f;
                GUI.Box(new Rect(screenPos.x - width / 2.0f, (float)Screen.height - screenPos.y - yoffset, width, height), label, hintTextStyle);
            }
        }
    }
}
