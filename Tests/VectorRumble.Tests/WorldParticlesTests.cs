using Microsoft.Xna.Framework;
using VectorEngine.Effects;
using Xunit;

namespace VectorRumble.Tests
{
    /// <summary>The world's explosions are VectorEngine particle effects held in World.Particles.</summary>
    public class WorldParticlesTests
    {
        static World NewWorld()
        {
            var world = new World(new Vector2(1280, 720));
            typeof(ShipManager).GetField("ships", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(world.ShipManager, new Ship[0]);
            return world;
        }

        [Fact]
        public void A_world_has_a_headless_particle_field_by_default()
        {
            var world = NewWorld();
            Assert.NotNull(world.Particles);
            Assert.Equal(0, world.Particles.ParticleCount);
        }

        [Fact]
        public void The_world_advances_its_explosions_and_they_end_when_their_life_does()
        {
            var world = NewWorld();
            // what a mine does: two bursts at once
            var colors = new[] { Color.Orange, Color.Red };
            world.Particles.Emit(ParticleEffects.Burst(64, 32f, 64f, 3f, 0.05f, colors), new Vector2(400, 300));
            world.Particles.Emit(ParticleEffects.Burst(16, 128f, 256f, 4f, 0.1f, colors), new Vector2(400, 300));
            Assert.Equal(80, world.Particles.ParticleCount);

            const float dt = 1f / 60f;
            for (int i = 0; i < (int)(3.5f / dt); i++) world.Update(dt);
            Assert.Equal(16, world.Particles.ParticleCount);       // the 3 second burst is over, the 4 second one is not

            for (int i = 0; i < (int)(1f / dt); i++) world.Update(dt);
            Assert.Equal(0, world.Particles.ParticleCount);
        }

        [Fact]
        public void A_screens_particle_manager_can_replace_the_headless_field()
        {
            var world = NewWorld();
            var replacement = new ParticleField();
            world.Particles = replacement;
            replacement.Emit(ParticleEffects.Burst(8, 32f, 64f, 1f, 0.05f, Color.White), Vector2.Zero);
            world.Update(1f / 60f);
            Assert.Equal(8, replacement.ParticleCount);
            Assert.Same(replacement, world.Particles);
        }
    }
}
