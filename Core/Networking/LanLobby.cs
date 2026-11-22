using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Net;

namespace VectorRumble.Networking
{
    /// <summary>
    /// The lobby state for a LAN game. The host owns it: clients tell the host what they
    /// want (ship, colour, ready) and the host sends everybody the resulting lobby and the
    /// rules of the match. Slot order (host first, then in joining order) becomes the player
    /// index in the match.
    /// 
    /// This class has no UI so it can be tested without the game.
    /// </summary>
    public sealed class LanLobby : IDisposable
    {
        readonly NetworkManager net;
        readonly int shipCount;
        readonly IList<string> arenas;
        readonly List<LobbyPlayerInfo> players = new List<LobbyPlayerInfo>();
        readonly SharedFiles files;
        MatchRules rules;
        bool disposed;

        // community files: what has been asked for and when, and what the host was last told about us
        readonly Dictionary<string, double> asked = new Dictionary<string, double>();
        readonly HashSet<string> uploaded = new HashSet<string>();
        double clock;
        bool reportedHasFiles = true;
        const double AskAgainAfter = 2.0;

        // this player's own choices; what we send is always based on these, never on the
        // lobby the host last echoed back (which lags behind quick changes)
        byte localShip;
        string localShipId = string.Empty;
        Color localColor = Color.White;
        bool localReady;

        // raised from NetworkManager.Update (game thread)
        public event Action Changed;
        public event Action<MatchStartPacket> MatchStarting;
        public event Action JoinRefused;                    // we arrived after the match started

        /// <summary>A community arena or ship came over the network and is now kept: its kind, id and bytes.</summary>
        public event Action<SharedFileKind, string, byte[]> FileReceived;

        /// <summary>
        /// True once the host has told us we arrived after the match started (the event can fire
        /// while no screen is listening, for instance during ship selection)
        /// </summary>
        public bool WasRefused { get; private set; }

        /// <param name="shipCount">How many ships a player can choose between</param>
        /// <param name="arenas">The names of the arenas the host can pick for a "Random" match (not "Random" itself)</param>
        /// <param name="rules">The rules to start with (the host's; a client's are replaced by the host's)</param>
        /// <param name="files">The community arenas and ships this machine has or has been sent; null for none</param>
        public LanLobby(NetworkManager net, int shipCount, IList<string> arenas, MatchRules rules, SharedFiles files = null)
        {
            this.net = net;
            this.shipCount = shipCount;
            this.arenas = arenas;
            this.rules = rules.Clone();
            this.files = files ?? new SharedFiles();

            if (net.IsHost)
            {
                net.Dispatcher.Register(PacketType.LobbyUpdate, PacketAudience.FromClient, OnLobbyUpdate);
                net.Dispatcher.Register(PacketType.FileRequest, PacketAudience.FromClient, OnFileRequestFromClient);
                net.Dispatcher.Register(PacketType.FileUpload, PacketAudience.FromClient, OnFileUpload);
                net.GamerJoined += OnGamerJoined;
                net.GamerLeft += OnGamerLeft;

                AddPlayer(net.LocalGamer);
                foreach (NetworkGamer gamer in net.Session.RemoteGamers)
                    AddPlayer(gamer);
            }
            else
            {
                net.Dispatcher.Register(PacketType.LobbySync, PacketAudience.FromHost, OnLobbySync);
                net.Dispatcher.Register(PacketType.MatchStart, PacketAudience.FromHost, OnMatchStart);
                net.Dispatcher.Register(PacketType.JoinRefused, PacketAudience.FromHost, OnJoinRefused);
                net.Dispatcher.Register(PacketType.FileData, PacketAudience.FromHost, OnFileData);
                net.Dispatcher.Register(PacketType.FileRequest, PacketAudience.FromHost, OnFileRequestFromHost);

                // let the host know who we are
                SendLocalChoice();
            }
        }

        public IList<LobbyPlayerInfo> Players
        {
            get { return players; }
        }

        /// <summary>
        /// The rules of the match. The host sets them with <see cref="SetRules"/>; a client sees the host's.
        /// </summary>
        public MatchRules Rules
        {
            get { return rules; }
        }

        /// <summary>
        /// Slot (player index) of this machine, or -1 before the host has told us
        /// </summary>
        public int LocalSlot
        {
            get
            {
                string id = net.LocalGamer.GamerId;
                for (int i = 0; i < players.Count; i++)
                    if (players[i].Id == id)
                        return i;
                return -1;
            }
        }

