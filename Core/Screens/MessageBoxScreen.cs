using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>
    /// The engine's message box, outlined in the colour of the arena being played.
    /// </summary>
    class MessageBoxScreen : VectorEngine.Screens.MessageBoxScreen
    {
        public MessageBoxScreen(string message) : base(message) { }

        protected override Color BorderColor => GameScreen.World.SelectedArena.Color;
    }
}
