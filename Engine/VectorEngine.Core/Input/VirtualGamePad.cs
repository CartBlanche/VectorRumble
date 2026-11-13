using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using VectorEngine.Rendering;

namespace VectorEngine.Input
{
    /// <summary>A screen corner that a virtual control is placed relative to.</summary>
    public enum PadAnchor { TopLeft, TopRight, BottomLeft, BottomRight }

    /// <summary>A floating thumb stick: it appears under the thumb that touches its zone.</summary>
    public sealed class VirtualStick
    {
        /// <summary>Where the stick rests when untouched, from <see cref="Anchor"/>, in layout units (see <see cref="VirtualGamePadLayout.BaseHeight"/>).</summary>
        public PadAnchor Anchor { get; init; }
        public Vector2 Offset { get; init; }
        /// <summary>The picture of the stick in the pad texture.</summary>
        public Rectangle Sprite { get; init; }
    }

    /// <summary>A round on-screen button that presses a gamepad button.</summary>
    public sealed class VirtualButton
    {
        /// <summary>The gamepad button it presses.</summary>
        public Buttons Button { get; init; }
        public PadAnchor Anchor { get; init; }
        public Vector2 Offset { get; init; }
        public Rectangle Sprite { get; init; }
    }

    /// <summary>
    /// Where the touch controls go and how they behave. The defaults are VectorRumble's twin-stick layout: a left
    /// stick (move) in the left of the screen, a right stick (aim) in the rest, A, B and X at the bottom right and
    /// Start at the top right, using the sprites in <c>Textures/gamepad.png</c>. A game with other controls builds its own.
    /// </summary>
    public sealed class VirtualGamePadLayout
    {
        /// <summary>Positions and sizes are written for a screen this high and scaled to the real one.</summary>
        public float BaseHeight { get; init; } = 480f;
        /// <summary>How far a thumb moves a stick from where it landed to push it all the way, in layout units.</summary>
        public float StickTravel { get; init; } = 60f;
        public float DeadZone { get; init; } = 0.15f;
        /// <summary>A touch in this left fraction of the screen (and not on a button) takes the left stick, the rest the right.</summary>
        public float LeftZoneFraction { get; init; } = 0.4f;
        public float ButtonHitRadius { get; init; } = 48f;
        public float ButtonScale { get; init; } = 1.5f;
        /// <summary>How far the left stick must be pushed before it also reads as a direction button (so menus can use it).</summary>
        public float DirectionThreshold { get; init; } = 0.5f;

        public VirtualStick LeftStick { get; init; } = new()
        {
            Anchor = PadAnchor.BottomLeft, Offset = new Vector2(110f, -110f), Sprite = new Rectangle(2, 2, 68, 68),
        };

        public VirtualStick RightStick { get; init; } = new()
        {
            Anchor = PadAnchor.BottomRight, Offset = new Vector2(-250f, -110f), Sprite = new Rectangle(3, 77, 67, 67),
        };

        /// <summary>The buttons, in the order they take a touch when circles overlap (and the order they are drawn).</summary>
        public VirtualButton[] Buttons { get; init; } =
        {
            new() { Button = Microsoft.Xna.Framework.Input.Buttons.A, Anchor = PadAnchor.BottomRight, Offset = new Vector2(-70f, -80f), Sprite = new Rectangle(73, 114, 36, 35) },
            new() { Button = Microsoft.Xna.Framework.Input.Buttons.B, Anchor = PadAnchor.BottomRight, Offset = new Vector2(-70f, -170f), Sprite = new Rectangle(73, 77, 36, 36) },
            // X is the fire-ahead / change-ship button: on a gamepad that is the right shoulder
            new() { Button = Microsoft.Xna.Framework.Input.Buttons.RightShoulder, Anchor = PadAnchor.BottomRight, Offset = new Vector2(-160f, -80f), Sprite = new Rectangle(73, 41, 36, 35) },
            new() { Button = Microsoft.Xna.Framework.Input.Buttons.Start, Anchor = PadAnchor.TopRight, Offset = new Vector2(-50f, 50f), Sprite = new Rectangle(73, 4, 36, 36) },
        };
    }

    /// <summary>
    /// On-screen twin-stick controls for touch devices. A player's gamepad state is the real gamepad's overlaid with
    /// whatever fingers are down on the controls. The touch tracking has no graphics in it (so it can be tested with
    /// made-up touches); <see cref="Draw"/> is separate.
    /// </summary>
    public class VirtualGamePad
    {
        private static readonly Buttons[] RealButtons =
        {
            Buttons.A, Buttons.B, Buttons.X, Buttons.Y, Buttons.Start, Buttons.Back,
            Buttons.LeftShoulder, Buttons.RightShoulder, Buttons.LeftStick, Buttons.RightStick,
            Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight, Buttons.BigButton,
            Buttons.LeftThumbstickUp, Buttons.LeftThumbstickDown, Buttons.LeftThumbstickLeft, Buttons.LeftThumbstickRight,
            Buttons.RightThumbstickUp, Buttons.RightThumbstickDown, Buttons.RightThumbstickLeft, Buttons.RightThumbstickRight,
        };

