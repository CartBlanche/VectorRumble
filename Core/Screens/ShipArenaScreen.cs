using System;
using System.Linq;
using Microsoft.Xna.Framework;
using VectorRumble.Networking;

namespace VectorRumble
{
    /// <summary>
    /// The last step before a match: which ship, in which colour, in which arena. The ship turns in its hangar and the
    /// arena is drawn from its own walls, so there are no pictures to ship.
    ///
    /// On this machine alone the Next link reads Start. In a network game the same screen is the waiting room: it lists
    /// who is in, the host picks the arena (everyone else sees it locked) and starts the match, and everyone else says
    /// when they are Ready.
    /// </summary>
    class ShipArenaScreen : FrameMenuScreen
    {
        static readonly string[] playerNames = { "One", "Two", "Three", "Four" };

        // what was chosen last time is what is offered next time
        static int shipIndex;
        static int colourIndex;

        readonly NetworkManager net;
        LanLobby lobby;                         // null on this machine alone, and once the match has started or the player has left
        MatchStartPacket pendingStart;          // the match to start as soon as the screen can change
        string notice;                          // why the host cannot start yet, for instance

        bool Hosting => net != null && net.IsHost;
        bool Joined => net != null && !net.IsHost;

        /// <summary>A game on this machine alone.</summary>
        public ShipArenaScreen() : this(null)
        {
        }

        /// <param name="net">The session of a network game. The screen takes it over, and ends it if the player leaves.</param>
        public ShipArenaScreen(NetworkManager net)
        {
            this.net = net;
            Heading = Strings.Ship_And_Arena;
            Layout = FrameLayout.Rows;
            ShowPanel = false;

            var ship = new FrameRow(Strings.Label_Ship, FrameRowKind.Choice) { Value = () => CurrentShip.Name };
            ship.Adjusted += d =>
            {
                shipIndex = Wrap(shipIndex + d, ShipCount);
                TellLobbyAboutShip();
            };
            Rows.Add(ship);

            var colour = new FrameRow(Strings.Label_Colour, FrameRowKind.Choice)
            {
                Value = () => ShipPalette.Name(colourIndex),
                Adornment = (canvas, at) => canvas.Polygon(CurrentShip.Shape.Points, at, MathF.PI / 2f, 11f / Reach(CurrentShip), CurrentColour, 2f),
            };
            colour.Adjusted += ChangeColour;
            Rows.Add(colour);

            // the host (and a game on this machine) picks the arena; everyone else in a network game sees it, locked
            var arena = new FrameRow(Strings.Label_Arena, FrameRowKind.Choice)
            {
                Value = () => ArenaName,
                Locked = () => Joined,
                LockedNote = Strings.Label_HostsPick,
            };
            arena.Adjusted += ChangeArena;
            Rows.Add(arena);

            Next = new FrameNext
            {
                Label = () => Joined ? (lobby != null && lobby.LocalReady ? Strings.Hint_NotReady : Strings.Hint_Ready) : Strings.Hint_Start,
                Step = net == null ? 2 : Hosting ? 3 : 2,
                Steps = net == null ? 2 : Hosting ? 3 : 2,
                Action = Joined ? ToggleReady : Hosting ? StartNetworkMatch : (Action)StartAlone,
            };
        }

        // the waiting room of a network game goes on while the player waits for the others
        internal override bool AllowsIdlePause => net == null;

        // the hangar and the arena take the right-hand side
        protected override float RowsWidth => 600f;

