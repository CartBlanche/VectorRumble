#region File Description
//-----------------------------------------------------------------------------
// GameplayScreen.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

#region Using Statements
using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
#endregion

namespace VectorRumble
{
    class BaseHUDScreen : GameScreen
    {

        #region Fields
        protected ContentManager content;

        protected LineBatch lineBatch;
        protected ParticleManager effects;

        protected SpriteBatch spriteBatch;
        SpriteFont spriteFont;

        Texture2D starTexture;
        protected VirtualGamePad virtualGamePad;

        /// <summary>
        /// Whether the on-screen controls may fade away when nobody is touching the screen. Only during play:
        /// the other screens ask the player to press things on them.
        /// </summary>
        protected virtual bool CanFadeVirtualGamePad => false;

        #endregion

        #region Initialization
        /// <summary>
        /// Constructor.
        /// </summary>
        public BaseHUDScreen()
        {
            TransitionOnTime = TimeSpan.FromSeconds(0.25);
            TransitionOffTime = TimeSpan.FromSeconds(0.25);
        }

        /// <summary>
        /// Load graphics content for the game.
        /// </summary>
        public override void LoadContent()
        {
            if (content == null)
            {
                content = new ContentManager(ScreenManager.Game.Services, "Content");
            }
            spriteBatch = new SpriteBatch(ScreenManager.GraphicsDevice);
            spriteFont = content.LoadLocalized<SpriteFont>("Fonts/retroSmall");
            starTexture = content.Load<Texture2D>("Textures/blank");


            lineBatch = new LineBatch(ScreenManager.GraphicsDevice);

            // update the projection in the line-batch
            lineBatch.SetProjection(Matrix.CreateOrthographicOffCenter(0.0f,
                Display.Viewport.Width,
                Display.Viewport.Height, 0.0f, 0.0f, 1.0f));

            // the world's explosions are drawn by this manager, through the same line batch and (identity) transform as the walls
            effects = new ParticleManager(ScreenManager, spriteBatch, lineBatch) { Transform = Matrix.Identity };
            World.Particles = effects;

            if (VectorEngine.Platform.IsMobile)
            {
                virtualGamePad = new VirtualGamePad(null, content.Load<Texture2D>("Textures/gamepad"),
                    new Vector2(Display.Viewport.Width, Display.Viewport.Height),
                    startHidden: GamePad.GetState(PlayerIndex.One).IsConnected);
                VirtualGamePad.Instance = virtualGamePad;
            }
        }


        /// <summary>
        /// Unload graphics content used by the game.
        /// </summary>
        public override void UnloadContent()
        {
            if (effects != null)
            {
                // the world outlives this screen: give it back a headless field, so nothing keeps the line batch disposed below
                if (World.Particles == effects)
                    World.Particles = new ParticleField();
                effects = null;
            }

            if (spriteBatch != null)
            {
                spriteBatch.Dispose();
                spriteBatch = null;
            }

            if (lineBatch != null)
            {
                lineBatch.Dispose();
                lineBatch = null;
            }

            if (virtualGamePad != null)
            {
                // A later screen's pad may already have taken over
                if (VirtualGamePad.Instance == virtualGamePad)
                    VirtualGamePad.Instance = null;
                virtualGamePad = null;
            }

            content.Unload();
        }
        #endregion

        /// <summary>
        /// Reads the touch controls while this screen has focus, and lets go of them otherwise.
        /// </summary>
        public override void Update(GameTime gameTime, bool otherScreenHasFocus,
                                                       bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

            if (otherScreenHasFocus || coveredByOtherScreen)
                virtualGamePad?.Reset();
            else
            {
                if (virtualGamePad != null)
                {
                    virtualGamePad.CanFade = CanFadeVirtualGamePad;
                    virtualGamePad.Update(ScreenManager.Input.CurrentTouchState, (float)gameTime.ElapsedGameTime.TotalSeconds);
                }
            }
        }

