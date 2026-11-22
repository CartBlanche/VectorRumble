using Microsoft.Xna.Framework;
using VectorEngine.Effects;

namespace VectorRumble
{
    /// <summary>
    /// The shockwave of an explosion: three vector rings that spread out from the middle and fade, one brighter and
    /// quicker, one fainter and wider. Made of the engine's ripple particles, so it needs no shader or render target and
    /// looks the same on every head. Drawn on each machine from the explosion it already knows about: nothing is sent.
    /// </summary>
    static class Shockwave
    {
        /// <summary>How many particles one shockwave makes.</summary>
        public const int Rings = 3;

        /// <summary>A ship's explosion: the widest.</summary>
        public const float ShipRadius = 240f;

        /// <summary>A mine: a little more than the 80 px its blast reaches, so the ring shows the danger.</summary>
        public const float MineRadius = 100f;

        /// <summary>A rocket: a little more than the 128 px its blast reaches.</summary>
        public const float RocketRadius = 140f;

        /// <summary>A ship coming down from a hop.</summary>
        public const float LandingRadius = 36f;

        /// <summary>Spreads a shockwave of the given reach (in pixels) from <paramref name="at"/>.</summary>
        public static void Emit(ParticleField particles, Vector2 at, float radius, Color color)
        {
            if (particles == null || radius <= 0f)
            {
                return;
            }

            particles.EmitRipple(at, radius, color, 0.8f);
            particles.EmitRipple(at, radius * 0.65f, Color.Lerp(color, Color.White, 0.5f), 0.55f);
            particles.EmitRipple(at, radius * 1.25f, color * 0.5f, 1.05f);
        }
    }
}
