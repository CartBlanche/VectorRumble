using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Xna.Framework.Content;

namespace VectorRumble
{
    internal class PlayerManager 
    {
        const string playerDirectory = "Player";
        private readonly World world;

        public PlayerManager(World world)
        {
            this.world = world;
        }

        public Player Player { get; private set; }

        internal void LoadContent(ContentManager content)
        {
            var playeFiles = Helper.GetFilesFromFolders(new string[] { content.RootDirectory, playerDirectory }, "*.xml");

            for (int i = 0; i < playeFiles.Length; i++)
            {
                using (XmlReader reader = XmlReader.Create(playeFiles[i]))
                {
                    Player = Player.Load(XDocument.Load(playeFiles[i]).XPathSelectElement("/XnaContent/Asset"));
                    Player.World = world;
                }
            }
        }
    }
}
