using ClickThroughFix;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static WaypointManager.RegisterToolbar;

namespace WaypointManager
{

    public class ImportExport : MonoBehaviour
    {
        List<string> waypointFiles = new List<string>();
        string path = "";
        bool visible = true;
        Rect windowPos = new Rect(500, 500, WaypointManager.SETTINGS_WIDTH, 200);

        static internal bool helpDialogVisible = false;
        bool exitCustomImport = false;


        void Awake()
        {
            path = string.Join(Path.DirectorySeparatorChar.ToString(), CustomWaypoints.CustomWaypointsDirectory, "CustomWaypoints");
        }

        void Start()
        {
            foreach (var str in Directory.GetFiles(path))
            {
                waypointFiles.Add(Path.GetFileName(str));
            }
            windowPos = new Rect(
                WaypointManager.Instance.settingsPosition.xMax + WaypointManager.SETTINGS_WIDTH + 4 > Screen.width ? WaypointManager.Instance.settingsPosition.xMin - WaypointManager.SETTINGS_WIDTH - 4 : WaypointManager.Instance.settingsPosition.xMax,
                WaypointManager.Instance.settingsPosition.yMin, WaypointManager.SETTINGS_WIDTH, 200);

        }

        // OnGUI has all the GUI stuff
        void OnGUI()
        {
            if (visible && !ImportExport.helpDialogVisible)
            {

                windowPos = ClickThruBlocker.GUILayoutWindow(typeof(WaypointManager).FullName.GetHashCode() + 20,
                     windowPos, WindowGUI, "Custom Waypoint Selection");

                // Add the close icon
                if (GUI.Button(new Rect(windowPos.xMax - 18, windowPos.yMin + 2, 16, 16), Config.closeIcon, GUI.skin.label))
                {
                    Destroy(this);
                }

            }
        }


        int selected = -1;
        Vector2 scrollListPos = new Vector2();
        void WindowGUI(int id)
        {
            GUILayout.BeginHorizontal();
            scrollListPos = GUILayout.BeginScrollView(scrollListPos);
            int cnt = -1;
            foreach (var wayPointFile in waypointFiles)
            {
                cnt++;
                GUILayout.BeginHorizontal();
                string str = wayPointFile.Replace("CustomWaypoints.", "");
                str = str.Replace(".cfg", "");
                if (selected == cnt)
                    str = "--> " + str + " <--";
                if (GUILayout.Button(str))
                {
                    selected = cnt;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUI.enabled = (selected >= 0);
            if (GUILayout.Button("Import"))
            {
                ImportSelectedFile(selected);
            }
            GUI.enabled = true;
            if (GUILayout.Button("OK") || exitCustomImport)
            {
                visible = false;
                Destroy(this);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (!ImportExport.helpDialogVisible && GUILayout.Button("Custom WP Help"))
            {
                helpDialogVisible = true;
                DialogGUIBase[] options = new DialogGUIBase[2];

                options[0] = new DialogGUIButton("Ok", () =>
                {
                    helpDialogVisible = false;
                });
                options[1] = new DialogGUIButton("Close", () =>
                {
                    exitCustomImport = true;
                    helpDialogVisible = false;
                });

                var multidialog = new MultiOptionDialog("waypointManager",
                    "If you have custom waypoints in a file, you can add it to the directory for loading.\n\n" +
                    "Put your custom file in the following directory:\n\nGameData/WaypointManager/PluginData/CustomWayPoints\n\n", "Custom File Help",
                                        UI.StyleCache.Skin,
                                      /*HighLogic.UISkin,*/ 450, options);

                var _activePopup = PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), multidialog, false, HighLogic.UISkin, true);
            }
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        void ImportSelectedFile(int cnt)
        {
            var wayPointFile = waypointFiles[cnt];
            var fullpath = string.Join(Path.DirectorySeparatorChar.ToString(), path, wayPointFile);
            ConfigNode master = new ConfigNode();
            var configNode = ConfigNode.Load(fullpath);
            CustomWaypoints.AddWaypointsFromConfig(master, configNode);
            ScenarioCustomWaypoints.Instance.OnLoad(master);

            int count = master.nodes.Count;
            if (count > 0)
            {
                Log.Info("[WM] Imported " + count + " waypoint" + (count != 1 ? "s" : "") + " from " + wayPointFile);
                ScreenMessages.PostScreenMessage("Imported " + count + " waypoint" + (count != 1 ? "s" : "") + " from " + wayPointFile,
                    6.0f, ScreenMessageStyle.UPPER_CENTER);
            }
            else
            {
                Log.Info("[WM] No new waypoints found to import");
                ScreenMessages.PostScreenMessage("No new waypoints found to import", 6f);
            }

        }

    }
}
