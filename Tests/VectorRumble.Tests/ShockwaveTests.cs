using Microsoft.Xna.Framework;
using VectorEngine.Effects;
using Xunit;

namespace VectorRumble.Tests
{
    public class ShockwaveTests
    {
        [Fact]
        public void A_shockwave_is_three_rings_that_are_all_gone_after_about_a_second()
        {
            var field = new ParticleField();
            Shockwave.Emit(field, new Vector2(400, 300), Shockwave.ShipRadius, Color.Cyan);
            Assert.Equal(Shockwave.Rings, field.ParticleCount);

            for (int i = 0; i < 70; i++)
            {
                field.Update(1f / 60f);
            }

            Assert.True(field.ParticleCount < Shockwave.Rings);

            for (int i = 0; i < 60; i++)
            {
                field.Update(1f / 60f);
            }

            Assert.Equal(0, field.ParticleCount);
        }

        [Fact]
        public void No_radius_or_no_field_draws_nothing_and_does_not_fail()
        {
            var field = new ParticleField();
            Shockwave.Emit(field, Vector2.Zero, 0f, Color.White);
            Shockwave.Emit(null, Vector2.Zero, 100f, Color.White);
            Assert.Equal(0, field.ParticleCount);
        }

        [Fact]
        public void The_mine_and_rocket_rings_reach_past_their_blasts()
        {
            Assert.True(Shockwave.MineRadius > 80f);
            Assert.True(Shockwave.RocketRadius > 128f);
            Assert.True(Shockwave.ShipRadius > Shockwave.RocketRadius);
            Assert.True(Shockwave.LandingRadius < Shockwave.MineRadius);
        }
    }
}
