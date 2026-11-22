using System;
using Microsoft.Xna.Framework;
using Xunit;

namespace VectorRumble.Tests
{
    public class SkyTests
    {
        [Fact]
        public void A_sky_number_fits_in_one_byte_and_every_byte_is_valid()
        {
            Assert.InRange(SkyPool.Count, 1, 256);
            for (int i = 0; i < 256; i++)
            {
                Assert.NotNull(SkyPool.Get((byte)i));
            }
        }

        [Fact]
        public void The_pool_has_about_five_skies_with_different_seeds()
        {
            Assert.Equal(5, SkyPool.Count);
            var seeds = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < SkyPool.Count; i++)
            {
                Assert.True(seeds.Add(SkyPool.Get((byte)i).Seed));
            }
        }

        [Fact]
        public void Picking_a_sky_stays_inside_the_pool()
        {
            var random = new Random(1);
            for (int i = 0; i < 200; i++)
            {
                Assert.True(SkyPool.Pick(random) < SkyPool.Count);
            }
        }

        [Fact]
        public void Every_constellation_line_joins_two_stars_that_exist()
        {
            foreach (Constellation c in new[] { Constellations.Orion, Constellations.Plough, Constellations.Cassiopeia,
                Constellations.SouthernCross, Constellations.Cygnus, Constellations.Scorpius, Constellations.Lyra })
            {
                Assert.NotEmpty(c.Lines);
                foreach ((int a, int b) in c.Lines)
                {
                    Assert.InRange(a, 0, c.Stars.Length - 1);
                    Assert.InRange(b, 0, c.Stars.Length - 1);
                    Assert.NotEqual(a, b);
                }
            }
        }

        [Fact]
        public void A_constellation_projects_round_its_middle()
        {
            Vector2[] points = Constellations.Orion.Project(9f);
            Vector2 mean = Vector2.Zero;
            foreach (Vector2 p in points)
            {
                mean += p;
            }

            mean /= points.Length;
            Assert.True(mean.Length() < 1f);
        }

        [Fact]
        public void The_same_seed_gives_the_same_numbers_and_another_seed_does_not()
        {
            var a = new SkyRandom(11);
            var b = new SkyRandom(11);
            var c = new SkyRandom(12);
            bool differs = false;
            for (int i = 0; i < 50; i++)
            {
                float x = a.NextFloat();
                Assert.Equal(x, b.NextFloat());
                Assert.InRange(x, 0f, 0.99999994f);
                differs |= x != c.NextFloat();
            }

            Assert.True(differs);
        }

        [Fact]
        public void Every_nebula_hue_is_violet_magenta_or_teal()
        {
            for (int i = 0; i < SkyPool.Count; i++)
            {
                foreach (SkyCloud cloud in SkyPool.Get((byte)i).Clouds)
                {
                    Assert.Contains(cloud.Hue, new[] { 285f, 320f, 190f });
                }
            }
        }

        [Fact]
        public void A_sky_makes_the_same_number_of_nebula_dots_every_time()
        {
            var a = new Sky(SkyPool.Get(2));
            var b = new Sky(SkyPool.Get(2));
            Assert.Equal(a.DotCount, b.DotCount);
            Assert.Equal(650 * SkyPool.Get(2).Clouds.Length, a.DotCount);
        }

        [Fact]
        public void The_same_sky_number_gives_the_same_starfield()
        {
            var bounds = new Rectangle(-512, -512, 2304, 1744);
            var a = new Starfield(100, bounds, 3);
            var b = new Starfield(100, bounds, 3);
            Assert.Equal(3, a.SkyIndex);
            Assert.Equal(a.Sky.Entry, b.Sky.Entry);

            // and the number is kept when a sky is chosen again
            a.SetSky(4);
            Assert.Equal(4, a.SkyIndex);
        }

        [Fact]
        public void The_layers_follow_the_settings()
        {
            var settings = new VectorRumbleSettings();
            Assert.Equal(SkyLayers.Default, settings.SkyLayers);
            Assert.False((settings.SkyLayers & SkyLayers.Names) != 0);

            settings.SkyNebula = false;
            settings.SkyNames = true;
            Assert.False((settings.SkyLayers & SkyLayers.Nebula) != 0);
            Assert.True((settings.SkyLayers & SkyLayers.Names) != 0);
        }

        [Fact]
        public void The_sky_words_exist_in_every_language()
        {
            foreach (string key in new[] { "Label_Sky", "Sky_Stars", "Sky_Constellations", "Sky_Names", "Sky_Planets", "Sky_Nebula", "Sky_NebulaGlow", "Sky_Comet" })
            {
                string english = Strings.ResourceManager.GetString(key, System.Globalization.CultureInfo.InvariantCulture);
                Assert.False(string.IsNullOrWhiteSpace(english));
                foreach (string culture in LocalizationTests.Cultures)
                {
                    string translated = Strings.ResourceManager.GetString(key, new System.Globalization.CultureInfo(culture));
                    Assert.False(string.IsNullOrWhiteSpace(translated));
                }
            }
        }
    }
}
