using System;
using System.Diagnostics;
using System.IO;

namespace VectorEngine.Networking
{
    /// <summary>
    /// A small log of what happened on the network (who joined or left, why a session ended),
    /// written to {game}-net.log in the temp folder. A packaged game has no console, so
    /// without this there is nothing to look at when a match ends unexpectedly.
    /// </summary>
    public static class NetLog
    {
        static readonly object sync = new object();

        /// <summary>
        /// Where the log is written
        /// </summary>
        public static string Path => System.IO.Path.Combine(System.IO.Path.GetTempPath(), gameName + "-net.log");

        static string gameName = "VectorEngine";

        /// <summary>Names the log after the game (the file is <c>{gameName}-net.log</c> in the temp folder).</summary>
        public static void Configure(string name)
        {
            gameName = name;
        }

        public static void Write(string message)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message;
            Debug.WriteLine("[NET] " + message);

            try
            {
                lock (sync)
                    File.AppendAllText(Path, line + Environment.NewLine);
            }
            catch (Exception)
            {
                // a log that cannot be written must never stop the game
            }
        }
    }
}
