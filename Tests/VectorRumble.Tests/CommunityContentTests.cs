using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Xunit;

namespace VectorRumble.Tests
{
    public class CommunityContentTests
    {
        static byte[] Bytes(string xml) => Encoding.UTF8.GetBytes(xml);

        static string ArenaXml(string name = "Box", string color = "FF00FFFF", string walls = "0.1 0.1 0.9 0.1 0.9 0.1 0.9 0.9", string extra = "") =>
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><XnaContent xmlns:VectorRumble=\"VectorRumble\"><Asset Type=\"VectorRumble:Arena\">" +
            "<Name>" + name + "</Name>" + extra + "<Color>" + color + "</Color><Walls>" + walls + "</Walls></Asset></XnaContent>";

        static string ShipXml(string points = "0 -10 8 8 0 4 -8 8", string mass = "20", string radius = "15", string player = "<PlayerIndex>One</PlayerIndex>", string name = "Dart", string thrusters = "") =>
            "<XnaContent><Asset Type=\"VectorRumble:Ship\"><Mass>" + mass + "</Mass><Radius>" + radius + "</Radius><Color>FF00FF00</Color>" +
            "<Shape><Points>" + points + "</Points></Shape>" + thrusters + "<Name>" + name + "</Name>" + player + "</Asset></XnaContent>";

        static string ArenaReason(string xml)
        {
            Assert.False(CommunityContent.TryParseArena(Bytes(xml), out Arena arena, out string reason));
            Assert.Null(arena);
            Assert.False(string.IsNullOrEmpty(reason));
            return reason;
        }

        static string ShipReason(string xml)
        {
            Assert.False(CommunityContent.TryParseShip(Bytes(xml), out Ship ship, out string reason));
            Assert.Null(ship);
            Assert.False(string.IsNullOrEmpty(reason));
            return reason;
        }

        [Fact]
        public void A_good_arena_loads_with_its_extras()
        {
            string xml = ArenaXml(extra: "<Author>Ann</Author><Description>Tight corners</Description>");
            Assert.True(CommunityContent.TryParseArena(Bytes(xml), out Arena arena, out _));
            Assert.Equal("Box", arena.Name);
            Assert.Equal(4, arena.Walls.Length);
            Assert.Equal("Ann", arena.Author);
            Assert.Equal("Tight corners", arena.Description);
            Assert.True(arena.IsCommunity);
            Assert.Equal(64, arena.Id.Length);
            Assert.Equal(CommunityContent.IdOf(Bytes(xml)), arena.Id);
        }

        [Fact]
        public void Any_whitespace_between_numbers_is_fine_and_a_six_digit_colour_is_opaque()
        {
            Assert.True(CommunityContent.TryParseArena(Bytes(ArenaXml(color: "00FF00", walls: " 0.1  0.1\n0.9\t0.1 \r\n 0.9 0.1 0.9 0.9 ")), out Arena arena, out _));
            Assert.Equal(4, arena.Walls.Length);
            Assert.Equal(255, arena.Color.A);
            Assert.Equal(255, arena.Color.G);
        }

        [Fact]
        public void Coordinates_outside_the_screen_are_clamped()
        {
            Assert.True(CommunityContent.TryParseArena(Bytes(ArenaXml(walls: "-5 0.5 9 0.5")), out Arena arena, out _));
            Assert.Equal(0f, arena.Walls[0].X);
            Assert.Equal(1f, arena.Walls[1].X);
        }

        [Fact]
        public void Bad_arenas_are_refused_with_a_reason_and_never_throw()
        {
            Assert.Contains("number", ArenaReason(ArenaXml(walls: "0.1 0.1 x 0.1")));
            Assert.Contains("four", ArenaReason(ArenaXml(walls: "0.1 0.1 0.9")));
            Assert.Contains("number", ArenaReason(ArenaXml(walls: "0.1 0.1 NaN 0.1")));
            Assert.Contains("number", ArenaReason(ArenaXml(walls: "0.1 0.1 Infinity 0.1")));
            Assert.Contains("Walls", ArenaReason(ArenaXml(walls: "")));
            Assert.Contains("Color", ArenaReason(ArenaXml(color: "red")));
            Assert.Contains("Name", ArenaReason(ArenaXml(name: "  ")));
            Assert.Contains("walls", ArenaReason(ArenaXml(walls: string.Join(" ", Enumerable.Repeat("0.1 0.1 0.2 0.2", CommunityContent.MaxWalls + 1)))));
            Assert.Contains("XML", ArenaReason("<XnaContent><Asset"));
            Assert.Contains("not a game file", ArenaReason("<html/>"));
            Assert.Contains("empty", ArenaReason(""));
            Assert.Contains("Ship", ArenaReason(ShipXml()).Replace("ship", "Ship"));
        }

