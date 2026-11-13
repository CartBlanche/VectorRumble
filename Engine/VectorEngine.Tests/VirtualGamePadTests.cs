using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using VectorEngine.Input;
using Xunit;

namespace VectorEngine.Tests
{
    public class VirtualGamePadTests
    {
        // 1280x720 is 1.5 times the 480 high layout: the A button sits at (1175, 600), Start at (1205, 75)
        static readonly Vector2 Screen = new(1280f, 720f);
        static readonly Vector2 ButtonA = new(1175f, 600f);
        static readonly Vector2 ButtonB = new(1175f, 465f);
        static readonly Vector2 Start = new(1205f, 75f);

        static VirtualGamePad NewPad(bool startHidden = false) => new(null, null, Screen, startHidden);

        static TouchLocation Touch(int id, TouchLocationState state, Vector2 position) => new(id, state, position);

        static void Send(VirtualGamePad pad, params TouchLocation[] touches) =>
            pad.Update(new TouchCollection(touches), 1f / 60f, p => p);

        static GamePadState State(VirtualGamePad pad) => pad.GetState(new GamePadState());

        [Fact]
        public void Touching_a_button_presses_it_until_the_finger_lifts()
        {
            var pad = NewPad();
            Send(pad, Touch(1, TouchLocationState.Pressed, ButtonA));
            Assert.Equal(ButtonState.Pressed, State(pad).Buttons.A);
            Assert.Equal(ButtonState.Released, State(pad).Buttons.B);

            Send(pad, Touch(1, TouchLocationState.Released, ButtonA));
            Assert.Equal(ButtonState.Released, State(pad).Buttons.A);
        }

        [Fact]
        public void The_fire_ahead_button_is_the_right_shoulder_and_start_is_start()
        {
            var pad = NewPad();
            Send(pad, Touch(1, TouchLocationState.Pressed, new Vector2(1040f, 600f)),
                      Touch(2, TouchLocationState.Pressed, Start));
            Assert.Equal(ButtonState.Pressed, State(pad).Buttons.RightShoulder);
            Assert.Equal(ButtonState.Pressed, State(pad).Buttons.Start);
            Assert.True(pad.IsHeld(Buttons.Start));
            Assert.False(pad.IsHeld(Buttons.B));
        }

        [Fact]
        public void A_touch_on_a_button_does_not_start_a_stick()
        {
            var pad = NewPad();
            Send(pad, Touch(1, TouchLocationState.Pressed, ButtonB));
            Send(pad, Touch(1, TouchLocationState.Moved, ButtonB + new Vector2(0f, -200f)));
            Assert.Equal(Vector2.Zero, State(pad).ThumbSticks.Right);
            Assert.Equal(Vector2.Zero, State(pad).ThumbSticks.Left);
        }

        [Fact]
        public void The_left_zone_steers_and_up_on_screen_is_up_on_the_stick()
        {
            var pad = NewPad();
            var landed = new Vector2(200f, 400f);
            Send(pad, Touch(1, TouchLocationState.Pressed, landed));
            Assert.Equal(Vector2.Zero, State(pad).ThumbSticks.Left); // not moved yet

            Send(pad, Touch(1, TouchLocationState.Moved, landed + new Vector2(0f, -45f))); // half of 60 * 1.5
            Vector2 left = State(pad).ThumbSticks.Left;
            Assert.Equal(0f, left.X, 3);
            Assert.Equal(0.5f, left.Y, 3);

            Send(pad, Touch(1, TouchLocationState.Moved, landed + new Vector2(500f, 0f)));
            Assert.Equal(1f, State(pad).ThumbSticks.Left.X, 3); // clamped to full
        }

        [Fact]
        public void A_small_wobble_is_inside_the_dead_zone()
        {
            var pad = NewPad();
            Send(pad, Touch(1, TouchLocationState.Pressed, new Vector2(200f, 400f)));
            Send(pad, Touch(1, TouchLocationState.Moved, new Vector2(205f, 400f)));
            Assert.Equal(Vector2.Zero, State(pad).ThumbSticks.Left);
        }

        [Fact]
        public void The_left_stick_pushed_far_enough_also_reads_as_a_direction_button_for_menus()
        {
            var pad = NewPad();
            var landed = new Vector2(200f, 400f);
            Send(pad, Touch(1, TouchLocationState.Pressed, landed));
            Send(pad, Touch(1, TouchLocationState.Moved, landed + new Vector2(0f, -60f)));
            Assert.True(State(pad).IsButtonDown(Buttons.LeftThumbstickUp));
            Assert.False(State(pad).IsButtonDown(Buttons.LeftThumbstickDown));
        }

