using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace VectorRumble
{
    /// <summary>
    /// What one player is asking their ship to do this update, whatever device it came from.
    /// </summary>
    public struct ShipInput
    {
        /// <summary>Where the player wants to go, as a stick: Y runs down the screen.</summary>
        public Vector2 Move;

        /// <summary>Where the player wants to fire, as a stick: Y runs down the screen.</summary>
        public Vector2 Aim;

        /// <summary>Keyboard steering: -1 turns left, 1 turns right.</summary>
        public float Turn;

        /// <summary>Keyboard thrust along the way the ship faces.</summary>
        public bool Thrust;

        /// <summary>Fire straight ahead.</summary>
        public bool FireAhead;

        /// <summary>Lay a mine behind the ship.</summary>
        public bool Mine;

        /// <summary>Hop over other ships (true only on the update the button went down).</summary>
        public bool Hop;

    }

    /// <summary>
    /// The keys one keyboard player uses. Any of an action's keys triggers it.
    /// </summary>
    public class KeyboardLayout
    {
        public readonly List<Keys> Left = new List<Keys>();
        public readonly List<Keys> Right = new List<Keys>();
        public readonly List<Keys> Thrust = new List<Keys>();
        public readonly List<Keys> Fire = new List<Keys>();
        public readonly List<Keys> Mine = new List<Keys>();
        public readonly List<Keys> Hop = new List<Keys>();

        public static KeyboardLayout Wasd()
        {
            var layout = new KeyboardLayout();
            layout.Left.Add(Keys.A);
            layout.Right.Add(Keys.D);
            layout.Thrust.Add(Keys.W);
            layout.Fire.Add(Keys.Tab);
            layout.Mine.Add(Keys.S);
            layout.Hop.Add(Keys.Space);
            return layout;
        }

        /// <summary>
        /// Both layouts at once, for the one player on a machine who has the whole keyboard to themselves
        /// (a network game): the arrow keys or WASD steer and thrust, and either side's fire and mine keys work.
        /// </summary>
        public static KeyboardLayout Everything()
        {
            var layout = new KeyboardLayout();
            foreach (KeyboardLayout part in new[] { Wasd(), Arrows() })
            {
                layout.Left.AddRange(part.Left);
                layout.Right.AddRange(part.Right);
                layout.Thrust.AddRange(part.Thrust);
                layout.Fire.AddRange(part.Fire);
                layout.Mine.AddRange(part.Mine);
                layout.Hop.AddRange(part.Hop);
            }
            return layout;
        }

        public static KeyboardLayout Arrows()
        {
            var layout = new KeyboardLayout();
            layout.Left.Add(Keys.Left);
            layout.Right.Add(Keys.Right);
            layout.Thrust.Add(Keys.Up);
            layout.Fire.Add(Keys.RightControl);
            layout.Fire.Add(Keys.RightAlt);
            layout.Mine.Add(Keys.Down);
            layout.Hop.Add(Keys.RightShift);
            return layout;
        }
    }

    /// <summary>
    /// One player's controls: a gamepad and, optionally, a keyboard layout. A ship reads
    /// its player's <see cref="ShipControls"/> and never names a key or a button, so a
    /// player can drive whichever ship they hold, in whichever slot.
    /// </summary>
    public class ShipControls
    {
        private const float FireThresholdSquared = 0.25f;

        private static readonly ShipControls localPlayer = new ShipControls(PlayerIndex.One, KeyboardLayout.Everything());

        /// <summary>
        /// The controls of the one player on this machine: gamepad one (and the touch controls), or the keyboard,
        /// in whichever layout they like.
        /// </summary>
        public static ShipControls ForLocalPlayer() => localPlayer;

        private readonly PlayerIndex pad;

        private GamePadState lastPad;
        private bool lastHopKey;

        /// <summary>The gamepad this player uses (it also shakes when the ship is hit).</summary>
        public PlayerIndex Pad => pad;

        /// <summary>The keys, or null for a gamepad only player.</summary>
        public KeyboardLayout Keyboard { get; }

        public ShipControls(PlayerIndex pad, KeyboardLayout keyboard)
        {
            this.pad = pad;
            Keyboard = keyboard;
        }

        /// <summary>Reads the devices. Call once an update: the "newly pressed" fields compare with the last call.</summary>
        public ShipInput Read()
        {
            GamePadState padState = VirtualGamePad.Apply(pad, GamePad.GetState(pad));
            KeyboardState keys = Microsoft.Xna.Framework.Input.Keyboard.GetState();

            var input = new ShipInput();
            input.Move = padState.ThumbSticks.Left;
            input.Move.Y = -input.Move.Y;

            input.Aim = padState.ThumbSticks.Right;
            input.Aim.Y = -input.Aim.Y;
            if (input.Aim.LengthSquared() <= FireThresholdSquared)
                input.Aim = Vector2.Zero;

            input.FireAhead = padState.Buttons.RightShoulder == ButtonState.Pressed;
            input.Mine = padState.Buttons.B == ButtonState.Pressed && lastPad.Buttons.B == ButtonState.Released;

            // A is the hop button on a pad and on the touch controls
            input.Hop = padState.Buttons.A == ButtonState.Pressed && lastPad.Buttons.A == ButtonState.Released;

            bool hopKey = Keyboard != null && IsDown(keys, Keyboard.Hop);
            input.Hop |= hopKey && !lastHopKey;
            lastHopKey = hopKey;

            if (Keyboard != null)
            {
                // The keys steer the ship only while no stick is pushed
                if (input.Move == Vector2.Zero)
                {
                    if (IsDown(keys, Keyboard.Left)) input.Turn -= 1f;
                    if (IsDown(keys, Keyboard.Right)) input.Turn += 1f;
                    input.Thrust = IsDown(keys, Keyboard.Thrust);
                    input.FireAhead |= IsDown(keys, Keyboard.Fire);
                    input.Mine |= IsDown(keys, Keyboard.Mine);
                }
            }

            lastPad = padState;
            return input;
        }

        private static bool IsDown(KeyboardState state, List<Keys> keys)
        {
            foreach (Keys key in keys)
            {
                if (state.IsKeyDown(key))
                    return true;
            }
            return false;
        }
    }
}