        [Fact]
        public void A_missing_element_does_not_crash()
        {
            Assert.False(CommunityContent.TryParseArena(Bytes("<XnaContent><Asset Type=\"VectorRumble:Arena\"><Name>X</Name></Asset></XnaContent>"), out _, out string reason));
            Assert.NotEmpty(reason);
            Assert.False(CommunityContent.TryParseShip(Bytes("<XnaContent><Asset Type=\"VectorRumble:Ship\"/></XnaContent>"), out _, out reason));
            Assert.NotEmpty(reason);
        }

        [Fact]
        public void Doctype_declarations_are_not_processed()
        {
            string xml = "<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY e SYSTEM \"file:///etc/passwd\">]>" + ArenaXml(name: "&e;").Substring(ArenaXml().IndexOf("<XnaContent", StringComparison.Ordinal));
            Assert.False(CommunityContent.TryParseArena(Bytes(xml), out _, out string reason));
            Assert.Contains("XML", reason);
        }

        [Fact]
        public void Too_big_a_file_is_refused()
        {
            Assert.False(CommunityContent.TryParseArena(new byte[CommunityContent.MaxFileBytes + 1], out _, out string reason));
            Assert.Contains("big", reason);
        }

        [Fact]
        public void Names_are_cleaned_and_cut()
        {
            Assert.Equal("AB", CommunityContent.CleanText("A\u0007\nB  ", 40));
            Assert.Equal(10, CommunityContent.CleanText(new string('x', 99), 10).Length);
            Assert.True(CommunityContent.TryParseArena(Bytes(ArenaXml(name: new string('n', 100))), out Arena arena, out _));
            Assert.Equal(CommunityContent.MaxNameLength, arena.Name.Length);
        }

        [Fact]
        public void A_good_ship_loads_and_is_always_a_spare()
        {
            Assert.True(CommunityContent.TryParseShip(Bytes(ShipXml(player: "<PlayerIndex>One</PlayerIndex>")), out Ship ship, out _));
            Assert.Equal("Dart", ship.Name);
            Assert.Equal(20, ship.Mass);
            Assert.Equal(15, ship.Radius);
            Assert.Equal(4, ship.Shape.Points.Length);
            Assert.Equal(string.Empty, ship.PlayerIndex); // never one of the numbered ships
            Assert.True(CommunityContent.TryParseShip(Bytes(ShipXml(player: "")), out ship, out _)); // PlayerIndex missing is fine
            Assert.True(ship.IsCommunity);
        }

        [Fact]
        public void Bad_ships_are_refused_with_a_reason()
        {
            Assert.Contains("points", ShipReason(ShipXml(points: "0 0 1 1")));
            Assert.Contains("points", ShipReason(ShipXml(points: string.Join(" ", Enumerable.Repeat("1 1", 25)))));
            Assert.Contains("Points", ShipReason(ShipXml(points: "0 0 1 1 2")));
            Assert.Contains("centre", ShipReason(ShipXml(points: "0 -90 8 8 -8 8")));
            Assert.Contains("number", ShipReason(ShipXml(points: "0 0 a 1 2 2")));
            Assert.Contains("Mass", ShipReason(ShipXml(mass: "9999")));
            Assert.Contains("Mass", ShipReason(ShipXml(mass: "heavy")));
            Assert.Contains("Radius", ShipReason(ShipXml(radius: "0")));
            Assert.Contains("Name", ShipReason(ShipXml(name: "")));
        }

        [Fact]
        public void Mass_and_radius_given_with_decimals_are_rounded_not_refused()
        {
            Assert.True(CommunityContent.TryParseShip(Bytes(ShipXml(mass: "20.4", radius: "15.6")), out Ship ship, out _));
            Assert.Equal(20, ship.Mass);
            Assert.Equal(16, ship.Radius);
        }

        [Fact]
        public void The_same_bytes_have_the_same_id_and_a_changed_byte_has_another()
        {
            byte[] a = Bytes(ArenaXml());
            byte[] b = (byte[])a.Clone();
            Assert.Equal(CommunityContent.IdOf(a), CommunityContent.IdOf(b));
            b[b.Length - 1] ^= 1;
            Assert.NotEqual(CommunityContent.IdOf(a), CommunityContent.IdOf(b));
        }