        public override void LoadContent()
        {
            // the ships a player can choose between are loaded fresh for each game
            World.ShipManager.LoadContent(ScreenManager.Game.Content);
            shipIndex = Math.Min(shipIndex, ShipCount - 1);

            // an arena chosen last time may be gone from the folder
            if (net == null || Hosting)
            {
                if (PreviewPainter.FindArena(MatchRules.Current.Arena, MatchRules.Current.ArenaId) == null)
                {
                    SetArena(null);
                }
            }

            if (net != null)
            {
                // the host picks "Random" among the arenas that come with the game: only they are on every machine
                string[] arenas = World.ArenaManager.BuiltInArenas
                    .Select(a => a.Name).Where(n => n != Strings.Arena_Random).ToArray();

                // the community arenas and ships on this machine can be sent to the others, which also have them by id
                ShipManager manager = World.ShipManager;
                ArenaManager arenaManager = World.ArenaManager;
                var files = new SharedFiles(id => arenaManager.FindById(id)?.SourceBytes ?? manager.Ships.FirstOrDefault(s => s.Id == id)?.SourceBytes);

                lobby = new LanLobby(net, World.ShipManager.BuiltInCount, arenas, MatchRules.Current, files);
                lobby.MatchStarting += start => pendingStart = start;
                lobby.FileReceived += OnFileReceived;
                TellLobbyAboutShip();
                lobby.SetLocalColor(ShipPalette.Colours[colourIndex]);
            }

            base.LoadContent();
        }

        #region Choices

        // a community ship is offered in a network game too: the other players are sent its file
        int ShipCount => World.ShipManager.Ships.Length;

        // the lobby knows a ship that comes with the game by number, a community ship by its file's id
        void TellLobbyAboutShip()
        {
            if (lobby == null)
            {
                return;
            }

            if (shipIndex >= World.ShipManager.BuiltInCount)
            {
                lobby.SetLocalShip(0, CurrentShip.Id);
            }
            else
            {
                lobby.SetLocalShip(shipIndex);
            }
        }

        // a community arena came over the network: it is played like any other
        void OnFileReceived(SharedFileKind kind, string id, byte[] bytes)
        {
            if (kind == SharedFileKind.Arena && CommunityContent.TryParseArena(bytes, out Arena arena, out _))
            {
                World.ArenaManager.AddReceived(arena);
            }
        }

        // what a community ship looks like to the others in the waiting room, worked out once from its file
        readonly System.Collections.Generic.Dictionary<string, Vector2[]> lobbyShapes = new System.Collections.Generic.Dictionary<string, Vector2[]>();

        Vector2[] LobbyShape(string shipId, int index)
        {
            if (string.IsNullOrEmpty(shipId))
            {
                return World.ShipManager.Ships[Math.Min(index, World.ShipManager.BuiltInCount - 1)].Shape.Points;
            }

            if (!lobbyShapes.TryGetValue(shipId, out Vector2[] shape))
            {
                byte[] bytes = lobby?.Files.Get(shipId);
                shape = bytes != null && CommunityContent.TryParseShip(bytes, out Ship parsed, out _) ? parsed.Shape.Points : null;
                if (shape != null)
                {
                    lobbyShapes[shipId] = shape;
                }
            }

            return shape; // null while the file has not come yet
        }

        static int Wrap(int value, int count) => ((value % count) + count) % count;

        static Ship CurrentShip => World.ShipManager.Ships[shipIndex];

        static Color CurrentColour => ShipPalette.Colours[colourIndex];

        static float Reach(Ship ship) => Math.Max(1f, ship.Shape.Points.Max(p => p.Length()));

        // the host's choice when we are joining, ours otherwise
        string ArenaName => Joined && lobby != null ? lobby.Rules.Arena : MatchRules.Current.Arena;
        string ArenaId => Joined && lobby != null ? lobby.Rules.ArenaId : MatchRules.Current.ArenaId;

        // the arena the rules name: an arena, or null for "Random"
        void SetArena(Arena arena)
        {
            bool random = arena == null || arena.Walls == null;
            MatchRules.Current.Arena = random ? Strings.Arena_Random : arena.Name;
            MatchRules.Current.ArenaId = random ? string.Empty : arena.Id ?? string.Empty;
        }

