using Microsoft.Xna.Framework;
using VectorEngine.Simulation;
using Xunit;

namespace VectorEngine.Tests
{
    public class WorldBaseTests
    {
        sealed class TestWorld : WorldBase
        {
            public Vector2[] WallPairs;
            public int Updated;
            public TestWorld() : base(new Vector2(1000, 1000)) { }
            protected override Vector2[] Walls => WallPairs;
            protected override void OnUpdated(float elapsedTime) => Updated++;
        }

        sealed class Mover : Actor
        {
            public WallBehavior Behavior = WallBehavior.Bounce;
            public Actor TouchedBy;
            public Mover(WorldBase world, Vector2 pos, Vector2 vel) : base(world)
            {
                position = pos; lastPosition = pos; velocity = vel; radius = 5f; dead = false;
            }
            public override WallBehavior WallBehavior => Behavior;
            public override bool Touch(Actor target) { TouchedBy = target; return true; }
        }

        // a vertical wall at x = 500 (the points are fractions of the world's size)
        static TestWorld WorldWithWall() => new TestWorld { WallPairs = new[] { new Vector2(0.5f, 0f), new Vector2(0.5f, 1f) } };

        [Fact]
        public void An_actor_bounces_off_a_wall()
        {
            var world = WorldWithWall();
            var ball = new Mover(world, new Vector2(498f, 100f), new Vector2(10f, 0f));
            world.Actors.Add(ball);
            world.Update(0.1f);
            Assert.True(ball.Velocity.X < 0f);
        }

        [Fact]
        public void A_touching_actor_touches_the_wall_actor_instead_of_bouncing()
        {
            var world = WorldWithWall();
            var wall = new WorldActor(world);
            world.Actors.Add(wall);
            world.WallActor = wall;
            var shot = new Mover(world, new Vector2(498f, 100f), new Vector2(100f, 0f)) { Behavior = WallBehavior.Touch };
            world.Actors.Add(shot);
            world.Update(0.1f);
            Assert.Same(wall, shot.TouchedBy);
            Assert.True(shot.Velocity.X > 0f);
        }

        [Fact]
        public void Without_a_wall_actor_the_first_actor_stands_in()
        {
            var world = WorldWithWall();
            var first = new WorldActor(world);
            world.Actors.Add(first);
            var shot = new Mover(world, new Vector2(498f, 100f), new Vector2(100f, 0f)) { Behavior = WallBehavior.Touch };
            world.Actors.Add(shot);
            world.Update(0.1f);
            Assert.Same(first, shot.TouchedBy);
        }

        [Fact]
        public void The_update_hook_runs_once_a_frame()
        {
            var world = WorldWithWall();
            world.Update(0.016f);
            world.Update(0.016f);
            Assert.Equal(2, world.Updated);
        }

        [Fact]
        public void A_spawn_point_is_inside_the_safe_area_and_repeatable_with_a_seed()
        {
            var a = new TestWorld { Random = new System.Random(7) };
            var b = new TestWorld { Random = new System.Random(7) };
            var actor = new Mover(a, Vector2.Zero, Vector2.Zero);
            Vector2 p1 = a.FindSpawnPoint(actor);
            Vector2 p2 = b.FindSpawnPoint(actor);
            Assert.Equal(p1, p2);
            Assert.True(a.SafeDimensions.Contains(p1));
        }
    }
}
