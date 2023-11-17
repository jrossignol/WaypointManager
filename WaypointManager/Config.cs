using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace WaypointManager
{
    /// <summary>
    /// Holds WaypointManager persistent configurator.
    /// </summary>
    public static class Config
    {
        private static string ConfigFileName
        {
            get
            {
                return string.Join(Path.DirectorySeparatorChar.ToString(), new string[]
                    { KSPUtil.ApplicationRootPath, "GameData", "WaypointManager", "PluginData", "WaypointManager.cfg"});
            }
        }
        private static string OldConfigFileName
        {
            get
            {
                return string.Join(Path.DirectorySeparatorChar.ToString(), new string[]
                    { KSPUtil.ApplicationRootPath, "GameData", "WaypointManager", "WaypointManager.cfg"});
            }
        }

        public enum DisplayMode
        {
            CONTRACT,
            CELESTIAL_BODY
        }

        public enum DistanceCalcMethod
        {
            LATERAL,
            STRAIGHT_LINE,
            COMPROMISE
        }

        public enum WaypointDisplay
        {
            ALL,
            ACTIVE,
            NONE
        }

        public static Rect mainWindowPos = new Rect(116f, 131f, 240f, 40f);
        public static DisplayMode displayMode = DisplayMode.CONTRACT;
        public static DistanceCalcMethod distanceCalcMethod = DistanceCalcMethod.COMPROMISE;
        public static WaypointDisplay waypointDisplay = WaypointDisplay.ALL;
        public static bool displayDecimal = false;

        public static bool hudDistance = true;
        public static bool hudTime = true;
        public static bool hudHeading = false;
        public static bool hudAngle = false;

        public static float opacity = 1.0f;

        public static float scaling = 1.0f;

        public static Texture2D toolbarIcon;
        public static Texture2D addWaypointIcon;
        public static Texture2D editWaypointIcon;
        public static Texture2D deleteWaypointIcon;
        public static Texture2D settingsIcon;
        public static Texture2D closeIcon;

        /// <summary>
        /// Saves the configuration to the default configuration file.
        /// </summary>
        public static void Save()
        {
            ConfigNode configNode = new ConfigNode("WAYPOINT_MANAGER_SETTINGS");

            configNode.AddValue("mainWindowPos.x", mainWindowPos.xMin);
            configNode.AddValue("mainWindowPos.y", mainWindowPos.yMin);
            configNode.AddValue("displayMode", displayMode);
            configNode.AddValue("distanceCalcMethod", distanceCalcMethod);
            configNode.AddValue("waypointDisplay", waypointDisplay);
            configNode.AddValue("hudDistance", hudDistance);
            configNode.AddValue("hudTime", hudTime);
            configNode.AddValue("hudHeading", hudHeading);
            configNode.AddValue("hudAngle", hudAngle);
            configNode.AddValue("displayDecimal", displayDecimal);
            configNode.AddValue("opacity", opacity);
            configNode.AddValue("scaling", scaling);

            configNode.Save(ConfigFileName,
                "Waypoint Manager Configuration File\r\n" +
                "//\r\n" +
                "// WARNING: this is an auto-generated file.  You can make changes if you are\r\n" +
                "// careful, but if you suspect you broke something it is safe to simply delete\r\n" +
                "// this file and let it be regenerated.");
        }

        /// <summary>
        /// Loads the configuration from the default configuration file.
        /// </summary>
        public static void Load()
        {
            ConfigNode configNode = ConfigNode.Load(ConfigFileName);

            // No config file, try old one
            if (configNode == null)
            {
                configNode = ConfigNode.Load(OldConfigFileName);
                if (configNode == null)
                {
                    return;
                }
            }

            float left = (float)Convert.ToDouble(configNode.GetValue("mainWindowPos.x"));
            float top = (float)Convert.ToDouble(configNode.GetValue("mainWindowPos.y"));
            mainWindowPos = new Rect(left, top, 1, 1);
            displayMode = configNode.GetEnumValue<DisplayMode>("displayMode");
            distanceCalcMethod = configNode.GetEnumValue<DistanceCalcMethod>("distanceCalcMethod");
            waypointDisplay = configNode.GetEnumValue<WaypointDisplay>("waypointDisplay");
            hudDistance = Convert.ToBoolean(configNode.GetValue("hudDistance"));
            hudTime = Convert.ToBoolean(configNode.GetValue("hudTime"));
            hudHeading = Convert.ToBoolean(configNode.GetValue("hudHeading"));
            hudAngle = configNode.HasValue("hudAngle") ? Convert.ToBoolean(configNode.GetValue("hudAngle")) : false;
            opacity = configNode.HasValue("opacity") ? (float)Convert.ToDouble(configNode.GetValue("opacity")) : 1.0f;
            displayDecimal = configNode.HasValue("displayDecimal") ? Convert.ToBoolean(configNode.GetValue("displayDecimal")) : false;

            scaling = configNode.HasValue("scaling") ? (float)Convert.ToDouble(configNode.GetValue("scaling")) : 1.0f;
        }

        private static T GetEnumValue<T>(this ConfigNode configNode, string name)
        {
            return (T)Enum.Parse(typeof(T), configNode.GetValue(name));
        }
    }
}