        [Fact]
        public void Reading_a_folder_skips_bad_files_and_duplicates_and_keeps_the_good_ones()
        {
            string folder = Path.Combine(Path.GetTempPath(), "vr-community-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllText(Path.Combine(folder, "a.xml"), ArenaXml("A"));
                File.WriteAllText(Path.Combine(folder, "a-copy.xml"), ArenaXml("A"));          // the same file again
                File.WriteAllText(Path.Combine(folder, "b.xml"), ArenaXml("B", walls: "1 2 3")); // parses as XML, not as an arena
                File.WriteAllText(Path.Combine(folder, "c.txt"), "not xml at all");              // not an .xml file
                File.WriteAllBytes(Path.Combine(folder, "huge.xml"), new byte[CommunityContent.MaxFileBytes + 10]);

                CommunityContent.ForgetSkipped("Arenas");
                var found = new List<(string, byte[], string)>();
                CommunityContent.ReadFolder(folder, "Arenas", found, new HashSet<string>());

                // a, b (readable bytes) but not the copy or the .txt; the huge file was skipped with a reason
                Assert.Equal(2, found.Count);
                Assert.Contains(CommunityContent.Skipped, s => s.Path.EndsWith("huge.xml") && s.Reason.Contains("KB"));
                int loaded = found.Count(f => CommunityContent.TryParseArena(f.Item2, out _, out _));
                Assert.Equal(1, loaded);
                CommunityContent.ForgetSkipped("Arenas");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void A_folder_that_does_not_exist_is_just_empty()
        {
            var found = new List<(string, byte[], string)>();
            CommunityContent.ReadFolder(Path.Combine(Path.GetTempPath(), "vr-nope-" + Guid.NewGuid().ToString("N")), "Ships", found, new HashSet<string>());
            Assert.Empty(found);
        }
    
        [Fact]
        public void ShipWithoutThrustersElementLeavesThemToTheGame()
        {
            Assert.True(CommunityContent.TryParseShip(Bytes(ShipXml()), out Ship ship, out _));
            Assert.Null(ship.Thrusters);
        }

        [Fact]
        public void ShipThrustersAreReadWithTheirSize()
        {
            string xml = ShipXml(thrusters: "<Thrusters><Thruster X=\"-3\" Y=\"8\"/><Thruster X=\"3.5\" Y=\"8\" Size=\"1.5\"/></Thrusters>");
            Assert.True(CommunityContent.TryParseShip(Bytes(xml), out Ship ship, out _));
            Assert.Equal(2, ship.Thrusters.Length);
            Assert.Equal(new Vector2(-3f, 8f), ship.Thrusters[0].At);
            Assert.Equal(1f, ship.Thrusters[0].Size);
            Assert.Equal(new Vector2(3.5f, 8f), ship.Thrusters[1].At);
            Assert.Equal(1.5f, ship.Thrusters[1].Size);
        }

        [Fact]
        public void EmptyThrustersElementMeansNoFlame()
        {
            Assert.True(CommunityContent.TryParseShip(Bytes(ShipXml(thrusters: "<Thrusters/>")), out Ship ship, out _));
            Assert.NotNull(ship.Thrusters);
            Assert.Empty(ship.Thrusters);
        }

        [Fact]
        public void ThrusterSizeIsKeptInRange()
        {
            string xml = ShipXml(thrusters: "<Thrusters><Thruster X=\"0\" Y=\"8\" Size=\"9\"/><Thruster X=\"2\" Y=\"8\" Size=\"0\"/></Thrusters>");
            Assert.True(CommunityContent.TryParseShip(Bytes(xml), out Ship ship, out _));
            Assert.Equal(CommunityContent.MaxThrusterSize, ship.Thrusters[0].Size);
            Assert.Equal(CommunityContent.MinThrusterSize, ship.Thrusters[1].Size);
        }

        [Fact]
        public void BadThrustersSkipTheShipWithAReason()
        {
            Assert.Contains("at most", ShipReason(ShipXml(thrusters: "<Thrusters>" + string.Concat(Enumerable.Repeat("<Thruster X=\"0\" Y=\"8\"/>", 4)) + "</Thrusters>")));
            Assert.Contains("X and Y", ShipReason(ShipXml(thrusters: "<Thrusters><Thruster X=\"a\" Y=\"8\"/></Thrusters>")));
            Assert.Contains("X and Y", ShipReason(ShipXml(thrusters: "<Thrusters><Thruster Y=\"8\"/></Thrusters>")));
            Assert.Contains("centre", ShipReason(ShipXml(thrusters: "<Thrusters><Thruster X=\"0\" Y=\"90\"/></Thrusters>")));
            Assert.Contains("Size", ShipReason(ShipXml(thrusters: "<Thrusters><Thruster X=\"0\" Y=\"8\" Size=\"big\"/></Thrusters>")));
        }
}
}
