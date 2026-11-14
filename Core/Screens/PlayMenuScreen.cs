using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>Play: a game with friends over the network, or a game against the computer.</summary>
    class PlayMenuScreen : FrameMenuScreen
    {
        public PlayMenuScreen()
        {
            Heading = Strings.Play_Game;
            Rows.Add(MainMenuScreen.Entry(Strings.Multiplayer, Strings.Desc_Multiplayer, FrameIcon.Wifi, () => ScreenManager.AddScreen(new LanBrowserScreen())));
            Rows.Add(MainMenuScreen.Entry(Strings.Single_Player, Strings.Desc_SinglePlayer, FrameIcon.Ship, () => ScreenManager.AddScreen(new SinglePlayerSetupScreen())));
        }
    }
}