        bool ColourTakenByOthers(int index)
        {
            if (lobby == null)
            {
                return false;
            }

            for (int i = 0; i < lobby.Players.Count; i++)
            {
                if (i != lobby.LocalSlot && lobby.Players[i].Color == ShipPalette.Colours[index])
                {
                    return true;
                }
            }

            return false;
        }

        void ChangeColour(int direction)
        {
            int count = ShipPalette.Colours.Length;
            for (int step = 1; step <= count; step++)
            {
                int candidate = Wrap(colourIndex + direction * step, count);
                if (!ColourTakenByOthers(candidate))
                {
                    colourIndex = candidate;
                    break;
                }
            }

            lobby?.SetLocalColor(CurrentColour);
        }

        void ChangeArena(int direction)
        {
            if (Joined)
            {
                return;
            }

            // the arenas that come with the game, "Random", and the player's own: a network game sends a community arena to the others
            Arena[] list = World.ArenaManager.Arenas;
            int index = Array.FindIndex(list, a => a.Name == MatchRules.Current.Arena && (a.Id ?? string.Empty) == (MatchRules.Current.ArenaId ?? string.Empty));
            SetArena(list[Wrap(index + direction, list.Length)]);

            if (lobby != null)
            {
                lobby.SetRules(MatchRules.Current.Clone());
            }
        }

        #endregion

        #region Starting

        /// <summary>
        /// Puts the player's ship, alone, into the match and loads the game. A single player game has one pilot on
        /// this machine, flying with whatever they have to hand (the keyboard, a gamepad or the touch controls).
        /// </summary>
        void StartAlone()
        {
            ShipManager manager = World.ShipManager;
            foreach (Ship ship in manager.Ships)
            {
                ship.PlayerIndex = string.Empty;
                ship.Selected = false;
                ship.Pilot = null;
            }

            Ship pilot = CurrentShip;
            pilot.PlayerIndex = "One";
            pilot.PilotName = LanLobby.DisplayName(Environment.UserName);
            pilot.SetColor(CurrentColour);
            pilot.Controls = ShipControls.ForLocalPlayer();
            pilot.Selected = true;
            AddComputerPilots(manager, pilot);
            manager.UpdateSelectedList();

            // a new match picks its arena afresh (and a "Random" arena picks again)
            World.SelectedArena = null;
            World.Match = null;

            LoadingScreen.Load(ScreenManager, LoadGameplayScreen, true);
        }

        /// <summary>
        /// Puts the computer's pilots into the match: each flies one of the ships that come with the game (never the
        /// player's own), in a colour nobody else has, and takes the next player slot.
        /// </summary>
        void AddComputerPilots(ShipManager manager, Ship human)
        {
            int count = Math.Max(0, Math.Min(MatchRules.MaxAIPilots, MatchRules.Current.AIPilots));
            AIDifficulty difficulty = UserSettings.Current.AIDifficulty;
            int seed = Environment.TickCount;

            int nextShip = shipIndex;
            int nextColour = colourIndex;
            for (int i = 0; i < count; i++)
            {
                Ship ship = null;
                for (int tries = 0; tries < manager.BuiltInCount && ship == null; tries++)
                {
                    nextShip = Wrap(nextShip + 1, manager.BuiltInCount);
                    Ship candidate = manager.Ships[nextShip];
                    if (candidate != human && !candidate.Selected)
                    {
                        ship = candidate;
                    }
                }

                if (ship == null)
                {
                    break;
                }

                nextColour = Wrap(nextColour + 1, ShipPalette.Colours.Length);
                ship.PlayerIndex = playerNames[i + 1];
                ship.PilotName = string.Format(Strings.AI_Pilot_Name, i + 1);
                ship.SetColor(ShipPalette.Colours[nextColour]);
                ship.Controls = null;
                ship.Pilot = new AIPilot(ship, World, difficulty, seed + i);
                ship.Selected = true;
            }
        }

        void ToggleReady()
        {
            lobby?.SetLocalReady(!lobby.LocalReady);
        }

