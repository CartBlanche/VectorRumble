#region File Description
//-----------------------------------------------------------------------------
// Game.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

#region Using Statements
using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
#endregion

namespace VectorRumble
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class VectorRumbleGame : VectorEngineGame
    {
        public VectorRumbleGame()
            : base(new VectorEngineGameOptions
            {
                // the game is laid out at 1280x720 (the engine's default) and the window opens at that size
                MouseVisible = true,
#if USE_DEFAULT_DEVICE_SETTINGS
                UseDefaultDeviceSettings = true,
#endif
                ScreenManager = new ScreenManagerOptions
                {
                    LoadFont = content => content.LoadLocalized<SpriteFont>("Fonts/retroLarge"),
                    BlankTexturePath = "Textures/blank",
                    // the game is drawn at a fixed 1280x720 into a target, bloom included, then scaled to the window
                    RenderToVirtualTarget = true,
                    PerfOverlay = true,
                    MenuStyle = new MenuStyle
                    {
                        LoadSmallFont = content => content.LoadLocalized<SpriteFont>("Fonts/retroSmall"),
                        MessageBoxUsageText = () => Strings.MessageBox_Usage,
                        LoadingText = () => Strings.Loading,
                    },
                },
                AudioProject = "VectorRumble",
            })
        {
            // keeps this game from listing, or joining, the sessions of other games on the network
            Networking.MultiplayerSessionService.Initialize();
        }


        /// <summary>
        /// Overridden from Game.Initialize().  Sets up the ScreenManager.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();

            // add the background screen to the screen manager
            ScreenManager.AddScreen(new BackgroundScreen());

            // add the main menu screen to the screen manager
            ScreenManager.AddScreen(new MainMenuScreen());
        }


        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            // the screen manager owns the real drawing

            base.Draw(gameTime);
        }
    }
}
