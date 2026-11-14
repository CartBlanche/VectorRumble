using System;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>
    /// What the player sees when they press Escape (or Start) in a match: the game paused behind a "Leave?" box, and
    /// nothing else to wade through. Stay (the default) goes back to the match; Leave! quits to the menu.
    /// </summary>
    class PauseMenuScreen : MessageBoxScreen
    {
        public PauseMenuScreen()
            : base(Strings.Confirm_LeaveTitle, "", Strings.Confirm_Leave, Strings.Confirm_Stay)
        {
            BackdropHeading = Strings.Paused;
            Accepted += LeaveAccepted;
        }

        /// <summary>
        /// The player agreed to leave: use the loading screen to go from the game back to the main menu.
        /// </summary>
        void LeaveAccepted(object sender, EventArgs e)
        {
            // If we are leaving, we no longer need these
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
