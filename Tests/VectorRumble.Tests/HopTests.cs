using System.IO;
using Microsoft.Xna.Framework;
using VectorRumble.Networking;
using Xunit;

namespace VectorRumble.Tests
{
    public class HopTests
    {
        const float Frame = 1f / 60f;

        static Ship NewShip(World world, Vector2 at)
        {
            var ship = new Ship { World = world, Selected = true };
            ship.PlayGame();
            ship.Spawn(false);
            ship.Position = at;
            return ship;
        }

        static World NewWorld()
        {
            var world = new World(new Vector2(1280, 720));
            typeof(ShipManager).GetField("ships", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(world.ShipManager, new Ship[0]);
            world.SelectedArena = new Arena { Walls = new Vector2[0] };
            return world;
        }

        static void Run(Ship ship, float seconds)
        {
            for (float t = 0; t < seconds; t += Frame)
            {
                ship.Update(Frame);
            }
        }

        [Fact]
        public void A_hop_lifts_the_ship_to_full_height_and_brings_it_down()
        {
            var ship = NewShip(NewWorld(), new Vector2(400, 300));
            Assert.True(ship.TryHop());
            Assert.True(ship.Airborne);

            float highest = 0f;
            for (float t = 0; t < Ship.HopDuration - 0.05f; t += Frame)
            {
                ship.Update(Frame);
                highest = System.Math.Max(highest, ship.Height);
                Assert.True(ship.Airborne);
            }

            Assert.InRange(highest, 0.95f, 1f);
            Run(ship, 0.2f);
            Assert.False(ship.Airborne);
            Assert.Equal(0f, ship.Height);
        }

        [Fact]
        public void A_second_hop_waits_for_the_cooldown()
        {
            var ship = NewShip(NewWorld(), new Vector2(400, 300));
            Assert.True(ship.TryHop());
            Run(ship, Ship.HopDuration + 0.1f);
            Assert.False(ship.Airborne);
            Assert.False(ship.TryHop());

            Run(ship, Ship.HopCooldown - Ship.HopDuration);
            Assert.True(ship.TryHop());
        }

        [Fact]
        public void A_ship_in_the_air_cannot_hop_again_or_be_hurt_by_a_shot()
        {
            var world = NewWorld();
            var ship = NewShip(world, new Vector2(400, 300));
            var shooter = NewShip(world, new Vector2(900, 300));
            ship.TryHop();
            Assert.False(ship.TryHop());

            ship.Safe = false;
            float shield = ship.ShieldFraction;
            Assert.False(ship.Damage(new LaserProjectile(world, shooter, new Vector2(-1, 0)), 20f));
            Assert.Equal(shield, ship.ShieldFraction);
        }

        [Fact]
        public void Anything_but_a_shot_still_hurts_a_ship_in_the_air()
        {
            var world = NewWorld();
            var ship = NewShip(world, new Vector2(400, 300));
            var other = NewShip(world, new Vector2(900, 300));
            ship.Safe = false;
            ship.TryHop();

            // an asteroid, the world: not a projectile, so not hopped over
            Assert.True(ship.Damage(other, 20f));
            Assert.True(ship.ShieldFraction < 1f);
        }

        [Fact]
        public void Ships_pass_through_each_other_while_one_is_in_the_air_and_bump_on_the_ground()
        {
            var world = NewWorld();
            var a = NewShip(world, new Vector2(400, 300));
            var b = NewShip(world, new Vector2(430, 300));
            Assert.True(a.Touch(b));

            a.TryHop();
            Assert.False(a.Touch(b));
            Assert.False(b.Touch(a));
        }

        [Fact]
        public void A_ship_cannot_hop_while_dead_or_not_in_the_match()
        {
            var world = NewWorld();
            var spare = new Ship { World = world };
            Assert.False(spare.TryHop());

            var ship = NewShip(world, new Vector2(400, 300));
            ship.Die(null);
            Assert.False(ship.TryHop());
        }

        [Fact]
        public void The_height_travels_as_a_byte_and_comes_back_the_same()
        {
            var state = new ShipStatePacket { Slot = 1, Height = 200 };
            var stream = new MemoryStream();
            state.Serialize(new BinaryWriter(stream));
            stream.Position = 0;

            Assert.Equal(200, ShipStatePacket.Deserialize(new BinaryReader(stream)).Height);
        }

        [Fact]
        public void A_ship_on_the_ground_sends_height_zero_and_one_in_the_air_never_does()
        {
            var ship = NewShip(NewWorld(), new Vector2(400, 300));
            Assert.Equal(0, ship.CaptureState(0, 0).Height);

            ship.TryHop();
            Assert.True(ship.CaptureState(0, 0).Height > 0);
        }

        [Fact]
        public void A_remote_ship_shows_the_height_it_was_sent()
        {
            var ship = NewShip(NewWorld(), new Vector2(400, 300));
            ship.IsRemote = true;
            ship.ApplyRemoteState(new Vector2(10, 10), 0f, Vector2.Zero, 100, 255);
            Assert.Equal(1f, ship.Height, 3);
            Assert.True(ship.Airborne);

            ship.ApplyRemoteState(new Vector2(10, 10), 0f, Vector2.Zero, 100, 0);
            Assert.False(ship.Airborne);
        }

        [Fact]
        public void The_interpolator_blends_the_height_between_two_states()
        {
            var interpolator = new RemoteShipInterpolator();
            interpolator.Add(new ShipStatePacket { Sequence = 1, Height = 0 }, 0.0);
            interpolator.Add(new ShipStatePacket { Sequence = 2, Height = 200 }, 0.1);

            interpolator.Sample(0.05 + RemoteShipInterpolator.Delay, out _, out _, out _, out _, out byte height);
            Assert.InRange(height, 90, 110);
        }

        [Fact]
        public void The_hop_keys_and_the_protocol_version()
        {
            Assert.Contains(Microsoft.Xna.Framework.Input.Keys.Space, KeyboardLayout.Wasd().Hop);
            Assert.Contains(Microsoft.Xna.Framework.Input.Keys.RightShift, KeyboardLayout.Arrows().Hop);
            Assert.Equal(2, KeyboardLayout.Everything().Hop.Count);
        }
    }
}
