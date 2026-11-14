using System;
using Microsoft.Xna.Framework;
using VectorRumble.Networking;
using Microsoft.Xna.Framework.Net;

namespace VectorRumble
{
    /// <summary>
    /// The first step of a single player game, or of hosting a network game: the rules. What to play to, and under
    /// "More rules" the asteroids and whether the sticks can fire in any direction. The next step picks the ship and
    /// the arena. When hosting, Next also starts the game on the network, so that others can find it.
    /// </summary>
    class MatchRulesScreen : FrameMenuScreen
    {
        // read each time, so they follow a change of language
        static string[] asteroidDensities => new[]
        {
            Strings.Asteroid_Density_None,
            Strings.Asteroid_Density_Low,
            Strings.Asteroid_Density_Medium,
            Strings.Asteroid_Density_High,
        };

        const int ScoreLimitStep = 5;

        readonly bool hosting;
        bool moreRules;

        // hosting: the game is being started on the network (null when idle)
        System.Threading.Tasks.Task<NetworkSession> hostTask;
        string errorText;

        public MatchRulesScreen(bool hosting)
        {
            this.hosting = hosting;
            Heading = hosting ? Strings.Lan_Host_A_Game : Strings.Single_Player;
            Layout = FrameLayout.Rows;

            var firstTo = new FrameRow(Strings.Label_FirstTo, FrameRowKind.Choice)
            {
                Value = () => string.Format(Strings.Value_Kills, MatchRules.Current.ScoreLimit),
            };
            firstTo.Adjusted += ChangeScoreLimit;
            Rows.Add(firstTo);

            var more = new FrameRow(Strings.Label_MoreRules, FrameRowKind.Expander) { Expanded = () => moreRules };
            more.Selected += () => moreRules = !moreRules;
            Rows.Add(more);

            var asteroids = new FrameRow(Strings.Label_Asteroids, FrameRowKind.Choice)
            {
                Value = () => asteroidDensities[(int)MatchRules.Current.AsteroidDensity],
                Hidden = () => !moreRules,
            };
            asteroids.Adjusted += ChangeAsteroidDensity;
            Rows.Add(asteroids);

            var freeAim = new FrameRow(Strings.Label_FreeAim, FrameRowKind.Toggle)
            {
                Value = () => MatchRules.Current.ControllersCanShootInAllDirections ? Strings.Value_On : Strings.Value_Off,
                Hidden = () => !moreRules,
            };
            freeAim.Adjusted += d => MatchRules.Current.ControllersCanShootInAllDirections = !MatchRules.Current.ControllersCanShootInAllDirections;
            Rows.Add(freeAim);

            Next = new FrameNext
            {
                Step = 1,
                Steps = hosting ? 3 : 2,
                Action = hosting ? StartHosting : () => ScreenManager.AddScreen(new ShipArenaScreen()),
            };
        }

        void StartHosting()
        {
            if (hostTask != null)
            {
                return;
            }

            errorText = null;
            hostTask = MultiplayerSessionService.CreateHostSessionAsync(MultiplayerSessionService.MaxPlayers);
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

            if (hostTask == null || !hostTask.IsCompleted)
            {
                return;
            }

            var task = hostTask;
            hostTask = null;
            if (task.IsFaulted || task.IsCanceled)
            {
                errorText = string.Format(Strings.Lan_Could_Not_Connect, task.Exception?.GetBaseException().Message ?? "cancelled");
                NetLog.Write("Host failed: " + errorText);
                return;
            }

            if (IsExiting)
            {
                // the player left while it was starting
                task.Result.Dispose();
                return;
            }

            ScreenManager.AddScreen(new ShipArenaScreen(new NetworkManager(task.Result)));
        }

        static void ChangeScoreLimit(int direction)
        {
            int limit = MatchRules.Current.ScoreLimit + direction * ScoreLimitStep;
            if (limit > MatchRules.MaxScoreLimit)
                limit = MatchRules.MinScoreLimit;
            else if (limit < MatchRules.MinScoreLimit)
                limit = MatchRules.MaxScoreLimit;
            MatchRules.Current.ScoreLimit = limit;
        }

        static void ChangeAsteroidDensity(int direction)
        {
            int count = asteroidDensities.Length;
            MatchRules.Current.AsteroidDensity = (AsteroidDensity)(((int)MatchRules.Current.AsteroidDensity + direction + count) % count);
        }

        protected override void DrawPanel(FrameCanvas c, Rectangle area, FrameRow selected)
        {
            c.Icon(hosting ? FrameIcon.Wifi : FrameIcon.Duel, new Vector2(area.Center.X, area.Y + 134), 92f, FrameScreen.Theme.Hot, Time);
        }

        protected override void DrawPanelText(FrameCanvas c, Rectangle area, FrameRow selected)
        {
            var theme = FrameScreen.Theme;
            c.Text(c.Display, string.Format(Strings.Score_Limit, MatchRules.Current.ScoreLimit), new Vector2(area.Center.X, area.Y + 258), theme.Text, 26f, FrameAlign.Centre);
            string status = hostTask != null ? Strings.Lan_Starting_Game : errorText ?? Strings.Setup_Next_Hint;
            c.Text(c.Large, status, new Vector2(area.Center.X, area.Bottom - 56), theme.Dim, 18f, FrameAlign.Centre);
        }
    }
}
