namespace VectorRumble
{
    /// <summary>
    /// Vector Rumble's screen base class: the engine's <see cref="VectorEngine.Screens.GameScreen"/> plus the world
    /// that every screen of a match shares.
    /// </summary>
    abstract class GameScreen : VectorEngine.Screens.GameScreen
    {
        /// <summary>
        /// The world within which this screen will exist
        /// </summary>
        internal static World World { get; set; }

        public override void Initialize()
        {
            EnsureWorld(ScreenManager);
        }

        /// <summary>Creates the world the first time any screen needs it.</summary>
        internal static void EnsureWorld(ScreenManager screenManager)
        {
            if (World == null)
            {
                World = new World(Display.BaseSize)
                {
                    // retrieve the audio manager
                    AudioManager = (AudioManager)screenManager.Game.Services.GetService(typeof(AudioManager))
                };
            }
        }
    }
}
