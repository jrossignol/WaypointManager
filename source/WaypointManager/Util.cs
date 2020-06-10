using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using FinePrint;
using FinePrint.Utilities;
using DDSHeaders;

namespace WaypointManager
{
    /// <summary>
    /// Utility methods for WaypointManager.
    /// </summary>
    public static class Util
    {
        private static string[] UNITS = { "m", "km", "Mm", "Gm", "Tm" };
        private static Dictionary<string, Dictionary<Color, Texture2D>> contractIcons = new Dictionary<string, Dictionary<Color, Texture2D>>();
        private static float lastAlpha;

        /// <summary>
        /// Gets the lateral distance in meters from the active vessel to the given waypoint.
        /// </summary>
        /// <param name="wpd">Activated waypoint</param>
        /// <returns>Distance in meters</returns>
        public static double GetLateralDistance(WaypointData wpd)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            CelestialBody celestialBody = v.mainBody;

            // Use the haversine formula to calculate great circle distance.
            double sin1 = Math.Sin(Math.PI / 180.0 * (v.latitude - wpd.waypoint.latitude) / 2);
            double sin2 = Math.Sin(Math.PI / 180.0 * (v.longitude - wpd.waypoint.longitude) / 2);
            double cos1 = Math.Cos(Math.PI / 180.0 * wpd.waypoint.latitude);
            double cos2 = Math.Cos(Math.PI / 180.0 * v.latitude);

            return 2 * (celestialBody.Radius + wpd.waypoint.height + wpd.waypoint.altitude) *
                Math.Asin(Math.Sqrt(sin1 * sin1 + cos1 * cos2 * sin2 * sin2));
        }
        
        /// <summary>
        /// Gets the distance in meters from the active vessel to the given waypoint.
        /// </summary>
        /// <param name="wpd">Activated waypoint</param>
        /// <returns>Distance in meters</returns>
        public static double GetDistanceToWaypoint(WaypointData wpd)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            CelestialBody celestialBody = v.mainBody;

            // Simple distance
            if (Config.distanceCalcMethod == Config.DistanceCalcMethod.STRAIGHT_LINE || celestialBody != wpd.celestialBody)
            {
                return GetStraightDistance(wpd);
            }

            // Use the haversine formula to calculate great circle distance.
            double sin1 = Math.Sin(Math.PI / 180.0 * (v.latitude - wpd.waypoint.latitude) / 2);
            double sin2 = Math.Sin(Math.PI / 180.0 * (v.longitude - wpd.waypoint.longitude) / 2);
            double cos1 = Math.Cos(Math.PI / 180.0 * wpd.waypoint.latitude);
            double cos2 = Math.Cos(Math.PI / 180.0 * v.latitude);

            double lateralDist = 2 * (celestialBody.Radius + wpd.waypoint.height + wpd.waypoint.altitude) *
                Math.Asin(Math.Sqrt(sin1 * sin1 + cos1 * cos2 * sin2 * sin2));
            double heightDist = Math.Abs(wpd.waypoint.altitude + wpd.waypoint.height - v.altitude);

