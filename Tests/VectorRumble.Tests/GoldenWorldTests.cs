using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Xna.Framework;
using VectorEngine.Simulation;
using Xunit;

namespace VectorRumble.Tests
{
    /// <summary>
    /// Golden-run safety net for the VectorEngine extraction: steps a headless World with scripted actors
    /// (no content, graphics or audio) and compares per-actor snapshots against a recorded baseline.
    /// Re-record deliberately with GOLDEN_RECORD=1; any other diff means simulation behaviour changed.
    /// </summary>
    public class GoldenWorldTests
    {
        private sealed class Ball : Actor
        {
            public Ball(World world, Vector2 pos, Vector2 vel, float radius, float mass) : base(world)
            {
                position = pos; lastPosition = pos; velocity = vel;
                this.radius = radius; this.mass = mass;
                dead = false; collidable = true;
            }
        }

        private static string GoldenPath([CallerFilePath] string here = "") =>
            Path.Combine(Path.GetDirectoryName(here)!, "Golden", "world_collisions.txt");

        private static string Run()
        {
            var world = new World(new Vector2(1280, 720));
            // no ships: ShipManager only fills its array when content loads, so give it an empty one
            typeof(ShipManager).GetField("ships", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(world.ShipManager, new Ship[0]);
            // a plain box arena (normalised wall coordinates) plus an inner pillar, so wall bounces are covered too
            world.SelectedArena = new Arena
            {
                Walls = new[]
                {
                    new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.02f),
                    new Vector2(0.98f, 0.02f), new Vector2(0.98f, 0.98f),
                    new Vector2(0.98f, 0.98f), new Vector2(0.02f, 0.98f),
                    new Vector2(0.02f, 0.98f), new Vector2(0.02f, 0.02f),
                    new Vector2(0.45f, 0.40f), new Vector2(0.55f, 0.60f),
                }
            };
            var rng = new Random(12345);
            for (int i = 0; i < 12; i++)
            {
                var pos = new Vector2(100 + rng.Next(1080), 100 + rng.Next(520));
                var vel = new Vector2(rng.Next(-300, 300), rng.Next(-300, 300));
                world.Actors.Add(new Ball(world, pos, vel, 14 + rng.Next(14), 1 + rng.Next(4)));
            }

            var sb = new StringBuilder();
            const float dt = 1f / 60f;
            for (int frame = 0; frame <= 600; frame++)
            {
                if (frame % 10 == 0)
                {
                    sb.Append("f").Append(frame).Append('\n');
                    foreach (var a in world.Actors)
                        sb.Append(a.Position.X.ToString("R")).Append(',').Append(a.Position.Y.ToString("R")).Append(' ')
                          .Append(a.Velocity.X.ToString("R")).Append(',').Append(a.Velocity.Y.ToString("R")).Append(' ')
                          .Append(a.Dead ? 'D' : 'A').Append('\n');
                }
                world.Update(dt);
            }
            return sb.ToString().Replace("\r\n", "\n");
        }

        [Fact]
        public void World_simulation_matches_golden_run()
        {
            string actual = Run();
            string path = GoldenPath();
            if (Environment.GetEnvironmentVariable("GOLDEN_RECORD") == "1" || !File.Exists(path))
            {
                File.WriteAllText(path, actual);
                return;
            }
            Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), actual);
        }

        [Fact]
        public void Golden_run_is_repeatable() => Assert.Equal(Run(), Run());
    }
}
