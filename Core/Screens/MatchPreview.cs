using System;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>
    /// The picture beside the single player rules: the ships that come with the game, you (in the ship and colour you
    /// flew last, if that is saved) on the left and one rival
    /// on the right for each computer pilot chosen, picked at random. Raising the number of pilots rolls the new rival;
    /// the ones already shown stay.
    /// </summary>
    class MatchPreview
    {
        const int MaxRivals = 3;

        readonly Vector2[][] shapes = new Vector2[MaxRivals + 1][]; // [0] is you
        readonly Color[] colours = new Color[MaxRivals + 1];
        readonly Random random = new Random();

        public MatchPreview()
        {
            var all = MenuDuel.Shapes();
            if (all.Count == 0)
            {
                return;
            }

            // you: the ship and colour from last time, a random ship if none was saved (or it is a community ship)
            int last = MenuDuel.Names().IndexOf(UserSettings.Current.LastShip);
            shapes[0] = all[last >= 0 ? last : random.Next(all.Count)];
            colours[0] = ShipPalette.Colours[Math.Max(0, Math.Min(ShipPalette.Colours.Length - 1, UserSettings.Current.LastColour))];
            colours[1] = colours[2] = colours[3] = default; // nothing taken yet
            for (int i = 1; i <= MaxRivals; i++)
            {
                Roll(i);
            }
        }

        /// <summary>False if the ships could not be read: the caller draws its plain picture.</summary>
        public bool Available => shapes[0] != null;

        /// <summary>A new rival in this slot (1 to 3): a ship and a colour, not yours.</summary>
        public void Roll(int slot)
        {
            var all = MenuDuel.Shapes();
            if (all.Count == 0 || slot < 1 || slot > MaxRivals)
            {
                return;
            }

            shapes[slot] = all[random.Next(all.Count)];
            Color colour;
            do
            {
                colour = ShipPalette.Colours[random.Next(ShipPalette.Colours.Length)];
            }
            while (Array.IndexOf(colours, colour) >= 0); // not yours and not another rival's
            colours[slot] = colour;
        }

        static float Reach(Vector2[] shape)
        {
            float max = 1f;
            foreach (Vector2 p in shape)
            {
                max = Math.Max(max, p.Length());
            }

            return max;
        }

        /// <summary>Draws you and <paramref name="rivals"/> computer pilots, centred on the panel.</summary>
        public void Draw(FrameCanvas c, Rectangle area, float time, int rivals)
        {
            rivals = Math.Clamp(rivals, 0, MaxRivals);
            Vector2 centre = new Vector2(area.Center.X, area.Y + 150);
            float bob = MathF.Sin(time) * 0.12f;

            if (rivals == 0)
            {
                c.Polygon(shapes[0], centre, bob, 48f / Reach(shapes[0]), colours[0], 2.4f);
                return;
            }

            // every ship is drawn about the same size, whatever its own size in the game; more rivals, smaller ships
            float reach = rivals == 1 ? 42f : rivals == 2 ? 34f : 28f;
            float gap = rivals == 2 ? 92f : 66f; // between neighbours
            c.Polygon(shapes[0], centre + new Vector2(-95, 0), MathF.PI / 2 - 0.2f + bob, reach / Reach(shapes[0]), colours[0], 2.4f);
            for (int i = 0; i < rivals; i++)
            {
                float y = (i - (rivals - 1) / 2f) * gap;
                float lean = (i % 2 == 0 ? 1f : -1f) * 0.1f;
                c.Polygon(shapes[i + 1], centre + new Vector2(95, y), -MathF.PI / 2 + lean + bob * (i % 2 == 0 ? 1f : -1f), reach / Reach(shapes[i + 1]), colours[i + 1], 2.4f);
            }
        }
    }
}
