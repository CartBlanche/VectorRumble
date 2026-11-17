using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>
    /// A thruster on a ship: where its flame starts (ship-local pixels, nose toward -y, before the game's scale) and how big the flame is
    /// (1 is the usual flame). The flame always points straight back, along +y.
    /// </summary>
    readonly struct Thruster
    {
        public readonly Vector2 At;
        public readonly float Size;

        public Thruster(Vector2 at, float size)
        {
            At = at;
            Size = size;
        }
    }
}
