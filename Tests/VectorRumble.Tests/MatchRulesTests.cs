using System.IO;
using Xunit;

namespace VectorRumble.Tests
{
    public class MatchRulesTests
    {
        static byte[] Write(MatchRules rules)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                rules.Write(writer);
                writer.Flush();
                return stream.ToArray();
            }
        }

        static MatchRules Read(byte[] bytes)
        {
            return MatchRules.Read(new BinaryReader(new MemoryStream(bytes)));
        }

        [Fact]
        public void Rules_round_trip()
        {
            var rules = new MatchRules
            {
                Arena = "Box",
                AsteroidDensity = AsteroidDensity.Medium,
                ScoreLimit = 20,
                ControllersCanShootInAllDirections = true,
            };

            var read = Read(Write(rules));

            Assert.Equal("Box", read.Arena);
            Assert.Equal(AsteroidDensity.Medium, read.AsteroidDensity);
            Assert.Equal(20, read.ScoreLimit);
            Assert.True(read.ControllersCanShootInAllDirections);
        }

        [Fact]
        public void Random_is_sent_without_its_translated_name()
        {
            var rules = new MatchRules { Arena = Strings.Arena_Random };

            byte[] bytes = Write(rules);

            // a length-prefixed string of length 0 comes first
            Assert.Equal(0, bytes[0]);
            Assert.True(Read(bytes).IsRandomArena);
        }

        static readonly string SomeId = new string('a', 64);

        [Fact]
        public void A_community_arena_travels_with_its_file_id()
        {
            var read = Read(Write(new MatchRules { Arena = "Maze", ArenaId = SomeId }));

            Assert.Equal("Maze", read.Arena);
            Assert.Equal(SomeId, read.ArenaId);
        }

        [Fact]
        public void Random_and_built_in_arenas_have_no_id()
        {
            Assert.Equal(string.Empty, Read(Write(new MatchRules { Arena = Strings.Arena_Random, ArenaId = SomeId })).ArenaId);
            Assert.Equal(string.Empty, Read(Write(new MatchRules { Arena = "One" })).ArenaId);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")] // upper case
        [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
        public void A_bad_arena_id_is_rejected(string id)
        {
            Assert.Throws<InvalidDataException>(() => Read(Write(new MatchRules { Arena = "Maze", ArenaId = id })));
        }

        [Fact]
        public void A_clone_is_independent_of_the_original()
        {
            var rules = new MatchRules { ScoreLimit = 10 };
            var clone = rules.Clone();

            clone.ScoreLimit = 25;

            Assert.Equal(10, rules.ScoreLimit);
        }

        [Fact]
        public void An_unknown_asteroid_density_is_rejected()
        {
            byte[] bytes = Write(new MatchRules { Arena = "Box" });
            bytes[1 + 3 + 1] = 99;    // after the arena name (1 byte length, 3 letters) and the arena id (1 byte length, empty)

            Assert.Throws<InvalidDataException>(() => Read(bytes));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(4)]
        [InlineData(26)]
        [InlineData(255)]
        public void A_score_limit_outside_the_allowed_range_is_rejected(int limit)
        {
            byte[] bytes = Write(new MatchRules { Arena = "Box" });
            bytes[1 + 3 + 1 + 1] = (byte)limit;

            Assert.Throws<InvalidDataException>(() => Read(bytes));
        }
    }
}
