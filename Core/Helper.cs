using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    internal static class Helper
    {

        const string MyDocumentsDirectory = "Documents"; // Making this a constant in case we need to IFDEF this per platform later

        /// <summary>
        /// Where the game keeps what the player owns: on desktop the local application data folder (macOS ~/Library/Application Support,
        /// Windows %LOCALAPPDATA%, Linux ~/.local/share), the same place as SolidOps; on a phone the Documents area (on iOS the Files app
        /// shows it). Arenas and ships the player makes go in the "Arenas" and "Ships" folders under "Vector Rumble" in here.
        /// </summary>
        public static string GetMyDocumentsFolder ()
        {
            if (VectorEngine.Platform.IsMobile) {
                // Once our app is signed, we can't change it, so let's look in the Special "MyDocuments" folder for user created data
                var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (!myDocuments.ToLower().Contains("documents")) {
                    myDocuments = Path.Combine(myDocuments, MyDocumentsDirectory);
                }

                return myDocuments;
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        /// <summary>
        /// The folder the player's own arenas ("Arenas") or ships ("Ships") go in. The environment variables VR_ARENAS_DIR and
        /// VR_SHIPS_DIR name another folder (for testing).
        /// </summary>
        public static string GetCommunityFolder (string kind)
        {
            var other = Environment.GetEnvironmentVariable(kind == "Ships" ? "VR_SHIPS_DIR" : "VR_ARENAS_DIR");
            if (!string.IsNullOrWhiteSpace(other)) {
                return other;
            }

            return Path.Combine(GetMyDocumentsFolder(), GetAssemblyTitle(), kind);
        }

        /// <summary>
        /// Where earlier versions looked for them on desktop (the Documents folder). Still read, so nothing made before is lost, but
        /// nothing is written there. Null when it is the same as the current folder (a phone) or a test folder is in use.
        /// </summary>
        public static string GetLegacyCommunityFolder (string kind)
        {
            if (VectorEngine.Platform.IsMobile || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(kind == "Ships" ? "VR_SHIPS_DIR" : "VR_ARENAS_DIR"))) {
                return null;
            }

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return string.IsNullOrEmpty(documents) ? null : Path.Combine(documents, GetAssemblyTitle(), kind);
        }

        /// <summary>
        /// Makes the folders the player puts arenas and ships in, so they can be found (and a new player can drop files in at once).
        /// Never throws: a folder that cannot be made just stays missing.
        /// </summary>
        public static void EnsureCommunityFolders ()
        {
            foreach (var kind in new[] { "Arenas", "Ships" }) {
                try {
                    Directory.CreateDirectory(GetCommunityFolder(kind));
                }
                catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine("Could not make the " + kind + " folder: " + ex.Message);
                }
            }
        }

        public static string GetAssemblyTitle ()
        {
            var assembly = typeof(Arena).Assembly;
            var assemblyAttributes = (AssemblyTitleAttribute[])assembly.GetCustomAttributes(typeof(AssemblyTitleAttribute), true);
            if (assemblyAttributes.Length > 0) {
                return assemblyAttributes[0].Title;
            }
            else {
                return string.Empty;
            }
        }

        /// <summary>
        /// Get files from the Content directory using TitleContainer.
        /// This is a more cross-platform way to access content files.
        /// </summary>
        /// <param name="contentDirectory">The subdirectory within Content (e.g., "Ships", "Arenas", etc)</param>
        /// <param name="manifestFileName">The manifest file listing all files (e.g., "ships.txt")</param>
        /// <returns>Array of paths that we can then use with TitleContainer.OpenStream()</returns>
        public static string[] GetFilesFromContent(string contentDirectory, string manifestFileName)
        {
            var manifestPath = Path.Combine("Content", contentDirectory, manifestFileName);

            try
            {
                using (var manifestStream = TitleContainer.OpenStream(manifestPath))
                using (var manifestStreamReader = new StreamReader(manifestStream))
                {
                    var files = new List<string>();
                    string line;
                    while ((line = manifestStreamReader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (!string.IsNullOrEmpty(line))
                        {
                            // Return the full content path
                            files.Add(Path.Combine("Content", contentDirectory, line));
                        }
                    }
                    return files.ToArray();
                }
            }
            catch (FileNotFoundException)
            {
                // Manifest file doesn't exist
                return null;
            }
        }

        public static string[] GetFilesFromFolders (string [] folders, string extensionFilter)
        {
            string root = Path.GetDirectoryName (System.AppContext.BaseDirectory);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                string resourceFolder = Path.Combine (root, "..", "..", "Resources");
                if (Directory.Exists(resourceFolder)) {
                    root = resourceFolder;
                }
                resourceFolder = Path.Combine (root, "..", "Resources");
                if (Directory.Exists(resourceFolder)) {
                    root = resourceFolder;
                }
            }
            var directory = Path.Combine (root, Path.Combine(folders));
            if (Directory.Exists(directory)) {
                return Directory.GetFiles(directory, extensionFilter);
            }
            else {
                return null;
            }
        }

        public static Color ColorParse (string colourString)
        {
            var value = uint.Parse (colourString, System.Globalization.NumberStyles.HexNumber);
            return new Color ((int)(value >> 16 & 0xFF),
                            (int)(value >> 8 & 0xFF),
                            (int)(value >> 0 & 0xFF),
                            (int)(value >> 24 & 0xFF));
        }
    }
}