        [Fact]
        public void The_rest_of_the_screen_aims_and_each_stick_follows_its_own_finger()
        {
            var pad = NewPad();
            Send(pad, Touch(1, TouchLocationState.Pressed, new Vector2(200f, 400f)),
                      Touch(2, TouchLocationState.Pressed, new Vector2(800f, 400f)));
            Send(pad, Touch(2, TouchLocationState.Moved, new Vector2(890f, 400f)));
            GamePadState state = State(pad);
            Assert.Equal(1f, state.ThumbSticks.Right.X, 3);
            Assert.Equal(Vector2.Zero, state.ThumbSticks.Left);

            // a second finger in the aim zone is ignored while the first holds the stick
            Send(pad, Touch(3, TouchLocationState.Pressed, new Vector2(900f, 300f)));
            Send(pad, Touch(3, TouchLocationState.Moved, new Vector2(900f, 100f)));
            Assert.Equal(1f, State(pad).ThumbSticks.Right.X, 3);

            // lifting finger 2 frees the stick: it is centred, and finger 3 does not take it over
            Send(pad, Touch(2, TouchLocationState.Released, new Vector2(890f, 400f)));
            Assert.Equal(Vector2.Zero, State(pad).ThumbSticks.Right);
        }

        [Fact]
        public void The_real_gamepad_shows_through_where_no_finger_is()
        {
            var pad = NewPad();
            var real = new GamePadState(new GamePadThumbSticks(new Vector2(0f, 0.8f), new Vector2(-0.6f, 0f)),
                default, new GamePadButtons(Buttons.X), default);

            Send(pad, Touch(1, TouchLocationState.Pressed, ButtonA));
            GamePadState state = pad.GetState(real);

            Assert.Equal(0.8f, state.ThumbSticks.Left.Y, 3);
            Assert.Equal(-0.6f, state.ThumbSticks.Right.X, 3);
            Assert.Equal(ButtonState.Pressed, state.Buttons.X);
            Assert.Equal(ButtonState.Pressed, state.Buttons.A);
        }

        [Fact]
        public void Reset_lets_go_of_every_finger()
        {
            var pad = NewPad();
            Send(pad, Touch(1, TouchLocationState.Pressed, ButtonA),
                      Touch(2, TouchLocationState.Pressed, new Vector2(200f, 400f)));
            Send(pad, Touch(2, TouchLocationState.Moved, new Vector2(300f, 400f)));
            Assert.NotEqual(Vector2.Zero, State(pad).ThumbSticks.Left);

            pad.Reset();

            Assert.Equal(ButtonState.Released, State(pad).Buttons.A);
            Assert.Equal(Vector2.Zero, State(pad).ThumbSticks.Left);
        }

        [Fact]
        public void Apply_changes_only_player_one_and_only_when_a_pad_is_in_play()
        {
            var real = new GamePadState(default, default, new GamePadButtons(Buttons.B), default);
            VirtualGamePad.Instance = null;
            Assert.Equal(ButtonState.Pressed, VirtualGamePad.Apply(PlayerIndex.One, real).Buttons.B);

            var pad = NewPad();
            VirtualGamePad.Instance = pad;
            try
            {
                Send(pad, Touch(1, TouchLocationState.Pressed, ButtonA));
                Assert.Equal(ButtonState.Pressed, VirtualGamePad.Apply(PlayerIndex.One, real).Buttons.A);
                Assert.Equal(ButtonState.Released, VirtualGamePad.Apply(PlayerIndex.Two, real).Buttons.A);
            }
            finally
            {
                VirtualGamePad.Instance = null;
            }
        }

        [Fact]
        public void With_a_real_gamepad_the_controls_start_out_of_sight_and_a_touch_brings_them_back()
        {
            var pad = NewPad(startHidden: true);
            Assert.Equal(0f, pad.Opacity);

            for (int i = 0; i < 30; i++)
                Send(pad, Touch(1, i == 0 ? TouchLocationState.Pressed : TouchLocationState.Moved, new Vector2(800f, 400f)));
            Assert.True(pad.Opacity > 0.9f);
        }

        [Fact]
        public void A_game_can_lay_out_its_own_controls()
        {
            var layout = new VirtualGamePadLayout
            {
                Buttons = new[]
                {
                    new VirtualButton { Button = Buttons.Y, Anchor = PadAnchor.TopLeft, Offset = new Vector2(100f, 100f) },
                },
            };
            var pad = new VirtualGamePad(layout, null, Screen, false);

            Send(pad, Touch(1, TouchLocationState.Pressed, new Vector2(150f, 150f)));
            Assert.Equal(ButtonState.Pressed, State(pad).Buttons.Y);
            Assert.Equal(ButtonState.Released, State(pad).Buttons.A);
        }
    }
}
