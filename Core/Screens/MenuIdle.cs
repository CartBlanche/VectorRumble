using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace VectorRumble
{
    /// <summary>
    /// The menus freeze when nobody has touched anything for a while: the fireworks and the animation stop and a dim
    /// "Paused" comes over them, which saves the battery of a phone left on the main menu, and the fans of a PC. Any input
    /// at all (a key, the mouse moving, a touch, a pad) wakes them again. The screens that must keep going while the player
    /// waits (the network lobby) never freeze.
    /// </summary>
    sealed class MenuIdle
    {
        /// <summary>Seconds without input before the menus freeze.</summary>
        public const float DefaultDelay = 10f;

        const float DimInSeconds = 0.6f;
        const float DimOutSeconds = 0.15f;

        readonly float delay;
        float idle;

        /// <summary>True while the animation is stopped; the menu screens read it to hold their clocks.</summary>
        public static bool Frozen { get; private set; }

        /// <summary>The dimming and the word "Paused": from 0 (none) to 1 (full).</summary>
        public float Dim { get; private set; }

        public bool Paused => idle >= delay;

        public MenuIdle(float delay = DefaultDelay)
        {
            this.delay = delay;
        }

        /// <param name="elapsedSeconds">Time since the last call.</param>
        /// <param name="activity">Something was done this update (see <see cref="AnyActivity"/>).</param>
        /// <param name="allowed">False where the menus must keep going: a lobby, the setting turned off.</param>
        public void Update(float elapsedSeconds, bool activity, bool allowed)
        {
            idle = activity || !allowed ? 0f : idle + elapsedSeconds;

            float step = elapsedSeconds / (Paused ? DimInSeconds : DimOutSeconds);
            Dim = Paused ? Math.Min(1f, Dim + step) : Math.Max(0f, Dim - step);
            Frozen = Paused;
        }

        /// <summary>The menus run again from here (when the background goes, with the menus).</summary>
        public void Release()
        {
            idle = 0f;
            Dim = 0f;
            Frozen = false;
        }

        /// <summary>Whether the player did anything this update: pressed a key, a button or the screen, moved the mouse, or pushed a stick.</summary>
        public static bool AnyActivity(VectorEngine.Input.InputState input)
        {
            if (VectorEngine.Splash.SplashScreen.AnyNewInput(input))
            {
                return true;
            }

            if (input.CurrentMouseState.X != input.LastMouseState.X || input.CurrentMouseState.Y != input.LastMouseState.Y
                || input.CurrentMouseState.ScrollWheelValue != input.LastMouseState.ScrollWheelValue)
            {
                return true;
            }

            if (input.CurrentTouchState.Count > 0)
            {
                return true;
            }

            for (int i = 0; i < input.CurrentGamePadStates.Length; i++)
            {
                GamePadState pad = input.CurrentGamePadStates[i];
                if (pad.ThumbSticks.Left.LengthSquared() > 0.25f || pad.ThumbSticks.Right.LengthSquared() > 0.25f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