        // the host is ready when they start
        void StartNetworkMatch()
        {
            if (lobby == null)
            {
                return;
            }

            lobby.SetLocalReady(true);
            notice = lobby.TryStart() ? null : Strings.Lan_Waiting_For_Players;
            if (notice != null)
            {
                // not started: the host is not ready until they say so again
                lobby.SetLocalReady(false);
            }
        }

        // every machine builds the same match from the host's start packet
        void StartMatch(MatchStartPacket start)
        {
            int slot = start.Players.FindIndex(p => p.Id == net.LocalGamer.GamerId);
            if (slot < 0)
            {
                Leave(Strings.Lan_Not_In_Match);
                return;
            }

            MatchRules.Current = start.Rules;

            // A ship that comes with the game flies for one player: if two chose the same, the later one gets the next along.
            // A community ship is made fresh from its file for each player who chose it (the file came from them or from this machine).
            ShipManager manager = World.ShipManager;
            byte[][] communityFiles = start.Players.Select(p => string.IsNullOrEmpty(p.ShipId) ? null : lobby?.Files.Get(p.ShipId)).ToArray();
            int[] assignment = ShipAssignment.Assign(start.Players.Select(p => (int)p.Ship).ToArray(), manager.BuiltInCount);

            foreach (Ship ship in manager.Ships)
            {
                ship.PlayerIndex = string.Empty;
                ship.Selected = false;
                ship.Pilot = null;
            }

            Ship[] ships = new Ship[start.Players.Count];
            for (int i = 0; i < ships.Length; i++)
            {
                // a community choice that cannot be made (no file) falls back to the ship that comes with the game
                Ship ship = communityFiles[i] != null ? manager.MakeCommunityShip(communityFiles[i]) : null;
                ship ??= manager.Ships[assignment[i]];
                ship.PlayerIndex = playerNames[i];
                ship.PilotName = start.Players[i].Name;
                ship.SetColor(start.Players[i].Color);
                ship.Selected = true;
                ships[i] = ship;
            }
            manager.UpdateSelectedList();

            World.SelectedArena = null;

            // the match takes the session over from here, and ends it when the game is left
            new NetworkMatch(World, net, start, slot, ships);
            DisposeLobby();

            LoadingScreen.Load(ScreenManager, LoadGameplayScreen, true);
        }

        void LoadGameplayScreen(object sender, EventArgs e)
        {
            ScreenManager.AddScreen(new GameplayScreen { ScreenManager = this.ScreenManager });
        }

        #endregion

        #region Update and leaving

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

            if (lobby == null)
            {
                return;
            }

            net.Update();
            lobby.Update(gameTime.ElapsedGameTime.TotalSeconds);

            // the host's colour stands: a player who joins in the same colour moves along to a free one
            if (Joined && ColourTakenByOthers(colourIndex))
            {
                ChangeColour(1);
            }

            if (pendingStart != null)
            {
                StartMatch(pendingStart);
            }
            else if (lobby.WasRefused)
            {
                Leave(Strings.Lan_Already_Started);
            }
            else if (net.HasEnded)
            {
                Leave(Strings.Lan_Host_Ended);
            }
        }

        protected override void OnCancel()
        {
            if (net != null)
            {
                Leave(null);
            }
            else
            {
                ExitScreen();
            }
        }

        // go back, ending the session
        void Leave(string message)
        {
            DisposeLobby();
            net.Dispose();
            ExitScreen();

            if (message != null)
            {
                ScreenManager.AddScreen(new MessageBoxScreen(message));
            }
        }

        void DisposeLobby()
        {
            lobby?.Dispose();
            lobby = null;
            pendingStart = null;
        }

        #endregion

        #region The panel: hangar above, arena below, and who is in at the bottom left

        static void Box(FrameCanvas c, float x, float y, float w, float h)
        {
            var frame = FrameCanvas.Chamfer(x, y, w, h, 26);
            c.Fill(frame, new Color(8, 8, 26), 0.5f);
            c.Poly(frame, true, FrameScreen.Theme.Panel, 1.8f, .7f);
        }

