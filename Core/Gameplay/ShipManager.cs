using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace VectorRumble
{
    internal class ShipManager 
    {
        const string shipDirectory = "Ships";
        private readonly World world;
        private Ship[] ships;
        public List<Ship> SelectedPlayers = new List<Ship>();

        /// <summary>
        /// How many of the ships come with the game (the rest are the player's own). Only these are the
        /// same on every machine, so only these can be chosen in a network match.
        /// </summary>
        public int BuiltInCount { get; private set; }

        public ShipManager(World world)
        {
            this.world = world;
        }

        public Ship[] AvailableShips => Ships.Where(s => !string.IsNullOrEmpty(s.PlayerIndex)).OrderBy(p => p.PlayerStringToIndex).ToArray();
        public Ship[] Players => Ships.Where(s => s.Playing).ToArray();
        public Ship[] Ships { get { return ships; } }

        public Ship[] SpareShips => Ships.Where(s => string.IsNullOrEmpty(s.PlayerIndex)).ToArray();

        internal void LoadContent(ContentManager content)
        {
            var shipFiles = Helper.GetFilesFromContent(shipDirectory, "ships.txt");
            ships = new Ship[shipFiles.Length];
            for (int i = 0; i < shipFiles.Length; i++)
            {
                using (var stream = TitleContainer.OpenStream(shipFiles[i]))
                {
                    var ship = Ship.Load(XDocument.Load(stream).XPathSelectElement("/XnaContent/Asset"));
                    ship.World = world;
                    ship.ApplyScale();
                    ships[i] = ship;
                }
            }

            BuiltInCount = ships.Length;

            // The player's own ships: the files in the Ships folder. A bad file is skipped (and reported in CommunityContent.Skipped).
            Helper.EnsureCommunityFolders();
            CommunityContent.ForgetSkipped("Ships");
            var community = new List<Ship>();
            foreach (var (path, bytes, id) in CommunityContent.ReadFolders("Ships"))
            {
                if (CommunityContent.TryParseShip(bytes, out Ship ship, out string reason))
                {
                    ship.World = world;
                    ship.ApplyScale();
                    community.Add(ship);
                }
                else
                {
                    CommunityContent.Skip("Ships", path, reason);
                }
            }

            ships = ships.Concat(community).ToArray();
        }

        /// <summary>
        /// A new ship made from a community ship's file, for one player of a network match. Players can choose the same community
        /// ship, and a ship can fly for only one player, so each gets a ship of their own. Null if the file is not a ship.
        /// </summary>
        public Ship MakeCommunityShip(byte[] bytes)
        {
            if (!CommunityContent.TryParseShip(bytes, out Ship ship, out string reason))
            {
                System.Diagnostics.Debug.WriteLine("[Community] A ship from the network was refused: " + reason);
                return null;
            }

            ship.World = world;
            ship.ApplyScale();
            ships = ships.Append(ship).ToArray();
            return ship;
        }

        public void UpdateSelectedList()
        {
            SelectedPlayers.Clear();
            foreach (var ship in Ships)
            {
                if (ship.Selected)
                {
                    SelectedPlayers.Add(ship);
                }
            }
        }
    }
}
