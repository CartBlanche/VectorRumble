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
            // create the world
            if (World == null)
            {
                World = new World(Display.BaseSize)
                {
                    // retrieve the audio manager
                    AudioManager = (AudioManager)ScreenManager.Game.Services.GetService(typeof(AudioManager))
                };
            }
        }
    }
}
