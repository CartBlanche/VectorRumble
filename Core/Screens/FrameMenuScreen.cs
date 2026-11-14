using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VectorRumble
{
    /// <summary>
    /// The base of Vector Rumble's menus in the Arena Frame look: the engine's <see cref="FrameScreen"/> plus the
    /// world every Vector Rumble screen shares, and the game's logo.
    /// </summary>
    abstract class FrameMenuScreen : FrameScreen
    {
        Texture2D titleTexture;

        internal static World World => GameScreen.World;

        public override void Initialize()
        {
            GameScreen.EnsureWorld(ScreenManager);
        }

        public override void LoadContent()
        {
            titleTexture = ScreenManager.Game.Content.Load<Texture2D>("Textures/title");
            base.LoadContent();
        }

        /// <summary>The logo: big on the main menu, small above a heading.</summary>
        protected override void DrawTitle(bool large)
        {
            float height = large ? 62f : 30f;
            float width = titleTexture.Width * height / titleTexture.Height;
            var rect = new Rectangle(86, large ? 86 : 62, (int)width, (int)height);
            ScreenManager.SpriteBatch.Begin();
            ScreenManager.SpriteBatch.Draw(titleTexture, rect, Color.White * TransitionAlpha);
            ScreenManager.SpriteBatch.End();
        }
    }
}
