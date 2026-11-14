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
    /// Shown when a match is over: the final score of every pilot, who won, and what to do next. A match on this
    /// machine can be played again, or its ship and arena changed; a network match goes back to the menu.
    /// </summary>
    class GameOverScreen : FrameMenuScreen
    {
        readonly string message;
        readonly IList<ScoreLine> scores;

        // the winner's confetti (VectorEngine's ParticleManager): a few bursts across the top in the first couple of seconds
        readonly bool celebrate;
        ParticleManager confetti;
        LineBatch confettiLines;
        readonly Random random = new Random();
        float confettiClock = -0.3f, nextBurst;

        /// <param name="message">Who won, or why the match ended</param>
        /// <param name="scores">The final score of each pilot, best first; empty when there is none to show</param>
        /// <param name="allowPlayAgain">False in a network game, which cannot be restarted from here</param>
        /// <param name="celebrate">True when somebody won, which showers the screen with confetti</param>
        public GameOverScreen(string message, IList<ScoreLine> scores, bool allowPlayAgain = true, bool celebrate = false)
        {
            this.message = message;
            this.scores = scores ?? new List<ScoreLine>();
            this.celebrate = celebrate;

            Heading = Strings.Game_Over;
            Layout = FrameLayout.Rows;
            ShowPanel = false;
            ShowBack = false;
            IsPopup = true;
            TransitionOnTime = TimeSpan.FromSeconds(0.8);
            TransitionOffTime = TimeSpan.FromSeconds(0.6);

            if (allowPlayAgain)
            {
                AddRow(Strings.Rematch, false, Rematch);
                AddRow(Strings.Ship_And_Arena, true, () => LoadingScreen.Load(ScreenManager, LoadShipAndArena, false));
            }

            AddRow(Strings.Return_to_Menu, true, () => LoadingScreen.Load(ScreenManager, LoadMainMenuScreen, false));
        }

        void AddRow(string label, bool chevron, Action selected)
        {
            var row = new FrameRow(label, FrameRowKind.Action) { Chevron = chevron };
            row.Selected += selected;
            Rows.Add(row);
        }

        protected override float RowsWidth => 600f;

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

        #region Next

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

        // back to the choice of ship and arena, with the screens before it in place so that Back works
        void LoadShipAndArena(object sender, EventArgs e)
        {
            ScreenManager.AddScreen(new BackgroundScreen());
            ScreenManager.AddScreen(new MainMenuScreen());
            ScreenManager.AddScreen(new PlayMenuScreen());
            ScreenManager.AddScreen(new MatchRulesScreen(false));
            ScreenManager.AddScreen(new ShipArenaScreen());
        }

        void LoadMainMenuScreen(object sender, EventArgs e)
        {
            ScreenManager.AddScreen(new BackgroundScreen());
            ScreenManager.AddScreen(new MainMenuScreen());
        }

        protected override void OnCancel()
        {
            LoadingScreen.Load(ScreenManager, LoadMainMenuScreen, false);
        }

        #endregion

        #region The scoreboard

        const float PanelX = 740, PanelW = 440;

        protected override void DrawBody(FrameCanvas c)
        {
            var frame = FrameCanvas.Chamfer(PanelX, 196, PanelW, 400, 26);
            c.Fill(frame, new Color(8, 8, 26), 0.5f);
            c.Poly(frame, true, FrameScreen.Theme.Panel, 1.8f, .7f);

            int most = Math.Max(Math.Max(1, MatchRules.Current.ScoreLimit), scores.Count == 0 ? 1 : scores.Max(s => s.Score));
            for (int i = 0; i < scores.Count && i < 4; i++)
            {
                float y = 304 + i * 66;
                ScoreLine line = scores[i];
                c.Polygon(line.Shape, new Vector2(PanelX + 40, y), MathF.PI / 2f, 13f / Math.Max(1f, line.Shape.Max(p => p.Length())), line.Colour, 2f);

                // the bar, in the pilot's colour, as long as their share of the score limit
                float full = PanelW - 250f, length = Math.Max(6f, full * Math.Max(0, line.Score) / most);
                var bar = new[] { new Vector2(PanelX + 190, y - 6), new Vector2(PanelX + 190 + length, y - 6), new Vector2(PanelX + 190 + length, y + 6), new Vector2(PanelX + 190, y + 6) };
                c.Fill(bar, line.Colour, .85f);
            }
        }

        protected override void DrawBodyText(FrameCanvas c)
        {
            var theme = FrameScreen.Theme;

            if (scores.Count == 0)
            {
                // no scoreboard (the network failed): just what happened
                c.Text(c.Large, message, new Vector2(PanelX + PanelW / 2, 380), theme.Text, 22f, FrameAlign.Centre);
                return;
            }

            c.Text(c.Display, Strings.Final_Score.ToUpperInvariant(), new Vector2(PanelX + PanelW / 2, 226), theme.Dim, 15f, FrameAlign.Centre);
            for (int i = 0; i < scores.Count && i < 4; i++)
            {
                float y = 304 + i * 66;
                ScoreLine line = scores[i];
                bool winner = i == 0;
                string name = line.Name ?? string.Empty;
                float height = 24f, room = 112f;
                float width = FrameCanvas.Measure(c.Medium, name, height);
                if (width > room) height = Math.Max(12f, height * room / width);
                c.Text(c.Medium, name, new Vector2(PanelX + 72, y - height / 2f), winner ? theme.Text : theme.Dim, height);
                c.Text(c.Display, line.Score.ToString(), new Vector2(PanelX + PanelW - 24, y - 12), winner ? theme.Hot : theme.Dim, 24f, FrameAlign.Right);
            }

            c.Text(c.Display, message.ToUpperInvariant(), new Vector2(PanelX + PanelW / 2, 562), theme.Hot, 16f, FrameAlign.Centre);
        }

        #endregion
    }
}
