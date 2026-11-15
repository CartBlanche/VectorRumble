using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace VectorRumble
{
    internal class ArenaManager
    {
        const string arenaDirectory = "Arenas";
        private readonly World world;

        private Arena[] arenas;
        public Arena[] Arenas { get { return arenas; } }

        /// <summary>
        /// The arenas that come with the game, and "Random" after them (the rest are the player's
        /// own). Only these are the same on every machine, so only these can be played in a network match.
        /// </summary>
        public Arena[] BuiltInArenas => arenas.Take(builtInCount).ToArray();

        private int builtInCount;

        public ArenaManager(World world)
        {
            this.world = world;
        }

        internal void LoadContent(ContentManager content)
        {
            var arenaFiles = Helper.GetFilesFromContent(arenaDirectory, "arenas.txt");
            var arenaLength = arenaFiles.Length; // Caching the value so that the property isn't accessed throughout the loop.

            arenas = new Arena[arenaLength + 1]; // Adding an extra slot for the Random Arena later.
            for (int i = 0; i < arenaLength; i++)
            {
                using (var stream = TitleContainer.OpenStream(arenaFiles[i]))
                {
                    var arena = Arena.Load(XDocument.Load(stream).XPathSelectElement("/XnaContent/Asset"));
                    arenas[i] = arena;
                }
            }

            // Add an extra element for our Random option.
            var randomArena = new Arena
            {
                Name = Strings.Arena_Random,
            };
            arenas[arenas.Length - 1] = randomArena;
            builtInCount = arenas.Length;

            // The player's own arenas: the files in the Arenas folder. A bad file is skipped (and reported in CommunityContent.Skipped).
            Helper.EnsureCommunityFolders();
            CommunityContent.ForgetSkipped("Arenas");
            var community = new List<Arena>();
            foreach (var (path, bytes, id) in CommunityContent.ReadFolders("Arenas"))
            {
                if (CommunityContent.TryParseArena(bytes, out Arena arena, out string reason))
                {
                    community.Add(arena);
                }
                else
                {
                    CommunityContent.Skip("Arenas", path, reason);
                }
            }

            // and the ones a host sent in this session, which stay until the game closes
            community.AddRange(received.Where(r => community.All(c => c.Id != r.Id)));
            arenas = arenas.Concat(community).ToArray();
        }

        private readonly List<Arena> received = new List<Arena>();

        /// <summary>The community arena with this id (from the Arenas folder or received from a host), or null.</summary>
        public Arena FindById(string id)
        {
            return string.IsNullOrEmpty(id) ? null : arenas?.FirstOrDefault(a => a.Id == id);
        }

        /// <summary>
        /// Keeps an arena a host sent. It is played and listed like any other but is not written to disk.
        /// </summary>
        public void AddReceived(Arena arena)
        {
            if (arena == null || FindById(arena.Id) != null)
            {
                return;
            }

            arena.Received = true;
            received.Add(arena);
            arenas = arenas.Append(arena).ToArray();
        }

        internal Arena Random()
        {
            // never the "Random" entry itself
            var choices = Arenas.Where(a => a.Walls != null).ToArray();
            return choices[System.Random.Shared.Next(choices.Length)];
        }
    }
}
