using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>
    /// The colours a pilot can fly in. A fixed set, so that no two pilots in a network match need share a
    /// colour by chance, and every colour reads well on the dark arena.
    /// </summary>
    static class ShipPalette
    {
        public static readonly Color[] Colours =
        {
            new Color(255, 154, 31),
            new Color(53, 242, 255),
            new Color(255, 79, 208),
            new Color(141, 255, 61),
            new Color(255, 210, 63),
            new Color(255, 61, 74),
            new Color(79, 107, 255),
            new Color(170, 120, 255),
        };

        /// <summary>The colour's name in the player's language.</summary>
        public static string Name(int index)
        {
            switch (index)
            {
                case 0: return Strings.Colour_Orange;
                case 1: return Strings.Colour_Cyan;
                case 2: return Strings.Colour_Magenta;
                case 3: return Strings.Colour_Lime;
                case 4: return Strings.Colour_Yellow;
                case 5: return Strings.Colour_Red;
                case 6: return Strings.Colour_Blue;
                default: return Strings.Colour_Violet;
            }
        }
    }
}