        public int LocalShip
        {
            get { return localShip; }
        }

        /// <summary>The id of the community ship this player chose, or empty for one that comes with the game.</summary>
        public string LocalShipId
        {
            get { return localShipId; }
        }

        /// <summary>The community arenas and ships known here (this machine's own and the ones sent over).</summary>
        public SharedFiles Files
        {
            get { return files; }
        }

        /// <summary>
        /// The ids of the community files this match needs: the arena the host chose and every player's ship.
        /// </summary>
        public List<string> NeededFiles()
        {
            var ids = new List<string>();
            if (!string.IsNullOrEmpty(rules.ArenaId) && !rules.IsRandomArena)
                ids.Add(rules.ArenaId);
            foreach (LobbyPlayerInfo player in players)
                if (!string.IsNullOrEmpty(player.ShipId) && !ids.Contains(player.ShipId))
                    ids.Add(player.ShipId);
            if (!string.IsNullOrEmpty(localShipId) && !ids.Contains(localShipId))
                ids.Add(localShipId);
            return ids;
        }

        /// <summary>The needed files this machine does not have yet (they are being fetched).</summary>
        public List<string> MissingFiles()
        {
            return NeededFiles().FindAll(id => !files.Has(id));
        }

        /// <summary>This machine has every community file the match needs.</summary>
        public bool HasAllFiles
        {
            get { return MissingFiles().Count == 0; }
        }

        public Color LocalColor
        {
            get { return localColor; }
        }

        public bool LocalReady
        {
            get { return localReady; }
        }

        public LobbyPlayerInfo LocalPlayer
        {
            get
            {
                int slot = LocalSlot;
                return slot < 0 ? null : players[slot];
            }
        }

        public bool IsHost
        {
            get { return net.IsHost; }
        }

        /// <summary>
        /// The host can start once at least two people are in and all of them are ready
        /// </summary>
        public bool CanStart
        {
            get
            {
                if (!net.IsHost || players.Count < 2)
                    return false;
                foreach (LobbyPlayerInfo player in players)
                    if (!player.Ready || !player.HasFiles)
                        return false;
                return HasAllFiles;
            }
        }

        #region Local actions

        /// <summary>
        /// Choose a ship: a number for one that comes with the game, or the id of a community ship (then the number is ignored).
        /// </summary>
        public void SetLocalShip(int ship, string shipId = null)
        {
            localShip = (byte)((ship % shipCount + shipCount) % shipCount);
            localShipId = shipId ?? string.Empty;
            ApplyLocalChoice();
        }

        public void SetLocalColor(Color color)
        {
            localColor = color;
            ApplyLocalChoice();
        }

        public void SetLocalReady(bool ready)
        {
            localReady = ready;
            ApplyLocalChoice();
        }

        void ApplyLocalChoice()
        {
            if (net.IsHost)
            {
                LobbyPlayerInfo me = LocalPlayer;
                if (me == null)
                    return;
                me.Ship = localShip;
                me.ShipId = localShipId;
                me.Color = localColor;
                me.HasFiles = HasAllFiles;
                me.Ready = localReady && me.HasFiles;
                SyncAll();
            }
            else
            {
                SendLocalChoice();
            }
        }

        void SendLocalChoice()
        {
            // the host needs the file of a community ship we fly: it is sent first, so it is there when the choice arrives
            if (localShipId.Length > 0 && !uploaded.Contains(localShipId))
            {
                byte[] bytes = files.Get(localShipId);
                if (bytes != null)
                {
                    uploaded.Add(localShipId);
                    net.SendToHost(new FileDataPacket { Packet = PacketType.FileUpload, Kind = SharedFileKind.Ship, Id = localShipId, Data = bytes }, true);
                }
            }

            reportedHasFiles = HasAllFiles;
            net.SendToHost(new LobbyUpdatePacket
            {
                Ship = localShip,
                ShipId = localShipId,
                Color = localColor,
                Ready = localReady && reportedHasFiles,
                HasFiles = reportedHasFiles,
            }, true);
        }

        /// <summary>
        /// Host only: change the rules of the match
        /// </summary>
        public void SetRules(MatchRules newRules)
        {
            if (!net.IsHost)
                return;

            rules = newRules.Clone();
            RefreshHostState();
            SyncAll();
        }

