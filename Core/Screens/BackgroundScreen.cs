#region File Description
//-----------------------------------------------------------------------------
// BackgroundScreen.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

#region Using Statements
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace VectorRumble
{
    /// <summary>
    /// The background screen sits behind all the other menu screens.
    /// It draws a background image that remains fixed in place regardless
    /// of whatever transitions the screens on top of it may be doing.
    /// </summary>
    /// <remarks>
    /// This class is somewhat similar to one of the same name in the 
    /// GameStateManagement sample.
    /// </remarks>
    class BackgroundScreen : GameScreen
    {
        #region Fields
        Random random;
        // the menu fireworks: VectorEngine particle effects (ParticleEffects.Burst is the classic Vector Rumble explosion as data)
        ParticleManager fireworks;
        float addTimer;
        Color[] explosionColors = new Color[] 
            {
                Color.Orange, Color.Red, Color.Gray, Color.Silver, Color.Yellow
            };
        LineBatch lineBatch;
        Texture2D titleTexture;

        // the menus freeze, with a dim "Paused", when nobody has done anything for a while
        readonly MenuIdle idle = new MenuIdle();

        public bool ShowParticles { get; set; }

        /// <summary>The fireworks show when the screen wants them and the player has not turned them off in Settings.</summary>
        bool FireworksOn => ShowParticles && UserSettings.Current.MenuFireworks;

        /// <summary>
        /// Whether the game's logo is drawn. The menus for starting a game turn it off, with the
        /// fireworks, so that they do not distract.
        /// </summary>
        public bool ShowTitle { get; set; } = false;

        /// <summary>
        /// Shows or hides the logo and the fireworks together.
        /// </summary>
        public bool Quiet
        {
            set
            {
                ShowParticles = !value;
                ShowTitle = !value;
            }
        }
        #endregion

        #region Initialization


        /// <summary>
        /// Constructor.
        /// </summary>
        public BackgroundScreen()
        {
            TransitionOnTime = TimeSpan.FromSeconds(1.0);
            TransitionOffTime = TimeSpan.FromSeconds(1.0);

			random = new Random();

            ShowParticles = true;
        }


        /// <summary>
        /// Loads graphics content for this screen. The background texture is quite
        /// big, so we use our own local ContentManager to load it. This allows us
        /// to unload before going from the menus into the game itself, wheras if we
        /// used the shared ContentManager provided by the ScreenManager, the content
        /// would remain loaded forever.
        /// </summary>
        public override void LoadContent()
        {
            lineBatch = new LineBatch(ScreenManager.GraphicsDevice);
            titleTexture = ScreenManager.Game.Content.Load<Texture2D>("Textures/title");

            int viewportWidth = Display.Viewport.Width;
            int viewportHeight = Display.Viewport.Height;

            // update the projection in the line-batch
            lineBatch.SetProjection(Matrix.CreateOrthographicOffCenter(0.0f,
                viewportWidth, viewportHeight, 0.0f, 0.0f, 1.0f));

            // recreate the fireworks, drawn exactly as before: through this screen's own line batch with the identity transform
            fireworks = new ParticleManager(ScreenManager, ScreenManager.SpriteBatch, lineBatch) { Transform = Matrix.Identity };
            for (int i = 0; i < 4; i++)
            {
                AddFirework(random.Next(viewportWidth), random.Next(viewportHeight));
            }

            ScreenManager.DrawingOverlays += DrawPaused;

            base.LoadContent();
        }

        public override void UnloadContent()
        {
            ScreenManager.DrawingOverlays -= DrawPaused;
            idle.Release();
            base.UnloadContent();
        }


        #endregion

        #region Update and Draw


        /// <summary>
        /// One firework: 64 particles, speed from 256 to 384, a lifetime of one to three seconds, as the old ParticleSystem call was.
        /// </summary>
        void AddFirework(int x, int y)
        {
            float life = 1f + 2f * (float)random.NextDouble();
            fireworks.Emit(ParticleEffects.Burst(64, 256, 128, life, 0.05f, explosionColors), new Vector2(x, y));
        }

        /// <summary>
        /// Updates the background screen. Unlike most screens, this should not
        /// transition off even if it has been covered by another screen: it is
        /// supposed to be covered, after all! This overload forces the
        /// coveredByOtherScreen parameter to false in order to stop the base
        /// Update method wanting to transition off.
        /// </summary>
        public override void Update(GameTime gameTime, bool otherScreenHasFocus,
                                                       bool coveredByOtherScreen)
        {
            // only a menu that can wait (not the network lobby) freezes, and only if the player has not turned it off
            var screens = ScreenManager.GetScreens();
            bool canFreeze = UserSettings.Current.PauseIdleMenus
                && screens.Length > 0 && screens[screens.Length - 1] is FrameScreen top && top.AllowsIdlePause;
            idle.Update((float)gameTime.ElapsedGameTime.TotalSeconds, MenuIdle.AnyActivity(ScreenManager.Input), canFreeze);

            if (FireworksOn && !idle.Paused)
            {
                addTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (addTimer > 0.25f)
                {
                    addTimer -= 0.15f;
                    AddFirework(random.Next(Display.Viewport.Width), random.Next(Display.Viewport.Height));
                }
                fireworks.Update(gameTime);
            }

            base.Update(gameTime, otherScreenHasFocus, false);
        }


        // over everything: the dimming and the word
        void DrawPaused(ScreenManager screens, GameTime gameTime)
        {
            if (idle.Dim <= 0f)
            {
                return;
            }

            SpriteBatch batch = screens.SpriteBatch;
            batch.Begin();
            batch.Draw(screens.BlankTexture, new Rectangle(0, 0, Display.BaseWidth, Display.BaseHeight), Color.Black * (0.6f * idle.Dim));

            SpriteFont font = screens.Font;
            const float scale = 1.5f;
            string word = Strings.Paused;
            Vector2 size = font.MeasureString(word) * scale;
            Vector2 at = new Vector2((Display.BaseWidth - size.X) / 2f, (Display.BaseHeight - size.Y) / 2f);
            batch.DrawString(font, word, at, Color.White * idle.Dim, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            batch.End();
        }

        /// <summary>
        /// Draws the background screen.
        /// </summary>
        public override void Draw(GameTime gameTime)
        {
            Viewport viewport = Display.Viewport;
            Vector2 viewportSize = new Vector2(viewport.Width, viewport.Height);

            if (FireworksOn)
            {
                // Apply bloom to the explosions 
                ScreenManager.BloomComponent.BeginDraw();

                fireworks.Draw(gameTime);

                ScreenManager.BloomComponent.Draw(gameTime);
            }

            if (!ShowTitle)
            {
                return;
            }

            // title
            Vector2 titlePosition = new Vector2(
                (viewportSize.X - titleTexture.Width) / 2f,
                viewportSize.Y * 0.18f);
            titlePosition.Y -= (float)Math.Pow(TransitionPosition, 2) * titlePosition.Y;
            Color titleColor = Color.White;

            ScreenManager.SpriteBatch.Begin();
            ScreenManager.SpriteBatch.Draw(titleTexture, titlePosition,
                new Color(titleColor.R, titleColor.G, titleColor.B, TransitionAlphaByte));
            ScreenManager.SpriteBatch.End();
        }


        #endregion
    }
}