        /// <summary>The pad for the screen in play, or null when there is none (desktop). <see cref="Apply"/> reads it.</summary>
        public static VirtualGamePad Instance { get; set; }

        private readonly VirtualGamePadLayout layout;
        private readonly Texture2D texture;
        private readonly Vector2 screenSize;
        private readonly float scale;

        // fades out while nobody is touching the screen: a player with a gamepad need not see these
        private readonly IdleFade fade;

        private readonly int[] buttonIds;
        private int leftId = -1, rightId = -1;
        private Vector2 leftOrigin, leftPosition, rightOrigin, rightPosition;

        /// <summary>
        /// True where the controls may fade away when not in use (during play). They stay in view elsewhere,
        /// such as a screen that asks the player to hold A.
        /// </summary>
        public bool CanFade { get; set; }

        /// <summary>How visible the controls are, 0 to 1.</summary>
        public float Opacity => fade.Opacity;

        /// <param name="layout">Where the controls go; null for VectorRumble's twin-stick layout</param>
        /// <param name="texture">The pad sprites, only needed to <see cref="Draw"/></param>
        /// <param name="screenSize">The virtual screen's size, in the same space the touches are converted to</param>
        /// <param name="startHidden">True when a real gamepad is plugged in: the controls come when a finger touches</param>
        public VirtualGamePad(VirtualGamePadLayout layout, Texture2D texture, Vector2 screenSize, bool startHidden)
        {
            this.layout = layout ?? new VirtualGamePadLayout();
            this.texture = texture;
            this.screenSize = screenSize;
            scale = screenSize.Y / this.layout.BaseHeight;
            buttonIds = new int[this.layout.Buttons.Length];
            Array.Fill(buttonIds, -1);
            fade = new IdleFade(startHidden);
        }

        private Vector2 Place(PadAnchor anchor, Vector2 offset)
        {
            Vector2 corner = anchor switch
            {
                PadAnchor.TopLeft => Vector2.Zero,
                PadAnchor.TopRight => new Vector2(screenSize.X, 0f),
                PadAnchor.BottomLeft => new Vector2(0f, screenSize.Y),
                _ => screenSize,
            };
            return corner + offset * scale;
        }

        private bool InButton(VirtualButton button, Vector2 position)
        {
            float radius = layout.ButtonHitRadius * scale;
            return Vector2.DistanceSquared(Place(button.Anchor, button.Offset), position) <= radius * radius;
        }

        /// <summary>Lets go of everything, for when the screen stops reading the controls.</summary>
        public void Reset()
        {
            leftId = rightId = -1;
            Array.Fill(buttonIds, -1);

            // when the screen has the controls back, they start their wait to fade afresh
            fade.Wake();
        }

        /// <summary>Whether a finger is holding the button that presses <paramref name="button"/>.</summary>
        public bool IsHeld(Buttons button)
        {
            for (int i = 0; i < buttonIds.Length; i++)
                if (layout.Buttons[i].Button == button && buttonIds[i] >= 0)
                    return true;
            return false;
        }

        /// <summary>
        /// Tracks the fingers down on the controls. Call once a frame, before anything reads the state, with the
        /// touches the input state read for the frame.
        /// </summary>
        /// <param name="toVirtual">Turns a touch position into the virtual screen's; null for <see cref="Display.ToVirtual"/></param>
        public void Update(TouchCollection touches, float elapsedSeconds, Func<Vector2, Vector2> toVirtual = null)
        {
            toVirtual ??= Display.ToVirtual;

            bool touched = false;
            foreach (TouchLocation touch in touches)
            {
                if (touch.State == TouchLocationState.Pressed || touch.State == TouchLocationState.Moved)
                    touched = true;

                switch (touch.State)
                {
                    case TouchLocationState.Pressed:
                        Press(touch.Id, toVirtual(touch.Position));
                        break;
                    case TouchLocationState.Moved:
                        if (touch.Id == leftId) leftPosition = toVirtual(touch.Position);
                        if (touch.Id == rightId) rightPosition = toVirtual(touch.Position);
                        break;
                    case TouchLocationState.Released:
                    case TouchLocationState.Invalid:
                        Release(touch.Id);
                        break;
                }
            }

            fade.Update(elapsedSeconds, touched, CanFade);
        }

        private void Press(int id, Vector2 position)
        {
            for (int i = 0; i < buttonIds.Length; i++)
            {
                if (InButton(layout.Buttons[i], position))
                {
                    buttonIds[i] = id;
                    return;
                }
            }

            if (position.X < screenSize.X * layout.LeftZoneFraction)
            {
                if (leftId < 0)
                {
                    leftId = id;
                    leftOrigin = leftPosition = position;
                }
            }
            else if (rightId < 0)
            {
                rightId = id;
                rightOrigin = rightPosition = position;
            }
        }

