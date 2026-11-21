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
        readonly Screenshots screenshots = new Screenshots();
#if DEVSHOT
        DevShots devShots;
#endif

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
#if DEVSHOT
            if (DevShots.FrameLock)
            {
                // one 60 Hz tick per drawn frame, so a capture is smooth and repeatable however long saving a picture takes
                IsFixedTimeStep = false;
            }
#endif
            // keeps this game from listing, or joining, the sessions of other games on the network
            // protocol 2: the packets carry a version byte now, so older builds neither join nor misread
            VectorEngine.Networking.MultiplayerSessionService.Initialize("VectorRumble", protocolVersion: 3);

            // what the player chose last time: the language (before any content loads), the display, the glow
            UserSettings.Initialize();
#if USE_DEFAULT_DEVICE_SETTINGS
            UserSettings.ApplyBeforeStart(Graphics, changeFullScreen: false);
#else
            UserSettings.ApplyBeforeStart(Graphics, changeFullScreen: true);
#endif
#if DEVSHOT
            DevShots.ApplyEarly();
#endif
        }


        /// <summary>
        /// Overridden from Game.Initialize().  Sets up the ScreenManager.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();

            UserSettings.ApplyAudio(Audio);

#if DEVSHOT
            devShots = new DevShots(ScreenManager, GraphicsDevice, Exit);
            devShots.Attach();
#endif

            // F12 saves a picture of the game; the note that it did is drawn over the game
            ScreenManager.DrawingOverlays += screenshots.DrawToast;

            var splash = new VectorEngine.Splash.SplashOptions
            {
                BloomIntensity = 1.0f,
#if DEVSHOT
                Skippable = DevShots.SplashSkippable,
#endif
            };

            // the VectorEngine splash, then the background and main menu screens
            VectorEngine.Splash.SplashScreen.Start(ScreenManager, () =>
            {
                ScreenManager.AddScreen(new BackgroundScreen());
                ScreenManager.AddScreen(new MainMenuScreen());
#if DEVSHOT
                devShots.OpenScreens();
#endif
            },
            // the engine's default bloom is strong for the intro's thin lines
            splash);
        }


        protected override void Update(GameTime gameTime)
        {
#if DEVSHOT
            if (DevShots.FrameLock)
            {
                gameTime = DevShots.NextTick();
            }
#endif
            screenshots.Update();
            base.Update(gameTime);
        }


        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
#if DEVSHOT
            if (DevShots.FrameLock)
            {
                gameTime = DevShots.CurrentTick;
            }
#endif
            GraphicsDevice.Clear(Color.Black);

            // the screen manager owns the real drawing
            screenshots.BeginFrame();
            base.Draw(gameTime);
            screenshots.EndFrame(GraphicsDevice);
#if DEVSHOT
            devShots.AfterDraw(gameTime);
#endif
        }
    }
}
