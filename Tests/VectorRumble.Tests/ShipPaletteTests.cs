using System.Linq;
using Xunit;

namespace VectorRumble.Tests
{
    public class ShipPaletteTests
    {
        [Fact]
        public void The_palette_has_eight_different_colours()
        {
            Assert.Equal(8, ShipPalette.Colours.Length);
            Assert.Equal(8, ShipPalette.Colours.Distinct().Count());
        }

        [Fact]
        public void Every_colour_has_a_name()
        {
            for (int i = 0; i < ShipPalette.Colours.Length; i++)
            {
                Assert.False(string.IsNullOrWhiteSpace(ShipPalette.Name(i)));
            }
        }

        [Fact]
        public void Every_colour_is_bright_enough_for_the_dark_arena()
        {
            // the brightest channel of a colour is at least 200 of 255
            Assert.All(ShipPalette.Colours, c => Assert.True(System.Math.Max(c.R, System.Math.Max(c.G, c.B)) >= 200));
        }
    }
}