        /// <summary>
        /// Draws the gameplay screen.
        /// </summary>
        public override void Draw(GameTime gameTime)
        {
            float elapsedTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            lineBatch.Begin();

            // draw all actors
            foreach (Actor actor in World.Actors)
            {
                if (actor.Dead == false)
                {
                    actor.Draw(elapsedTime, lineBatch);
                }
            }

            lineBatch.End();

            DrawHud(elapsedTime);

            // draw the stars
            spriteBatch.Begin();
            World.Starfield.Draw(spriteBatch, starTexture);
            virtualGamePad?.Draw(spriteBatch);
            spriteBatch.End();

            // If the game is transitioning on or off, fade it out to black.
            if (TransitionPosition > 0)
                ScreenManager.FadeBackBufferToBlack(1f - TransitionAlpha);
        }

        /// <summary>
        /// Draw the user interface elements of the game (scores, etc.).
        /// </summary>
        /// <param name="elapsedTime">The amount of elapsed time, in seconds.</param>
        private void DrawHud(float elapsedTime)
        {
            Vector2 position = new Vector2(0.48f * Display.Viewport.Width, 0.09f * Display.Viewport.Height);
            int offset = (int)(Display.Viewport.Width / 5);
            float scale = 1.0f;

            spriteBatch.Begin();

            // each pilot's score, in the slot of their player number across the top
            Ship[] ships = World.ShipManager.Ships;
            for (int i = 0; i < ships.Length; i++)
            {
                Ship ship = ships[i];
                if (!ship.Playing)
                {
                    continue;
                }

                string message = string.Format(Strings.Score, ship.ScoreName, ship.Score);
                Vector2 size = spriteFont.MeasureString(message) * scale;
                position.X = ((int)ship.PlayerStringToIndex + 1) * offset - size.X / 2;
                spriteBatch.DrawString(spriteFont, message, position, Color.Yellow, 0.0f, Vector2.Zero, scale, SpriteEffects.None, 1.0f);
            }

            spriteBatch.End();
        }
    }

    /// <summary>
    /// This screen implements the actual game logic. It is just a
    /// placeholder to get the idea across: you'll probably want to
    /// put some more interesting gameplay in here!
    /// </summary>
    /// <remarks>
    /// This class is somewhat similar to one of the same name in the 
    /// GameStateManagement sample.
    /// </remarks>
    class GameplayScreen : BaseHUDScreen
    {
        #region Fields
        bool gameOver;

        protected override bool CanFadeVirtualGamePad => true;
        #endregion

        #region Initialization
        /// <summary>
        /// Constructor.
        /// </summary>
        public GameplayScreen()
        {
            TransitionOnTime = TimeSpan.FromSeconds(1.0);
            TransitionOffTime = TimeSpan.FromSeconds(1.0);
        }


        /// <summary>
        /// Initialize the game, after the ScreenManager is set, but not every time
        /// the graphics are reloaded.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            // start up the music
            World.AudioManager.PlayMusic("gameMusic");

            // start up the game
            World.StartNewGame();
            gameOver = false;
        }
        #endregion

