using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Xunit;

namespace VectorRumble.Tests
{
    public class ScreenshotTests
    {
        static readonly DateTime Moment = new DateTime(2026, 11, 14, 22, 15, 30);

        [Fact]
        public void A_picture_is_named_by_the_time_it_was_taken()
        {
            Assert.Equal("VectorRumble-2026-11-14-221530.png", Screenshots.FileName(Moment, n => false));
        }

        [Fact]
        public void Several_pictures_in_one_second_do_not_overwrite_each_other()
        {
            var taken = new HashSet<string> { "VectorRumble-2026-11-14-221530.png", "VectorRumble-2026-11-14-221530-2.png" };
            Assert.Equal("VectorRumble-2026-11-14-221530-3.png", Screenshots.FileName(Moment, taken.Contains));
        }

        [Fact]
        public void The_name_does_not_depend_on_the_players_language()
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
                Assert.Equal("VectorRumble-2026-11-14-221530.png", Screenshots.FileName(Moment, n => false));
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = culture;
            }
        }

        [Fact]
        public void The_picture_is_the_game_without_the_bars_round_it()
        {
            // a 16:9 game in a wider window: bars at the sides
            Assert.Equal(new Rectangle(280, 0, 1280, 720), Screenshots.GameArea(new Rectangle(280, 0, 1280, 720), 1840, 720));

            // a destination that overhangs the window is kept inside it
            Assert.Equal(new Rectangle(0, 0, 800, 450), Screenshots.GameArea(new Rectangle(-10, -5, 900, 500), 800, 450));
        }

        [Fact]
        public void A_picture_is_never_see_through()
        {
            var pixels = new[] { new Color(10, 20, 30, 0), new Color(1, 2, 3, 128) };
            Screenshots.MakeOpaque(pixels);
            Assert.All(pixels, p => Assert.Equal(255, p.A));
            Assert.Equal(10, pixels[0].R);
        }

        [Fact]
        public void The_folder_is_the_games_own_one_in_pictures()
        {
            Assert.EndsWith("VectorRumble", Screenshots.Folder());
        }
    }
}
