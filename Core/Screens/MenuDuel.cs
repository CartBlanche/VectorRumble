using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>
    /// The two ships that face each other in the main menu's Play panel: two different ships that come with the game,
    /// picked at random once when the game starts, red against blue, with red on the left half of the time.
    /// </summary>
    static class MenuDuel
    {
        static readonly Color Red = new Color(255, 61, 74);
        static readonly Color Blue = new Color(70, 120, 255);

        static Vector2[] left, right;
        static Color leftColour, rightColour;
        static bool chosen;

        static List<Vector2[]> loaded;
        static readonly List<string> loadedNames = new List<string>();

        /// <summary>The names of the ships <see cref="Shapes"/> returns, in the same order.</summary>
        internal static List<string> Names()
        {
            Shapes();
            return loadedNames;
        }

        /// <summary>The outlines of the ships that come with the game (empty if they cannot be read).</summary>
        internal static List<Vector2[]> Shapes()
        {
            if (loaded != null)
            {
                return loaded;
            }

            var shapes = new List<Vector2[]>();
            try
            {
                foreach (string file in Helper.GetFilesFromContent("Ships", "ships.txt"))
                {
                    using var stream = TitleContainer.OpenStream(file);
                    Ship ship = Ship.Load(XDocument.Load(stream).XPathSelectElement("/XnaContent/Asset"));
                    shapes.Add(ship.Shape.Points);
                    loadedNames.Add(ship.Name);
                }
            }
            catch (Exception)
            {
                // no ships to show: the panel falls back to its plain picture
            }

            return loaded = shapes;
        }

        static void Choose()
        {
            chosen = true;
            var shapes = Shapes();

            if (shapes.Count == 0)
            {
                return;
            }

            var random = Random.Shared;
            int a = random.Next(shapes.Count);
            int b = shapes.Count > 1 ? (a + 1 + random.Next(shapes.Count - 1)) % shapes.Count : a;
            bool redOnLeft = random.Next(2) == 0;
            left = shapes[a];
            right = shapes[b];
            leftColour = redOnLeft ? Red : Blue;
            rightColour = redOnLeft ? Blue : Red;
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

        /// <summary>Draws the face-off, centred in the panel, with a bolt between the ships.</summary>
        public static void Draw(FrameCanvas c, Rectangle area, float time)
        {
            if (!chosen)
            {
                Choose();
            }

            if (left == null)
            {
                c.Icon(FrameIcon.Duel, new Vector2(area.Center.X, area.Y + 160), 92f, c.Style.Hot, time);
                return;
            }

            // every ship is drawn about the same size, whatever its own size in the game
            const float reach = 42f;
            float leftScale = reach / Reach(left), rightScale = reach / Reach(right);
            float bob = MathF.Sin(time) * 0.12f;
            Vector2 centre = new Vector2(area.Center.X, area.Y + 160);
            Vector2 l = centre + new Vector2(-95, 22), r = centre + new Vector2(95, -22);
            c.Polygon(left, l, MathF.PI / 2 - 0.2f + bob, leftScale, leftColour, 2.4f);
            c.Polygon(right, r, -MathF.PI / 2 - 0.2f + bob, rightScale, rightColour, 2.4f);

            // a bolt that travels between them
            float k = time * 1.6f % 1f;
            Vector2 bolt = Vector2.Lerp(l + new Vector2(45, -10), r + new Vector2(-45, 10), k);
            c.Neon(bolt - new Vector2(14, -1), bolt + new Vector2(6, -1), c.Style.Spark, 2.4f);
        }
    }
}