            if (Config.distanceCalcMethod == Config.DistanceCalcMethod.LATERAL || heightDist <= lateralDist / 2.0)
            {
                return lateralDist;
            }
            else
            {
                // Get the ratio to use in our formula
                double x = (heightDist - lateralDist / 2.0) / lateralDist;

                // x / (x + 1) starts at 0 when x = 0, and increases to 1
                return (x / (x + 1)) * heightDist + lateralDist;
            }
        }

        public static double GetStraightDistance(WaypointData wpd)
        {
            Vessel v = FlightGlobals.ActiveVessel;

            Vector3 wpPosition = wpd.celestialBody.GetWorldSurfacePosition(wpd.waypoint.latitude, wpd.waypoint.longitude, wpd.waypoint.height + wpd.waypoint.altitude);
            return Vector3.Distance(wpPosition, v.transform.position);
        }

        /// <summary>
        /// Gets the printable distance to the waypoint.
        /// </summary>
        /// <param name="wpd">WaypointData object</param>
        /// <returns>The distance and unit for screen output</returns>
        public static string PrintDistance(WaypointData wpd)
        {
            int unit = 0;
            double distance = wpd.distanceToActive;
            while (unit < 4 && distance >= 10000.0)
            {
                distance /= 1000.0;
                unit++;
            }

            return distance.ToString("N1") + " " + UNITS[unit];
        }

        /// <summary>
        /// Formats the coordinate.
        /// </summary>
        /// <param name="coord">The coordinate</param>
        /// <returns>The location for screen output</returns>
        public static string FormatCoordinate(double coord, bool islatitude)
        {
            if (Config.displayDecimal)
            {
                return coord.ToString("F3") + " °";
            }
            else
            {
                double acoord = Math.Abs(coord);
                int d = (int)acoord;
                int m = (int)Math.Abs((acoord - d) * 60.0);
                int s = (int)Math.Abs(((acoord - d) * 60.0 - m) * 60.0);

                string direction = coord > 0.0 ? (islatitude ? "N" : "E") : (islatitude ? "S" : "W");

                return string.Format("{0}° {1}' {2}\" {3}", d, m, s, direction);
            }
        }

        /// <summary>
        /// Gets the celestial body for the given name.
        /// </summary>
        /// <param name="name">Name of the celestial body</param>
        /// <returns>The CelestialBody object</returns>
        public static CelestialBody GetBody(string name)
        {
            CelestialBody body = FlightGlobals.Bodies.Where(b => b.bodyName == name).FirstOrDefault();
            if (body == null)
            {
                Debug.LogWarning("Couldn't find celestial body with name '" + name + "'.");
            }
            return body;
        }

        /// <summary>
        /// Checks if the given waypoint is the nav waypoint.
        /// </summary>
        /// <param name="waypoint"></param>
        /// <returns></returns>
        public static bool IsNavPoint(Waypoint waypoint)
        {
            NavWaypoint navPoint = NavWaypoint.fetch;
            if (navPoint == null || !NavWaypoint.fetch.IsActive)
            {
                return false;
            }

            return navPoint.Latitude == waypoint.latitude && navPoint.Longitude == waypoint.longitude;
        }

        /// <summary>
        /// Gets the contract icon for the given id and seed (color).
        /// </summary>
        /// <param name="url">URL of the icon</param>
        /// <param name="seed">Seed to use for generating the color</param>
        /// <returns>The texture</returns>
        public static Texture2D GetContractIcon(string url, int seed)
        {
            // Check cache for texture
            Texture2D texture;
            Color color = SystemUtilities.RandomColor(seed, 1.0f, 1.0f, 1.0f);
            if (!contractIcons.ContainsKey(url))
            {
                contractIcons[url] = new Dictionary<Color, Texture2D>();
            }
            if (!contractIcons[url].ContainsKey(color))
            {
                Texture2D baseTexture = ContractDefs.sprites[url].texture;

                try
                {
                    Texture2D loadedTexture = null;
                    string path = (url.Contains('/') ? "GameData/" : "GameData/Squad/Contracts/Icons/") + url;
                    // PNG loading
                    if (File.Exists(path + ".png"))
                    {
                        path += ".png";
                        loadedTexture = new Texture2D(baseTexture.width, baseTexture.height, TextureFormat.RGBA32, false);
                        loadedTexture.LoadImage(File.ReadAllBytes(path.Replace('/', Path.DirectorySeparatorChar)));
                    }
                    // DDS loading
                    else if (File.Exists(path + ".dds"))
                    {
                        path += ".dds";
                        BinaryReader br = new BinaryReader(new MemoryStream(File.ReadAllBytes(path)));

                        if (br.ReadUInt32() != DDSValues.uintMagic)
                        {
                            throw new Exception("Format issue with DDS texture '" + path + "'!");
                        }
                        DDSHeader ddsHeader = new DDSHeader(br);
                        if (ddsHeader.ddspf.dwFourCC == DDSValues.uintDX10)
                        {
                            DDSHeaderDX10 ddsHeaderDx10 = new DDSHeaderDX10(br);
                        }

                        TextureFormat texFormat;
                        if (ddsHeader.ddspf.dwFourCC == DDSValues.uintDXT1)
                        {
                            texFormat = UnityEngine.TextureFormat.DXT1;
                        }
                        else if (ddsHeader.ddspf.dwFourCC == DDSValues.uintDXT3)
                        {
                            texFormat = UnityEngine.TextureFormat.DXT1 | UnityEngine.TextureFormat.Alpha8;
                        }
                        else if (ddsHeader.ddspf.dwFourCC == DDSValues.uintDXT5)
                        {
                            texFormat = UnityEngine.TextureFormat.DXT5;
                        }
                        else
                        {
                            throw new Exception("Unhandled DDS format!");
                        }

                        loadedTexture = new Texture2D((int)ddsHeader.dwWidth, (int)ddsHeader.dwHeight, texFormat, false);
                        loadedTexture.LoadRawTextureData(br.ReadBytes((int)(br.BaseStream.Length - br.BaseStream.Position)));
                    }
                    else
                    {
                        throw new Exception("Couldn't find file for icon  '" + url + "'");
                    }

                    Color[] pixels = loadedTexture.GetPixels();
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        pixels[i] *= color;
                    }
                    texture = new Texture2D(baseTexture.width, baseTexture.height, TextureFormat.RGBA32, false);
                    texture.SetPixels(pixels);
                    texture.Apply(false, false);
                    contractIcons[url][color] = texture;
                    UnityEngine.Object.Destroy(loadedTexture);
                }
                catch (Exception e)
                {
                    Debug.LogError("WaypointManager: Couldn't create texture for '" + url + "'!");
                    Debug.LogException(e);
                    texture = contractIcons[url][color] = baseTexture;
                }
            }
            else
            {
                texture = contractIcons[url][color];
            }

            return texture;
        }

        public static double WaypointHeight(Waypoint w, CelestialBody body)
        {
            return TerrainHeight(w.latitude, w.longitude, body);
        }

        public static double TerrainHeight(double latitude, double longitude, CelestialBody body)
        {
            // Not sure when this happens - for Sun and Jool?
            if (body.pqsController == null)
            {
                return 0;
            }

            // Figure out the terrain height
            double latRads = Math.PI / 180.0 * latitude;
            double lonRads = Math.PI / 180.0 * longitude;
            Vector3d radialVector = new Vector3d(Math.Cos(latRads) * Math.Cos(lonRads), Math.Sin(latRads), Math.Cos(latRads) * Math.Sin(lonRads));
            return Math.Max(body.pqsController.GetSurfaceHeight(radialVector) - body.pqsController.radius, 0.0);
        }

        public static void DrawWaypoint(CelestialBody targetBody, double latitude, double longitude, double altitude, string id, int seed, float alpha = -1.0f)
        {
            // Translate to scaled space
            Vector3d localSpacePoint = targetBody.GetWorldSurfacePosition(latitude, longitude, altitude);
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

            // Half-res for the icon too (16 x 16)
            Rect iconRect = new Rect(screenPos.x - 8f, (float)Screen.height - screenPos.y - 39.0f, 16f, 16f);

            if (alpha < 0.0f)
            {
                Vector3 cameraPos = ScaledSpace.ScaledToLocalSpace(PlanetariumCamera.Camera.transform.position);
                bool occluded = WaypointData.IsOccluded(targetBody, cameraPos, localSpacePoint, altitude);
                float desiredAlpha = occluded ? 0.3f : 1.0f * Config.opacity;
                if (lastAlpha < 0.0f)
                {
                    lastAlpha = desiredAlpha;
                }
                else if (lastAlpha < desiredAlpha)
                {
                    lastAlpha = Mathf.Clamp(lastAlpha + Time.deltaTime * 4f, lastAlpha, desiredAlpha);
                }
                else
                {
                    lastAlpha = Mathf.Clamp(lastAlpha - Time.deltaTime * 4f, desiredAlpha, lastAlpha);
                }
                alpha = lastAlpha;
            }

            // Draw the marker
            Graphics.DrawTexture(markerRect, GameDatabase.Instance.GetTexture("Squad/Contracts/Icons/marker", false), new Rect(0.0f, 0.0f, 1f, 1f), 0, 0, 0, 0, new Color(0.5f, 0.5f, 0.5f, 0.5f * (alpha - 0.3f) / 0.7f));

            // Draw the icon
            Graphics.DrawTexture(iconRect, ContractDefs.sprites[id].texture, new Rect(0.0f, 0.0f, 1f, 1f), 0, 0, 0, 0, SystemUtilities.RandomColor(seed, alpha));
        }

    }
}
