namespace VectorRumble
{
    /// <summary>
    /// The engine's menu screen, plus the world every Vector Rumble screen shares.
    /// </summary>
    abstract class MenuScreen : VectorEngine.Screens.MenuScreen
    {
        internal static World World => GameScreen.World;

        public override void Initialize()
        {
            GameScreen.EnsureWorld(ScreenManager);
        }
    }
}