        private void Release(int id)
        {
            if (id == leftId) leftId = -1;
            if (id == rightId) rightId = -1;
            for (int i = 0; i < buttonIds.Length; i++)
                if (buttonIds[i] == id)
                    buttonIds[i] = -1;
        }

        private Vector2 StickValue(int id, Vector2 origin, Vector2 position)
        {
            if (id < 0)
                return Vector2.Zero;

            // Screen Y runs down, a gamepad stick's runs up
            Vector2 delta = (position - origin) / (layout.StickTravel * scale);
            delta.Y = -delta.Y;
            if (delta.LengthSquared() > 1f)
                delta.Normalize();
            return delta.LengthSquared() < layout.DeadZone * layout.DeadZone ? Vector2.Zero : delta;
        }

        /// <summary>Overlays the touch controls on a real gamepad state.</summary>
        public GamePadState GetState(GamePadState real)
        {
            Vector2 touchLeft = StickValue(leftId, leftOrigin, leftPosition);
            Vector2 left = touchLeft == Vector2.Zero ? real.ThumbSticks.Left : touchLeft;
            Vector2 touchRight = StickValue(rightId, rightOrigin, rightPosition);
            Vector2 right = touchRight == Vector2.Zero ? real.ThumbSticks.Right : touchRight;

            Buttons pressed = 0;
            foreach (Buttons button in RealButtons)
            {
                if (real.IsButtonDown(button))
                    pressed |= button;
            }
            for (int i = 0; i < buttonIds.Length; i++)
            {
                if (buttonIds[i] >= 0)
                    pressed |= layout.Buttons[i].Button;
            }

            // The left stick also reads as buttons, which is how the menus move through their entries
            if (touchLeft.Y > layout.DirectionThreshold) pressed |= Buttons.LeftThumbstickUp;
            if (touchLeft.Y < -layout.DirectionThreshold) pressed |= Buttons.LeftThumbstickDown;
            if (touchLeft.X < -layout.DirectionThreshold) pressed |= Buttons.LeftThumbstickLeft;
            if (touchLeft.X > layout.DirectionThreshold) pressed |= Buttons.LeftThumbstickRight;

            return new GamePadState(new GamePadThumbSticks(left, right), real.Triggers,
                new GamePadButtons(pressed), real.DPad);
        }

        /// <summary>The state a player should read: the touch controls over the real pad. Only player one is touch controlled.</summary>
        public static GamePadState Apply(PlayerIndex player, GamePadState real) =>
            Instance != null && player == PlayerIndex.One ? Instance.GetState(real) : real;

        /// <summary>Draws the controls with the pad texture given to the constructor.</summary>
        public void Draw(SpriteBatch spriteBatch)
        {
            // faded right out: nothing to draw (the controls still work, a touch shows them again)
            float visible = fade.Opacity;
            if (visible <= 0.01f || texture == null)
                return;

            DrawStick(spriteBatch, layout.LeftStick, leftId, leftOrigin, leftPosition, visible);
            DrawStick(spriteBatch, layout.RightStick, rightId, rightOrigin, rightPosition, visible);
            for (int i = 0; i < buttonIds.Length; i++)
            {
                VirtualButton button = layout.Buttons[i];
                DrawButton(spriteBatch, button.Sprite, Place(button.Anchor, button.Offset), buttonIds[i] >= 0, visible);
            }
        }

        private void DrawStick(SpriteBatch spriteBatch, VirtualStick stick, int id, Vector2 origin, Vector2 position, float visible)
        {
            bool active = id >= 0;
            Vector2 center = active ? origin : Place(stick.Anchor, stick.Offset);
            Vector2 offset = active ? position - origin : Vector2.Zero;
            float travel = layout.StickTravel * scale;
            if (offset.Length() > travel)
                offset = Vector2.Normalize(offset) * travel;

            var half = new Vector2(stick.Sprite.Width, stick.Sprite.Height) / 2f;
            Color color = Color.White * ((active ? 0.7f : 0.35f) * visible);
            spriteBatch.Draw(texture, center, stick.Sprite, color * 0.6f, 0f, half, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(texture, center + offset, stick.Sprite, color, 0f, half, scale * 0.6f, SpriteEffects.None, 0f);
        }

        private void DrawButton(SpriteBatch spriteBatch, Rectangle source, Vector2 center, bool down, float visible)
        {
            var half = new Vector2(source.Width, source.Height) / 2f;
            Color color = Color.White * ((down ? 0.9f : 0.4f) * visible);
            spriteBatch.Draw(texture, center, source, color, 0f, half, scale * layout.ButtonScale, SpriteEffects.None, 0f);
        }
    }
}