        /// <summary>
        /// Host only: begin the match on every machine
        /// </summary>
        public bool TryStart()
        {
            if (!CanStart)
                return false;

            // every machine plays the same arena, so a "Random" one is picked here, once
            MatchRules matchRules = rules.Clone();
            if (matchRules.IsRandomArena && arenas.Count > 0)
                matchRules.Arena = arenas[new Random().Next(arenas.Count)];

            MatchStartPacket start = new MatchStartPacket
            {
                Rules = matchRules,
                Seed = new Random().Next(),
                Sky = SkyPool.Pick(new Random()),
                Players = new List<LobbyPlayerInfo>(players),
            };

            // the library refuses new players once the session is playing; the property hides
            // the game from other players' lists (the library keeps advertising it)
            net.Session.AllowJoinInProgress = false;
            MultiplayerSessionService.MarkInProgress(net.Session);
            net.Session.StartGame();
            net.Broadcast(start, true);

            if (MatchStarting != null)
                MatchStarting(start);
            return true;
        }

        #endregion

        void OnJoinRefused(NetworkGamer sender, BinaryReader reader)
        {
            WasRefused = true;
            if (JoinRefused != null)
                JoinRefused();
        }

        #region Host

        void AddPlayer(NetworkGamer gamer)
        {
            if (gamer == null || FindById(gamer.GamerId) != null || players.Count >= MultiplayerSessionService.MaxPlayers)
                return;

            players.Add(new LobbyPlayerInfo
            {
                Id = gamer.GamerId,
                Name = DisplayName(gamer.Gamertag),
                Ship = 0,
                Color = Color.White,
                Ready = false,
            });
        }

        void OnGamerJoined(NetworkGamer gamer)
        {
            AddPlayer(gamer);
            SyncAll();
        }

        void OnGamerLeft(NetworkGamer gamer)
        {
            LobbyPlayerInfo left = FindById(gamer.GamerId);
            if (left != null)
            {
                players.Remove(left);
                SyncAll();
            }
        }

        void OnLobbyUpdate(NetworkGamer sender, BinaryReader reader)
        {
            LobbyUpdatePacket update = LobbyUpdatePacket.Deserialize(reader);

            LobbyPlayerInfo player = FindById(sender.GamerId);
            if (player == null)
                return;

            player.Ship = (byte)(update.Ship % shipCount);
            player.ShipId = update.ShipId;
            player.Color = update.Color;
            player.HasFiles = update.HasFiles;
            player.Ready = update.Ready && update.HasFiles;
            RefreshHostState();
            SyncAll();
        }

        // the host's own state follows what the match needs: the host may lack a file a player has just chosen
        void RefreshHostState()
        {
            LobbyPlayerInfo me = LocalPlayer;
            if (me == null)
                return;
            me.HasFiles = HasAllFiles;
            me.Ready = localReady && me.HasFiles;
        }

        void SyncAll()
        {
            net.Broadcast(new LobbySyncPacket
            {
                Rules = rules,
                Players = new List<LobbyPlayerInfo>(players),
            }, true);

            if (Changed != null)
                Changed();
        }

        #endregion

        #region Client

        void OnLobbySync(NetworkGamer sender, BinaryReader reader)
        {
            LobbySyncPacket sync = LobbySyncPacket.Deserialize(reader);

            players.Clear();
            players.AddRange(sync.Players);
            rules = sync.Rules;

            AskForMissingFiles();
            if (HasAllFiles != reportedHasFiles)
                SendLocalChoice();

            if (Changed != null)
                Changed();
        }

        void OnMatchStart(NetworkGamer sender, BinaryReader reader)
        {
            MatchStartPacket start = MatchStartPacket.Deserialize(reader);
            if (MatchStarting != null)
                MatchStarting(start);
        }

        #endregion

        #region Community files

        /// <summary>
        /// Call every frame: asks again for a missing file now and then (a request can be lost, or the host may not have the file
        /// yet because the player who owns it has not sent it).
        /// </summary>
        public void Update(double elapsedSeconds)
        {
            clock += elapsedSeconds;
            AskForMissingFiles();
        }

