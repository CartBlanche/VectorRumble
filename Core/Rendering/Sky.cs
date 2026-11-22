using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VectorRumble
{
    /// <summary>The small textures the sky is drawn with, made in code so every head builds the same way (no content, no effect).</summary>
    sealed class SkyAssets : IDisposable
    {
        /// <summary>A white pixel.</summary>
        public readonly Texture2D Blank;

        /// <summary>A small round dot with a soft edge, for stars and planets.</summary>
        public readonly Texture2D Disc;

        /// <summary>A round soft glow, bright in the middle and fading to nothing at the edge.</summary>
        public readonly Texture2D Glow;

        public SkyAssets(GraphicsDevice device)
        {
            Blank = new Texture2D(device, 1, 1);
            Blank.SetData(new[] { Color.White });
            Disc = Radial(device, 16, (d, r) => MathHelper.Clamp(r - d + 0.5f, 0f, 1f));
            Glow = Radial(device, 128, (d, r) => Math.Max(0f, 1f - d / r));
        }

        // a square texture whose alpha depends on the distance from the middle (premultiplied, so it also adds up well)
        static Texture2D Radial(GraphicsDevice device, int size, Func<float, float, float> alphaAt)
        {
            var texture = new Texture2D(device, size, size);
            var data = new Color[size * size];
            float middle = (size - 1) / 2f, radius = size / 2f - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - middle, dy = y - middle;
                    data[y * size + x] = Color.White * alphaAt((float)Math.Sqrt(dx * dx + dy * dy), radius);
                }
            }

            texture.SetData(data);
            return texture;
        }

        public void Dispose()
        {
            Blank.Dispose();
            Disc.Dispose();
            Glow.Dispose();
        }
    }

    /// <summary>
    /// The sky behind the arena, from one entry of <see cref="SkyPool"/>: nebula (faint dots and a soft glow),
    /// constellations as line art, planets and a comet. Everything is made from the entry's seed, so every machine
    /// shows the same sky. Each layer sits at its own depth and moves a little with the ships' mean position.
    /// </summary>
    sealed class Sky
    {
        /// <summary>How far the nebula moves with the ships (a fraction of the camera's movement).</summary>
        const float NebulaDepth = 0.04f;

        /// <summary>How strong the nebula's glow is at its middle (0 to 1; the colour is added to the black behind it).</summary>
        const float GlowStrength = 0.35f;

        /// <summary>How bright the constellation stars are (0 to 1); the lines between them are dimmer (alpha 64 of 255).</summary>
        const float StarBrightness = 0.6f;

        /// <summary>How bright the comet's head and the start of its tail are (0 to 1).</summary>
        const float CometBrightness = 0.5f;

        /// <summary>The comet crosses once every <see cref="CometPeriod"/> seconds, taking <see cref="CometCrossing"/> of them.</summary>
        const float CometPeriod = 50f, CometCrossing = 14f, CometFirst = 8f, CometTail = 130f;

        readonly SkyEntry entry;
        readonly Vector2[][] points;
        readonly float[] dotX, dotY;
        readonly byte[] dotSize;
        readonly Color[] dotColour;
        float time;

        public Sky(SkyEntry entry)
        {
            this.entry = entry ?? throw new ArgumentNullException(nameof(entry));

            points = new Vector2[entry.Constellations.Length][];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = entry.Constellations[i].Shape.Project(entry.Constellations[i].Scale);
            }

            // the nebula's dots: a bell shaped swarm round each cloud's middle, brightest in the middle
            var random = new SkyRandom(entry.Seed);
            const int DotsPerCloud = 650;
            int count = DotsPerCloud * entry.Clouds.Length;
            dotX = new float[count];
            dotY = new float[count];
            dotSize = new byte[count];
            dotColour = new Color[count];
            int n = 0;
            foreach (SkyCloud cloud in entry.Clouds)
            {
                for (int i = 0; i < DotsPerCloud; i++, n++)
                {
                    float x = cloud.X + random.Gauss() * cloud.Width;
                    float y = cloud.Y + random.Gauss() * cloud.Height;
                    float ex = (x - cloud.X) / cloud.Width, ey = (y - cloud.Y) / cloud.Height;
                    float edge = (float)Math.Exp(-(ex * ex + ey * ey) / 2.0);
                    dotSize[n] = (byte)(random.NextFloat() < 0.12f ? 2 : 1);
                    float alpha = 0.12f + 0.5f * random.NextFloat() * edge;
                    float hue = cloud.Hue + (random.NextFloat() - 0.5f) * 40f;
                    dotX[n] = x;
                    dotY[n] = y;
                    dotColour[n] = Hsl(hue, 0.7f, 0.65f) * alpha;
                }
            }
        }

        /// <summary>The number of nebula dots (for the tests).</summary>
        public int DotCount => dotX.Length;

        public SkyEntry Entry => entry;

        public void Update(float elapsedTime) => time += elapsedTime;

        /// <summary>
        /// Draws the sky. <paramref name="offset"/> is how far the ships' mean position has moved since the match began.
        /// <paramref name="font"/> is only used for the names, and may be null.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, LineBatch lineBatch, SkyAssets assets, SpriteFont font, SkyLayers layers, Vector2 offset)
        {
            Vector2 nebula = offset * NebulaDepth;

            // 1. the glow under the dots: soft discs added to what is behind
            if ((layers & SkyLayers.Glow) != 0)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive);
                foreach (SkyCloud cloud in entry.Clouds)
                {
                    float radius = cloud.Width * 1.5f;
                    var origin = new Vector2(assets.Glow.Width / 2f);
                    spriteBatch.Draw(assets.Glow, new Vector2(cloud.X, cloud.Y) + nebula, null, Hsl(cloud.Hue, 0.7f, 0.45f) * GlowStrength,
                        0f, origin, radius * 2f / assets.Glow.Width, SpriteEffects.None, 0f);
                }

                spriteBatch.End();
            }

            // 2. the nebula's dots
            if ((layers & SkyLayers.Nebula) != 0)
            {
                spriteBatch.Begin();
                for (int i = 0; i < dotX.Length; i++)
                {
                    spriteBatch.Draw(assets.Blank, new Rectangle((int)(dotX[i] + nebula.X), (int)(dotY[i] + nebula.Y), dotSize[i], dotSize[i]), dotColour[i]);
                }

                spriteBatch.End();
            }

            // 3. lines: the constellations, Saturn's ring and the comet's tail
            bool showConstellations = (layers & SkyLayers.Constellations) != 0;
            bool showPlanets = (layers & SkyLayers.Planets) != 0;
            bool comet = (layers & SkyLayers.Comet) != 0 && CometProgress(out _);
            if (showConstellations || showPlanets || comet)
            {
                lineBatch.Begin();
                if (showConstellations)
                {
                    var lineColour = new Color(170, 200, 255, 64);
                    for (int c = 0; c < points.Length; c++)
                    {
                        SkyConstellation sc = entry.Constellations[c];
                        Vector2 at = new Vector2(sc.X, sc.Y) + offset * sc.Depth;
                        foreach ((int a, int b) in sc.Shape.Lines)
                        {
                            lineBatch.DrawLineList(at + points[c][a], at + points[c][b], lineColour);
                        }
                    }
                }

                if (showPlanets)
                {
                    foreach (SkyPlanet planet in entry.Planets)
                    {
                        if (planet.Ring)
                        {
                            DrawRing(lineBatch, PlanetPosition(planet, offset), planet.Radius * 2.4f, planet.Radius * 0.8f, -0.4f, new Color(242, 220, 154, 150));
                        }
                    }
                }

                if (comet)
                {
                    CometProgress(out float u);
                    Vector2 head = Vector2.Lerp(entry.CometFrom, entry.CometTo, u);
                    Vector2 direction = Vector2.Normalize(entry.CometTo - entry.CometFrom);
                    lineBatch.DrawLineList(head, head - direction * CometTail, new Color(255, 255, 255, (int)(255 * CometBrightness * 0.8f)), new Color(255, 255, 255, 0));
                }

                lineBatch.End();
            }

            // 4. the points: constellation stars, planets, the comet's head, and the names
            bool names = (layers & SkyLayers.Names) != 0 && font != null;
            if (showConstellations || showPlanets || comet)
            {
                spriteBatch.Begin();
                var discOrigin = new Vector2(assets.Disc.Width / 2f);
                if (showConstellations)
                {
                    for (int c = 0; c < points.Length; c++)
                    {
                        SkyConstellation sc = entry.Constellations[c];
                        Vector2 at = new Vector2(sc.X, sc.Y) + offset * sc.Depth;
                        for (int i = 0; i < points[c].Length; i++)
                        {
                            SkyStar star = sc.Shape.Stars[i];
                            float radius = Math.Max(1f, 2.6f - star.Magnitude * 0.5f);
                            float twinkle = 0.85f + 0.15f * (float)Math.Sin(time * 1.3f + i * 2.1f);
                            spriteBatch.Draw(assets.Disc, at + points[c][i], null, new Color(225, 236, 255) * (twinkle * StarBrightness), 0f, discOrigin,
                                radius * 2f / assets.Disc.Width, SpriteEffects.None, 0f);
                            if (names && star.Magnitude < 2.2f)
                            {
                                Label(spriteBatch, font, star.Name, at + points[c][i] + new Vector2(7, -5), new Color(150, 175, 220) * 0.55f);
                            }
                        }

                        if (names)
                        {
                            Label(spriteBatch, font, sc.Shape.Name, at + new Vector2(-40, 90), new Color(150, 175, 220) * 0.4f);
                        }
                    }
                }

                if (showPlanets)
                {
                    foreach (SkyPlanet planet in entry.Planets)
                    {
                        Vector2 p = PlanetPosition(planet, offset);
                        spriteBatch.Draw(assets.Disc, p, null, planet.Colour, 0f, discOrigin, planet.Radius * 2f / assets.Disc.Width, SpriteEffects.None, 0f);
                        if (names)
                        {
                            Label(spriteBatch, font, planet.Name, p + new Vector2(8, -6), new Color(200, 190, 150) * 0.55f);
                        }
                    }
                }

                if (comet)
                {
                    CometProgress(out float u);
                    Vector2 head = Vector2.Lerp(entry.CometFrom, entry.CometTo, u);
                    spriteBatch.Draw(assets.Disc, head, null, Color.White * CometBrightness, 0f, discOrigin, 4.8f / assets.Disc.Width, SpriteEffects.None, 0f);
                }

                spriteBatch.End();
            }
        }

        // a planet drifts to and fro by a few pixels, at under a pixel a second
        Vector2 PlanetPosition(SkyPlanet planet, Vector2 offset) =>
            new Vector2(planet.X + 12f * (float)Math.Sin(time * 0.05f + planet.X), planet.Y) + offset * planet.Depth;

        /// <summary>Whether the comet is crossing now, and how far along its path (0 to 1).</summary>
        bool CometProgress(out float u)
        {
            float into = (time + CometPeriod - CometFirst) % CometPeriod;
            u = into / CometCrossing;
            return u < 1f;
        }

        /// <summary>How much brighter than usual the name labels are drawn (1 is normal; a DevShots recording sets it higher).</summary>
        internal static float LabelBoost = 1f;

        static void Label(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color colour) =>
            spriteBatch.DrawString(font, text, position, colour * LabelBoost, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        static void DrawRing(LineBatch lineBatch, Vector2 centre, float radiusX, float radiusY, float tilt, Color colour)
        {
            const int Steps = 16;
            float cos = (float)Math.Cos(tilt), sin = (float)Math.Sin(tilt);
            Vector2 previous = default;
            for (int i = 0; i <= Steps; i++)
            {
                float angle = i * MathHelper.TwoPi / Steps;
                float x = radiusX * (float)Math.Cos(angle), y = radiusY * (float)Math.Sin(angle);
                Vector2 p = centre + new Vector2(x * cos - y * sin, x * sin + y * cos);
                if (i > 0)
                {
                    lineBatch.DrawLineList(previous, p, colour);
                }

                previous = p;
            }
        }

        /// <summary>A colour from hue (degrees), saturation and lightness (0 to 1), opaque.</summary>
        internal static Color Hsl(float hue, float saturation, float lightness)
        {
            hue = ((hue % 360f) + 360f) % 360f;
            float c = (1f - Math.Abs(2f * lightness - 1f)) * saturation;
            float h = hue / 60f;
            float x = c * (1f - Math.Abs(h % 2f - 1f));
            float r = 0, g = 0, b = 0;
            if (h < 1) { r = c; g = x; }
            else if (h < 2) { r = x; g = c; }
            else if (h < 3) { g = c; b = x; }
            else if (h < 4) { g = x; b = c; }
            else if (h < 5) { r = x; b = c; }
            else { r = c; b = x; }
            float m = lightness - c / 2f;
            return new Color(r + m, g + m, b + m);
        }
    }
}
