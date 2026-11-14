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

        public bool ShowParticles { get; set; }

        /// <summary>
        /// Whether the game's logo is drawn. The menus for starting a game turn it off, with the
        /// fireworks, so that they do not distract.
        /// </summary>
        public bool ShowTitle { get; set; } = true;

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

            base.LoadContent();
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
            if (ShowParticles)
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


        /// <summary>
        /// Draws the background screen.
        /// </summary>
        public override void Draw(GameTime gameTime)
        {
            Viewport viewport = Display.Viewport;
            Vector2 viewportSize = new Vector2(viewport.Width, viewport.Height);

            if (ShowParticles)
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