        void AskForMissingFiles()
        {
            foreach (string id in MissingFiles())
            {
                if (asked.TryGetValue(id, out double when) && clock - when < AskAgainAfter)
                    continue;
                asked[id] = clock;

                if (!net.IsHost)
                {
                    net.SendToHost(new FileRequestPacket { Id = id }, true);
                    continue;
                }

                // the host asks the player who flies that ship; an arena it lacks it cannot ask for (it chose it from its own folder)
                foreach (LobbyPlayerInfo player in players)
                {
                    if (player.ShipId == id)
                    {
                        NetworkGamer owner = net.FindRemote(player.Id);
                        if (owner != null)
                            net.Send(owner, new FileRequestPacket { Id = id }, true);
                        break;
                    }
                }
            }
        }

        // host: a player wants a file, and gets it only if the match needs it now (nobody can pull anything else out of the host)
        void OnFileRequestFromClient(NetworkGamer sender, BinaryReader reader)
        {
            FileRequestPacket request = FileRequestPacket.Deserialize(reader);
            byte[] bytes = files.Get(request.Id);
            if (bytes == null || !NeededFiles().Contains(request.Id))
                return;

            SharedFileKind kind = request.Id == rules.ArenaId ? SharedFileKind.Arena : SharedFileKind.Ship;
            net.Send(sender, new FileDataPacket { Kind = kind, Id = request.Id, Data = bytes }, true);
        }

        // client: the host asks for the file of our ship
        void OnFileRequestFromHost(NetworkGamer sender, BinaryReader reader)
        {
            FileRequestPacket request = FileRequestPacket.Deserialize(reader);
            byte[] bytes = files.Get(request.Id);
            if (bytes != null && request.Id == localShipId)
                net.SendToHost(new FileDataPacket { Packet = PacketType.FileUpload, Kind = SharedFileKind.Ship, Id = request.Id, Data = bytes }, true);
        }

        // host: a player sent the file of the ship they chose
        void OnFileUpload(NetworkGamer sender, BinaryReader reader)
        {
            FileDataPacket file = FileDataPacket.Deserialize(reader, PacketType.FileUpload);
            if (file.Kind != SharedFileKind.Ship)
                return;

            // only a ship that player has chosen (or is choosing: the choice follows this packet)
            if (files.TryAdd(file.Kind, file.Id, file.Data, out string reason))
            {
                RefreshHostState();
                SyncAll();
                FileReceived?.Invoke(file.Kind, file.Id, file.Data);
            }
            else
                System.Diagnostics.Debug.WriteLine("[NET] Refused a ship file from " + sender.Gamertag + ": " + reason);
        }

        // client: the host sent a file we asked for
        void OnFileData(NetworkGamer sender, BinaryReader reader)
        {
            FileDataPacket file = FileDataPacket.Deserialize(reader);
            if (!NeededFiles().Contains(file.Id))
                return;

            if (files.TryAdd(file.Kind, file.Id, file.Data, out string reason))
            {
                FileReceived?.Invoke(file.Kind, file.Id, file.Data);
                if (HasAllFiles != reportedHasFiles)
                    SendLocalChoice();
                if (Changed != null)
                    Changed();
            }
            else
                System.Diagnostics.Debug.WriteLine("[NET] Refused a file from the host: " + reason);
        }

        #endregion

        LobbyPlayerInfo FindById(string id)
        {
            foreach (LobbyPlayerInfo player in players)
                if (player.Id == id)
                    return player;
            return null;
        }

        /// <summary>
        /// Gamertags look like "name_1a2b3c4d"; show just the name
        /// </summary>
        public static string DisplayName(string gamertag)
        {
            if (string.IsNullOrEmpty(gamertag))
                return "Player";
            return Regex.Replace(gamertag, "_[0-9a-fA-F]{8}$", "");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            net.Dispatcher.Unregister(PacketType.LobbyUpdate);
            net.Dispatcher.Unregister(PacketType.LobbySync);
            net.Dispatcher.Unregister(PacketType.MatchStart);
            net.Dispatcher.Unregister(PacketType.JoinRefused);
            net.Dispatcher.Unregister(PacketType.FileUpload);
            net.Dispatcher.Unregister(PacketType.FileData);
            net.Dispatcher.Unregister(PacketType.FileRequest);
            net.GamerJoined -= OnGamerJoined;
            net.GamerLeft -= OnGamerLeft;
        }
    }
}