        #region Update and Draw
        /// <summary>
        /// Updates the state of the game. This method checks the GameScreen.IsActive
        /// property, so the game will stop updating when the pause menu is active,
        /// or if you tab away to a different application.
        /// </summary>
        public override void Update(GameTime gameTime, bool otherScreenHasFocus,
                                                       bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

            // if this screen is leaving, then stop the music
            if (IsExiting)
            {
                World.AudioManager.StopMusic();
            }
            else if ((otherScreenHasFocus == true) || (coveredByOtherScreen == true))
            {    
                if (!VectorEngine.Platform.IsMobile)
                {
                    // make sure the player's controller is not vibrating
                    GamePad.SetVibration(PlayerIndex.One, 0f, 0f);
                }

                if (gameOver == false)
                {
                    for (int i = 0; i < World.Ships.Length; i++)
                    {
                        World.Ships[i].ProcessInput(gameTime.TotalGameTime.Seconds,
                            true);
                    }
                }

                // a network match goes on while a menu is up
                if (World.Match != null)
                {
                    SuspendShipInput(true);
                    World.Match.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
                    if (gameOver == false)
                    {
                        World.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
                    }
                }
            }
            else
            {
                // check for a winner
                if (gameOver == false && World.Match != null)
                {
                    // in a network match the host says who won
                    if (World.Match.Winner >= 0)
                    {
                        Ship[] slots = World.ShipManager.Players.OrderBy(p => p.PlayerStringToIndex).ToArray();
                        Ship winner = World.Match.Winner < slots.Length ? slots[World.Match.Winner] : null;
                        ScreenManager.AddScreen(new GameOverScreen(WinnerMessage(winner, World.Match.Winner), Scoreboard(), false, celebrate: true));
                        gameOver = true;
                    }
                    else if (World.Match.HasEnded)
                    {
                        // the host left, or the network failed
                        ScreenManager.AddScreen(new GameOverScreen(Strings.Lan_Connection_Lost, Scoreboard(), false));
                        gameOver = true;
                    }
                }
                else if (gameOver == false)
                {
                    for (int i = 0; i < World.Ships.Length; i++)
                    {
                        if (World.Ships[i].Score >= MatchRules.Current.ScoreLimit)
                        {
                            ScreenManager.AddScreen(new GameOverScreen(WinnerMessage(World.Ships[i], i), Scoreboard(), celebrate: true));
                            gameOver = true;
                            break;
                        }
                    }
                }
                // update the world
                if (gameOver == false)
                {
                    if (World.Match != null)
                    {
                        SuspendShipInput(false);
                        World.Match.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
                    }
                    World.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
                }
            }
        }


        /// <summary>Who won ("dominique Wins!"), by the pilot's name; the player's number when the ship cannot be found.</summary>
        static string WinnerMessage(Ship winner, int index)
        {
            return winner != null
                ? string.Format(Strings.Player_X_Wins, winner.ScoreName)
                : string.Format(Strings.Player_X_Wins, index + 1);
        }

        /// <summary>The final score of each pilot in the match, best first.</summary>
        static System.Collections.Generic.List<ScoreLine> Scoreboard()
        {
            return World.ShipManager.Players
                .OrderByDescending(p => p.Score)
                .Select(p => new ScoreLine { Name = p.ScoreName, Score = p.Score, Colour = p.Color, Shape = p.Shape.Points })
                .ToList();
        }

        /// <summary>
        /// Leaving the game ends a network match, and its session with it.
        /// </summary>
        public override void UnloadContent()
        {
            World.Match?.Dispose();
            base.UnloadContent();
        }

        /// <summary>
        /// Stops (or lets) the ships take their player's controls.
        /// </summary>
        void SuspendShipInput(bool suspended)
        {
            foreach (Ship ship in World.Ships)
            {
                ship.InputSuspended = suspended;
            }
        }

        /// <summary>
        /// Lets the game respond to player input. Unlike the Update method,
        /// this will only be called when the gameplay screen is active.
        /// </summary>
        public override void HandleInput(GameTime gameTime, InputState input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (input.PauseGame)
            {
                // If they pressed pause, bring up the pause menu screen.
                ScreenManager.AddScreen(new PauseMenuScreen());
            }
        }


        /// <summary>
        /// Draws the gameplay screen.
        /// </summary>
        public override void Draw(GameTime gameTime)
        {
            if (WorldRules.NeonEffect) {
                ScreenManager.BloomComponent.BeginDraw();
            }

            // the explosions (the manager has its own line batch pass: it comes first, as the particles did)
            effects.Draw(gameTime);

            lineBatch.Begin();

            // draw the walls
            World.SelectedArena.DrawWalls(lineBatch);

            lineBatch.End();

            if (WorldRules.NeonEffect)
            {
                ScreenManager.BloomComponent.Draw(gameTime);
            }

            base.Draw(gameTime);
        }
#endregion
    }
}
