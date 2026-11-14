#region Using Statements
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Net;
using VectorRumble.Networking;
#endregion

namespace VectorRumble
{
    /// <summary>
    /// Multiplayer: lists the games on the network and lets the player host one or join one.
    /// </summary>
    class LanBrowserScreen : FrameMenuScreen
    {
        #region Fields

        // seconds between searches for games
        const float SearchInterval = 3f;

        readonly List<AvailableNetworkSession> sessions = new List<AvailableNetworkSession>();

        // a join in progress (null when idle)
        Task<NetworkSession> sessionTask;
        string busyText;

        // a search for games in progress (null when idle)
        Task<AvailableNetworkSessionCollection> findTask;
        float searchTimer;

        // why the last join failed
        string errorText;

        // what is listed now, to tell when the list has to be rebuilt
        string listedKey;

        #endregion

        public LanBrowserScreen()
        {
            Heading = Strings.Multiplayer;
            RebuildEntries();
        }

        #region Entries

        void RebuildEntries()
        {
            int selected = SelectedIndex;
            Rows.Clear();

            var host = MainMenuScreen.Entry(Strings.Lan_Host_A_Game, Strings.Desc_Host, FrameIcon.Wifi,
                () => { if (!Busy) ScreenManager.AddScreen(new MatchRulesScreen(true)); });
            Rows.Add(host);

            foreach (AvailableNetworkSession session in sessions)
            {
                AvailableNetworkSession joining = session;
                var join = MainMenuScreen.Entry(LanLobby.DisplayName(session.HostGamertag), Strings.Desc_Join, FrameIcon.Ship, () => Join(joining));
                join.Meta = (session.CurrentGamerCount) + " / " + (session.CurrentGamerCount + session.OpenPublicGamerSlots);
                Rows.Add(join);
            }

            // An "Enter address..." entry would go here, to join a game whose address the player
            // types in, for when the game cannot be found by searching.
            // It is left out for now: typing an address needs a text-entry screen that works with a
            // gamepad and with touch, which the game does not have yet. When it does, add the entry
            // and make it call
            //     Join(MultiplayerSessionService.DirectSession(address));
            // MultiplayerSessionService.DirectSession is kept for it.

            SelectedIndex = selected;
            listedKey = ListKey();
        }

        string ListKey() => string.Join("|", sessions.Select(s => s.SessionId));

        #endregion

        #region Join

        bool Busy => sessionTask != null;

        void Join(AvailableNetworkSession session)
        {
            if (Busy)
                return;

            errorText = null;
            busyText = string.Format(Strings.Lan_Joining, LanLobby.DisplayName(session.HostGamertag));
            sessionTask = MultiplayerSessionService.JoinSessionAsync(session);
        }

        void FinishSessionTask()
        {
            Task<NetworkSession> task = sessionTask;
            sessionTask = null;
            busyText = null;

            if (task.IsFaulted || task.IsCanceled)
            {
                errorText = string.Format(Strings.Lan_Could_Not_Connect,
                    task.Exception?.GetBaseException().Message ?? "cancelled");
                NetLog.Write("Join failed: " + errorText);
                return;
            }

            NetworkSession session = task.Result;
            if (IsExiting)
            {
                // the player left while it was connecting
                session.Dispose();
                return;
            }

            ScreenManager.AddScreen(new ShipArenaScreen(new NetworkManager(session)));
        }

        #endregion

        #region Search

        void UpdateSearch(float elapsedTime)
        {
            if (findTask != null)
            {
                if (!findTask.IsCompleted)
                    return;

                Task<AvailableNetworkSessionCollection> task = findTask;
                findTask = null;
                searchTimer = SearchInterval;

                sessions.Clear();
                if (!task.IsFaulted && !task.IsCanceled)
                {
                    foreach (AvailableNetworkSession session in task.Result)
                    {
                        if (MultiplayerSessionService.IsCompatible(session))
                            sessions.Add(session);
                    }
                }

                if (ListKey() != listedKey)
                    RebuildEntries();
                return;
            }

            searchTimer -= elapsedTime;
            if (searchTimer <= 0f)
            {
                searchTimer = SearchInterval;
                findTask = MultiplayerSessionService.FindSessionsAsync();
            }
        }

        #endregion

        #region Update and Draw

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

            if (sessionTask != null && sessionTask.IsCompleted)
                FinishSessionTask();

            // look for games only while this screen is the one in front
            if (!otherScreenHasFocus && !coveredByOtherScreen && !IsExiting && !Busy)
                UpdateSearch((float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        // what is happening: joining, a failure, searching, or that nothing was found
        protected override void DrawPanelText(FrameCanvas c, Rectangle area, FrameRow selected)
        {
            base.DrawPanelText(c, area, selected);

            string status = busyText ?? errorText;
            if (status == null)
                status = findTask != null ? Strings.Lan_Searching : (sessions.Count == 0 ? Strings.Lan_No_Games : null);

            if (status != null)
            {
                c.Text(c.Large, status, new Vector2(area.Center.X, area.Y + 28), FrameScreen.Theme.Dim, 18f, FrameAlign.Centre);
            }
        }

        #endregion
    }
}
