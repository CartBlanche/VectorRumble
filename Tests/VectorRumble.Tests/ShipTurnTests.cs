using Xunit;

namespace VectorRumble.Tests
{
    public class ShipTurnTests
    {
        const float Frame = 1f / 60f;

        [Fact]
        public void A_turn_takes_a_full_step_when_the_heading_is_far_off_either_way()
        {
            float right = Ship.TurnStep(1.5f, 1f, Frame);
            float left = Ship.TurnStep(1.5f, -1f, Frame);

            Assert.True(right > 0f);
            Assert.Equal(right, -left, 5);
        }

        [Fact]
        public void A_turn_stops_at_the_heading_instead_of_overshooting_it_to_the_left_as_well_as_to_the_right()
        {
            // a heading 0.02 radians off is less than one step (0.1 radians at 60 Hz)
            Assert.Equal(0.02f, Ship.TurnStep(0.02f, 1f, Frame), 5);
            Assert.Equal(-0.02f, Ship.TurnStep(0.02f, -1f, Frame), 5);
        }

        [Fact]
        public void A_nose_turning_to_the_left_settles_on_its_heading_rather_than_swinging_either_side_of_it()
        {
            float nose = 0f, target = -1f;
            for (int i = 0; i < 60; i++)
            {
                float diff = System.Math.Abs(target - nose);
                float facing = target > nose ? 1f : -1f;
                nose += Ship.TurnStep(diff, facing, Frame);
            }

            Assert.Equal(target, nose, 4);
        }
    }
}
