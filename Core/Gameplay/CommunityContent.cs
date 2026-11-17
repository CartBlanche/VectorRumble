using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>A community file the game did not load, and why.</summary>
    internal sealed class SkippedFile
    {
        public string Kind;
        public string Path;
        public string Reason;
    }

    /// <summary>
    /// Arenas and ships the player made (in the "Arenas" and "Ships" folders), and the ones a host sends in a network game. A file
    /// from outside the game is never trusted: it is read tolerantly (any whitespace, optional parts missing) but checked against
    /// limits, and a bad one is skipped with a reason, never a crash. The files that come with the game do not go through here.
    /// </summary>
    internal static class CommunityContent
    {
        /// <summary>The biggest file read or sent. A 400-wall arena is about 12 KB.</summary>
        public const int MaxFileBytes = 64 * 1024;
        public const int MaxWalls = 400;
        public const int MinShipPoints = 3;
        public const int MaxShipPoints = 24;
        public const float MaxShipReach = 32f;
        public const int MaxNameLength = 40;
        public const int MaxThrusters = 3;
        public const float MinThrusterSize = 0.5f;
        public const float MaxThrusterSize = 2f;

        /// <summary>What was skipped the last time the folders were read, for the log and for tests.</summary>
        public static readonly List<SkippedFile> Skipped = new List<SkippedFile>();

        static readonly char[] Whitespace = { ' ', '\t', '\r', '\n' };

        /// <summary>The id of a file: the lower-case hex SHA-256 of its bytes. The same file has the same id on every machine.</summary>
        public static string IdOf(byte[] bytes)
        {
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        /// <summary>Text for showing: control characters removed, spaces trimmed, cut to <paramref name="max"/> characters.</summary>
        public static string CleanText(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (char c in text)
            {
                if (!char.IsControl(c))
                {
                    builder.Append(c);
                }
            }

            string clean = builder.ToString().Trim();
            return clean.Length > max ? clean.Substring(0, max).TrimEnd() : clean;
        }

        #region Reading the folders

        /// <summary>
        /// The files in the folder for <paramref name="kind"/> ("Arenas" or "Ships") and in the folder earlier versions used. A file
        /// that is too big or cannot be read is skipped. The same file in both places comes once.
        /// </summary>
        public static List<(string path, byte[] bytes, string id)> ReadFolders(string kind)
        {
            var found = new List<(string, byte[], string)>();
            var seen = new HashSet<string>();
            ReadFolder(Helper.GetCommunityFolder(kind), kind, found, seen);
            ReadFolder(Helper.GetLegacyCommunityFolder(kind), kind, found, seen);
            return found;
        }

        /// <summary>The XML files of one folder, added to <paramref name="found"/> unless their id is already in <paramref name="seen"/>.</summary>
        public static void ReadFolder(string folder, string kind, List<(string path, byte[] bytes, string id)> found, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            string[] files;
            try
            {
                if (!Directory.Exists(folder))
                {
                    return;
                }

                files = Directory.GetFiles(folder, "*.xml");
            }
            catch (Exception ex)
            {
                Skip(kind, folder, "The folder cannot be read: " + ex.Message);
                return;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                try
                {
                    long length = new FileInfo(file).Length;
                    if (length > MaxFileBytes)
                    {
                        Skip(kind, file, "The file is " + (length / 1024) + " KB; more than " + (MaxFileBytes / 1024) + " KB is not accepted.");
                        continue;
                    }

                    byte[] bytes = File.ReadAllBytes(file);
                    string id = IdOf(bytes);
                    if (seen.Add(id))
                    {
                        found.Add((file, bytes, id));
                    }
                }
                catch (Exception ex)
                {
                    Skip(kind, file, "The file cannot be read: " + ex.Message);
                }
            }
        }

        /// <summary>Starts the list of skipped files again for one kind: each scan reports its own.</summary>
        public static void ForgetSkipped(string kind)
        {
            Skipped.RemoveAll(s => s.Kind == kind);
        }

        public static void Skip(string kind, string path, string reason)
        {
            Skipped.Add(new SkippedFile { Kind = kind, Path = path, Reason = reason });
            System.Diagnostics.Debug.WriteLine("[Community] Skipped " + path + ": " + reason);
        }

        #endregion

        #region Parsing

        static XElement Root(byte[] bytes, string type, out string reason)
        {
            reason = null;
            if (bytes == null || bytes.Length == 0)
            {
                reason = "The file is empty.";
                return null;
            }

            if (bytes.Length > MaxFileBytes)
            {
                reason = "The file is too big.";
                return null;
            }

            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxFileBytes * 2 };
                XDocument document;
                using (var stream = new MemoryStream(bytes))
                using (var reader = XmlReader.Create(stream, settings))
                {
                    document = XDocument.Load(reader);
                }

                XElement asset = document.Root?.Element("Asset");
                if (asset == null)
                {
                    reason = "It is not a game file (no XnaContent / Asset).";
                    return null;
                }

                string declared = (string)asset.Attribute("Type") ?? string.Empty;
                if (!declared.EndsWith(":" + type, StringComparison.Ordinal) && declared != type)
                {
                    reason = "It holds a \"" + declared + "\", not a " + type.ToLowerInvariant() + ".";
                    return null;
                }

                return asset;
            }
            catch (Exception ex) when (ex is XmlException || ex is InvalidOperationException)
            {
                reason = "It is not valid XML (" + ex.Message + ").";
                return null;
            }
        }

        static string Text(XElement root, string name)
        {
            return root.Element(name)?.Value;
        }

        /// <summary>Colours are AARRGGBB; six digits (RRGGBB) are taken as opaque.</summary>
        public static bool TryParseColour(string text, out Color color)
        {
            color = Color.White;
            text = text?.Trim();
            if (text == null || (text.Length != 8 && text.Length != 6) ||
                !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
            {
                return false;
            }

            if (text.Length == 6)
            {
                value |= 0xFF000000;
            }

            color = new Color((int)(value >> 16 & 0xFF), (int)(value >> 8 & 0xFF), (int)(value & 0xFF), (int)(value >> 24 & 0xFF));
            return true;
        }

        static bool TryNumbers(string text, out float[] numbers)
        {
            numbers = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string[] parts = text.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);
            numbers = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i]) || !float.IsFinite(numbers[i]))
                {
                    numbers = null;
                    return false;
                }
            }

            return true;
        }

        public static bool TryParseArena(byte[] bytes, out Arena arena, out string reason)
        {
            arena = null;
            XElement root = Root(bytes, "Arena", out reason);
            if (root == null)
            {
                return false;
            }

            string name = CleanText(Text(root, "Name"), MaxNameLength);
            if (name.Length == 0)
            {
                reason = "The arena has no Name.";
                return false;
            }

            if (!TryParseColour(Text(root, "Color"), out Color color))
            {
                reason = "The Color is missing or is not hex digits (AARRGGBB).";
                return false;
            }

            if (!TryNumbers(Text(root, "Walls"), out float[] n))
            {
                reason = "The Walls are missing, or hold something that is not a number.";
                return false;
            }

            if (n.Length == 0 || n.Length % 4 != 0)
            {
                reason = "Walls holds " + n.Length + " numbers; it needs four for each wall (x1 y1 x2 y2).";
                return false;
            }

            if (n.Length / 4 > MaxWalls)
            {
                reason = "The arena has " + (n.Length / 4) + " walls; the most accepted is " + MaxWalls + ".";
                return false;
            }

            var walls = new Vector2[n.Length / 2];
            for (int i = 0; i < walls.Length; i++)
            {
                walls[i] = new Vector2(Math.Clamp(n[i * 2], 0f, 1f), Math.Clamp(n[i * 2 + 1], 0f, 1f));
            }

            arena = new Arena
            {
                Name = name,
                Color = color,
                Walls = walls,
                Id = IdOf(bytes),
                Author = CleanText(Text(root, "Author"), MaxNameLength),
                Description = CleanText(Text(root, "Description"), 300),
                SourceBytes = bytes,
            };
            return true;
        }

        public static bool TryParseShip(byte[] bytes, out Ship ship, out string reason)
        {
            ship = null;
            XElement root = Root(bytes, "Ship", out reason);
            if (root == null)
            {
                return false;
            }

            string name = CleanText(Text(root, "Name"), MaxNameLength);
            if (name.Length == 0)
            {
                reason = "The ship has no Name.";
                return false;
            }

            if (!TryParseColour(Text(root, "Color"), out Color color))
            {
                reason = "The Color is missing or is not hex digits (AARRGGBB).";
                return false;
            }

            if (!TryWhole(Text(root, "Mass"), 1, 100, out int mass))
            {
                reason = "Mass is missing, or not a number from 1 to 100.";
                return false;
            }

            if (!TryWhole(Text(root, "Radius"), 5, 40, out int radius))
            {
                reason = "Radius is missing, or not a number from 5 to 40.";
                return false;
            }

            if (!TryNumbers(root.Element("Shape")?.Element("Points")?.Value, out float[] n) || n.Length % 2 != 0)
            {
                reason = "Shape/Points is missing, odd, or holds something that is not a number.";
                return false;
            }

            int count = n.Length / 2;
            if (count < MinShipPoints || count > MaxShipPoints)
            {
                reason = "The outline has " + count + " points; a ship needs " + MinShipPoints + " to " + MaxShipPoints + ".";
                return false;
            }

            var points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                points[i] = new Vector2(n[i * 2], n[i * 2 + 1]);
                if (points[i].Length() > MaxShipReach)
                {
                    reason = "Point " + (i + 1) + " is " + (int)points[i].Length() + " px from the centre; ships may reach " + (int)MaxShipReach + ".";
                    return false;
                }
            }

            if (!TryParseThrusters(root, out Thruster[] thrusters, out reason))
            {
                return false;
            }

            ship = new Ship
            {
                Name = name,
                Thrusters = thrusters,
                Color = color,
                Mass = mass,
                Radius = radius,
                Shape = new VectorPolygon(points),
                // a community ship is always a spare: the numbered ones (and their controllers) belong to the ships that come with the game
                PlayerIndex = string.Empty,
                Id = IdOf(bytes),
                Author = CleanText(Text(root, "Author"), MaxNameLength),
                Description = CleanText(Text(root, "Description"), 300),
                SourceBytes = bytes,
            };
            return true;
        }

        /// <summary>
        /// The ship's optional &lt;Thrusters&gt;: each &lt;Thruster X="" Y="" Size=""/&gt;, up to three. No element at all gives null (the game then puts
        /// a flame at the outline's rearmost points); an empty one gives a ship with no flame. Size is optional and kept within 0.5 to 2.
        /// </summary>
        public static bool TryParseThrusters(XElement root, out Thruster[] thrusters, out string reason)
        {
            thrusters = null;
            reason = null;
            XElement list = root.Element("Thrusters");
            if (list == null)
            {
                return true;
            }

            var found = new List<Thruster>();
            foreach (XElement element in list.Elements("Thruster"))
            {
                if (found.Count == MaxThrusters)
                {
                    reason = "A ship may have at most " + MaxThrusters + " thrusters.";
                    return false;
                }

                if (!TryFloat((string)element.Attribute("X"), out float x) || !TryFloat((string)element.Attribute("Y"), out float y))
                {
                    reason = "Thruster " + (found.Count + 1) + " needs numbers for X and Y.";
                    return false;
                }

                if (new Vector2(x, y).Length() > MaxShipReach)
                {
                    reason = "Thruster " + (found.Count + 1) + " is " + (int)new Vector2(x, y).Length() + " px from the centre; ships may reach " + (int)MaxShipReach + ".";
                    return false;
                }

                float size = 1f;
                string sizeText = (string)element.Attribute("Size");
                if (sizeText != null && !TryFloat(sizeText, out size))
                {
                    reason = "Thruster " + (found.Count + 1) + " has a Size that is not a number.";
                    return false;
                }

                found.Add(new Thruster(new Vector2(x, y), Math.Min(MaxThrusterSize, Math.Max(MinThrusterSize, size))));
            }

            thrusters = found.ToArray();
            return true;
        }

        static bool TryFloat(string text, out float value)
        {
            return float.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value);
        }

        static bool TryWhole(string text, int min, int max, out int value)
        {
            value = 0;
            if (!float.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float f) || !float.IsFinite(f))
            {
                return false;
            }

            value = (int)Math.Round(f);
            return value >= min && value <= max;
        }

        #endregion
    }
}