        // each player's choices as the lobby knows them; our own follow our changes at once
        (string name, int ship, string shipId, Color colour, bool ready) PlayerAt(int i)
        {
            LobbyPlayerInfo p = lobby.Players[i];
            if (i == lobby.LocalSlot)
            {
                return (p.Name, lobby.LocalShip, lobby.LocalShipId, lobby.LocalColor, lobby.LocalReady);
            }

            return (p.Name, p.Ship, p.ShipId, p.Color, p.Ready);
        }

        protected override void DrawBody(FrameCanvas c)
        {
            const float x = 740, w = 440;
            Box(c, x, 196, w, 190);
            Box(c, x, 404, w, 156);

            // the ship, turning slowly in its colour
            Ship ship = CurrentShip;
            c.Polygon(ship.Shape.Points, new Vector2(x + w / 2, 291), Time * 0.7f, 52f / Reach(ship), CurrentColour, 3f);

            // the arena, from its own walls
            Arena arena = PreviewPainter.FindArena(ArenaName, ArenaId);
            if (arena?.Walls != null && arena.Walls.Length >= 2)
            {
                const float ix = x + 90, iy = 420, iw = w - 180, ih = 124;
                for (int i = 0; i + 1 < arena.Walls.Length; i += 2)
                {
                    Vector2 a = new Vector2(ix + arena.Walls[i].X * iw, iy + arena.Walls[i].Y * ih);
                    Vector2 b = new Vector2(ix + arena.Walls[i + 1].X * iw, iy + arena.Walls[i + 1].Y * ih);
                    c.Neon(a, b, arena.Color, 2f);
                }
            }

            // who is in: a small ship in each player's colour
            if (lobby != null)
            {
                for (int i = 0; i < lobby.Players.Count; i++)
                {
                    var p = PlayerAt(i);
                    Vector2[] theirs = LobbyShape(p.shipId, p.ship);
                    if (theirs != null)
                    {
                        c.Polygon(theirs, new Vector2(100, 520 + i * 36), MathF.PI / 2f, 12f / Math.Max(1f, theirs.Max(v => v.Length())), p.colour, 2f);
                    }
                }
            }
        }

        protected override void DrawBodyText(FrameCanvas c)
        {
            var theme = FrameScreen.Theme;
            const float x = 740, w = 440;

            Arena arena = PreviewPainter.FindArena(ArenaName, ArenaId);
            if (arena?.Walls == null || arena.Walls.Length < 2)
            {
                // a "Random" arena has no walls to show
                c.Text(c.Display, "?", new Vector2(x + w / 2, 458), theme.Text, 48f, FrameAlign.Centre);
            }

            if (lobby == null)
            {
                return;
            }

            c.Text(c.Display, Strings.Label_Players.ToUpperInvariant(), new Vector2(86, 470), theme.Dim, 14f);
            for (int i = 0; i < lobby.Players.Count; i++)
            {
                var p = PlayerAt(i);
                float y = 520 + i * 36 - 11;
                c.Text(c.Medium, LanLobby.DisplayName(p.name), new Vector2(126, y), theme.Text, 22f);
                c.Text(c.Display, p.ready ? Strings.Value_Ready.ToUpperInvariant() : "...", new Vector2(640, y + 3), p.ready ? theme.Hot : theme.Dim, 15f, FrameAlign.Right);
            }

            // why the match cannot start, or what the others are doing
            // (and while an arena or ship from another player is on its way: it only takes a moment)
            string status = notice ?? (lobby.MissingFiles().Count > 0 ? Strings.Loading : Joined ? Strings.Lan_Waiting_For_Host : null);
            if (status != null)
            {
                c.Text(c.Large, status, new Vector2(x + w / 2, 580), theme.Dim, 17f, FrameAlign.Centre);
            }
        }

        #endregion
    }
}
