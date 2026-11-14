using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>One pilot's line on the final scoreboard.</summary>
    class ScoreLine
    {
        public string Name;
        public int Score;
        public Color Colour;
        public Vector2[] Shape;
    }

    /// <summary>
    /// Shown when a match is over, for a match on this machine and a network match alike: the game behind a box
    /// that names the winner, lists the final score of every pilot, and offers Rematch and Leave!. A network
    /// match cannot be restarted from here (the lobby would have to come back), so it offers Leave! alone.
    /// </summary>
    class GameOverScreen : FrameMenuScreen
    {
        const float RowHeight = 54f;

        readonly IList<ScoreLine> scores;

        // the winner's confetti (VectorEngine's ParticleManager): a few bursts across the top in the first couple of seconds
        readonly bool celebrate;
        ParticleManager confetti;
        LineBatch confettiLines;
        readonly Random random = new Random();
        float confettiClock = -0.3f, nextBurst;

        /// <param name="message">Who won, or why the match ended: the heading of the box</param>
        /// <param name="scores">The final score of each pilot, best first; empty when there is none to show</param>
        /// <param name="allowPlayAgain">False in a network game, which cannot be restarted from here</param>
        /// <param name="celebrate">True when somebody won, which showers the screen with confetti</param>
        public GameOverScreen(string message, IList<ScoreLine> scores, bool allowPlayAgain = true, bool celebrate = false)
        {
            this.scores = scores ?? new List<ScoreLine>();
            this.celebrate = celebrate;

            Layout = FrameLayout.Modal;
            Heading = message;
            Backdrop = Strings.Game_Over;
            ShowBack = false;
            IsPopup = true;
            TransitionOnTime = TimeSpan.FromSeconds(0.8);
            TransitionOffTime = TimeSpan.FromSeconds(0.6);

            if (allowPlayAgain)
            {
                var rematch = new FrameRow(Strings.Rematch);
                rematch.Selected += Rematch;
                Rows.Add(rematch);
            }

            var leave = new FrameRow(Strings.Confirm_Leave);
            leave.Selected += Leave;
            Rows.Add(leave);
        }

        public override void LoadContent()
        {
            if (celebrate)
            {
                confettiLines = new LineBatch(ScreenManager.GraphicsDevice);
                confettiLines.SetProjection(Matrix.CreateOrthographicOffCenter(0f,
                    Display.BaseWidth, Display.BaseHeight, 0f, 0f, 1f));
                confetti = new ParticleManager(ScreenManager, ScreenManager.SpriteBatch, confettiLines,
                    ScreenManager.BlankTexture, Vector2.Zero);
            }

            base.LoadContent();
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
            if (confetti == null)
                return;

            confettiClock += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (confettiClock >= nextBurst && confettiClock < 2f)
            {
                nextBurst = confettiClock + 0.12f;
                Vector2 size = ScreenManager.BaseScreenSize;
                for (int i = 0; i < 3; i++)
                {
                    confetti.Position = new Vector2(size.X * (0.1f + 0.8f * (float)random.NextDouble()),
                        size.Y * (0.05f + 0.25f * (float)random.NextDouble()));
                    confetti.Emit(12, ParticleEffectType.Confetti);
                }
            }
            confetti.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
            confetti?.Draw(gameTime);
        }

        #region What the buttons do

        // the same pilots in the same arena (a "Random" arena is picked again)
        void Rematch()
        {
            World.ShipManager.UpdateSelectedList();
            World.SelectedArena = null;
            LoadingScreen.Load(ScreenManager, LoadGameplayScreen, true);
        }

        void LoadGameplayScreen(object sender, EventArgs e)
        {
            ScreenManager.AddScreen(new GameplayScreen { ScreenManager = this.ScreenManager });
        }

        void Leave() => LoadingScreen.Load(ScreenManager, LoadMainMenuScreen, false);

        void LoadMainMenuScreen(object sender, EventArgs e)
        {
            ScreenManager.AddScreen(new BackgroundScreen());
            ScreenManager.AddScreen(new MainMenuScreen());
        }

        protected override void OnCancel() => Leave();

        #endregion

        #region The scoreboard, inside the box

        protected override float ModalWidth => 600f;

        protected override float ModalBodyHeight(string[] messageLines) => Math.Min(4, scores.Count) * RowHeight;

        protected override void DrawModalBody(FrameCanvas c, float x, float y, float width)
        {
            int most = Math.Max(Math.Max(1, MatchRules.Current.ScoreLimit), scores.Count == 0 ? 1 : scores.Max(s => s.Score));
            for (int i = 0; i < scores.Count && i < 4; i++)
            {
                float cy = y + 26 + i * RowHeight;
                ScoreLine line = scores[i];
                c.Polygon(line.Shape, new Vector2(x + 18, cy), MathF.PI / 2f, 13f / Math.Max(1f, line.Shape.Max(p => p.Length())), line.Colour, 2f);

                // the bar, in the pilot's colour, as long as their share of the score limit
                float barX = x + 215, full = width - 215 - 56, length = Math.Max(6f, full * Math.Max(0, line.Score) / most);
                var bar = new[] { new Vector2(barX, cy - 6), new Vector2(barX + length, cy - 6), new Vector2(barX + length, cy + 6), new Vector2(barX, cy + 6) };
                c.Fill(bar, line.Colour, .85f);
            }
        }

        protected override void DrawModalBodyText(FrameCanvas c, float x, float y, float width)
        {
            var theme = FrameScreen.Theme;
            for (int i = 0; i < scores.Count && i < 4; i++)
            {
                float cy = y + 26 + i * RowHeight;
                ScoreLine line = scores[i];
                bool winner = i == 0;
                string name = line.Name ?? string.Empty;
                float height = 24f, room = 150f;
                float measured = FrameCanvas.Measure(c.Medium, name, height);
                if (measured > room) height = Math.Max(12f, height * room / measured);
                c.Text(c.Medium, name, new Vector2(x + 52, cy - height / 2f), winner ? theme.Text : theme.Dim, height);
                c.Text(c.Medium, line.Score.ToString(), new Vector2(x + width - 8, cy - 13), winner ? theme.Hot : theme.Dim, 26f, FrameAlign.Right);
            }
        }

        #endregion
    }
}
