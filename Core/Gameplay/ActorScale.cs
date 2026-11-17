using System;
using System.Globalization;

namespace VectorRumble
{
    /// <summary>
    /// How much bigger ships and asteroids are drawn and collide than the sizes in their files and in the code. The game was made for a lower
    /// resolution than the native 720p it runs at now. Every machine in a network match must use the same value (asteroid shapes and
    /// collisions depend on it), so it is a constant of the build, not a setting; <c>VR_SCALE</c> overrides it only to try values by eye.
    /// </summary>
    static class ActorScale
    {
        /// <summary>1 means the sizes the files and the code say; the game's default is 1.5, chosen by eye at 1280x720.</summary>
        public static readonly float Value = Read();

        static float Read()
        {
            string text = Environment.GetEnvironmentVariable("VR_SCALE");
            if (text != null && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float scale) && scale >= 0.5f && scale <= 3f)
            {
                return scale;
            }
            return Default;
        }

        const float Default = 1.5f;
    }
}
