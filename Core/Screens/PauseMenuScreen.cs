using System;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>
    /// The pause menu comes up over the top of the game: resume, change the settings, or quit the match.
    /// </summary>
    class PauseMenuScreen : FrameMenuScreen
    {
        public PauseMenuScreen()
        {
            Heading = Strings.Paused;

            Rows.Add(MainMenuScreen.Entry(Strings.Resume_Game, Strings.Desc_Resume, FrameIcon.Ship, ExitScreen));
            // the settings take this menu's place, and bring it back when they are closed
            Rows.Add(MainMenuScreen.Entry(Strings.Settings, Strings.Desc_Settings, FrameIcon.Gear, () =>
            {
                ExitScreen();
                ScreenManager.AddScreen(new SettingsScreen(returnToPause: true));
            }));
            Rows.Add(MainMenuScreen.Entry(Strings.Quit_Match, Strings.Desc_QuitMatch, FrameIcon.Exit, ConfirmQuit));

            // the game does not transition off while the pause menu is over it
            IsPopup = true;
        }

        /// <summary>When the user cancels the pause menu, resume the game.</summary>
        protected override void OnCancel()
        {
            ExitScreen();
        }

        void ConfirmQuit()
        {
            var box = new MessageBoxScreen(Strings.Confirm_QuitTitle, Strings.Confirm_QuitBody,
                Strings.Confirm_QuitToMenu, Strings.Confirm_KeepPlaying);
            box.Accepted += QuitAccepted;
            ScreenManager.AddScreen(box);
        }

        /// <summary>
        /// The player agreed to quit: use the loading screen to go from the game back to the main menu.
        /// </summary>
        void QuitAccepted(object sender, EventArgs e)
        {
            // If we are exiting, we no longer need these
            World.Actors.Clear();

            LoadingScreen.Load(ScreenManager, LoadMainMenuScreen, false);
        }

        void LoadMainMenuScreen(object sender, EventArgs e)
        {
            ScreenManager.AddScreen(new BackgroundScreen());
            ScreenManager.AddScreen(new MainMenuScreen());
        }
    }
}
