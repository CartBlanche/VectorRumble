using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>The parts of the sky the player can switch on and off (Settings, Sky).</summary>
    [Flags]
    enum SkyLayers
    {
        None = 0,
        Stars = 1,
        Constellations = 2,
        Names = 4,
        Planets = 8,
        Nebula = 16,
        Glow = 32,
        Comet = 64,

        /// <summary>What a new player sees: everything except the name labels.</summary>
        Default = Stars | Constellations | Planets | Nebula | Glow | Comet,
    }

    /// <summary>
    /// A small deterministic random number generator (xorshift). The sky is drawn from fixed seeds, so every
    /// machine makes the same picture; System.Random does not promise the same numbers on every platform.
    /// </summary>
    sealed class SkyRandom
    {
        uint state;

        public SkyRandom(int seed)
        {
            state = (uint)seed * 2654435761u + 0x9E3779B9u;
            if (state == 0)
            {
                state = 1;
            }

            Next();
            Next();
        }

        uint Next()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        /// <summary>A number from 0 up to, but not including, 1.</summary>
        public float NextFloat() => (Next() >> 8) / 16777216f;

        /// <summary>A whole number from min up to, but not including, max.</summary>
        public int Next(int min, int max) => min + (int)(NextFloat() * (max - min));

        /// <summary>A bell shaped number around 0, about -2.4 to 2.4.</summary>
        public float Gauss() => (NextFloat() + NextFloat() + NextFloat() + NextFloat() - 2f) * 1.2f;
    }

    /// <summary>One star of a constellation: right ascension in hours, declination in degrees, magnitude (smaller is brighter).</summary>
    readonly struct SkyStar
    {
        public readonly string Name;
        public readonly float Ra, Dec, Magnitude;

        public SkyStar(string name, float ra, float dec, float magnitude)
        {
            Name = name;
            Ra = ra;
            Dec = dec;
            Magnitude = magnitude;
        }
    }

    /// <summary>A constellation as line art: real star positions (bright stars only, rounded) and the lines between them.</summary>
    sealed class Constellation
    {
        public readonly string Name;
        public readonly SkyStar[] Stars;
        public readonly (int, int)[] Lines;

        public Constellation(string name, SkyStar[] stars, params (string, string)[] lines)
        {
            Name = name;
            Stars = stars;
            Lines = new (int, int)[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                Lines[i] = (IndexOf(lines[i].Item1), IndexOf(lines[i].Item2));
            }
        }

        int IndexOf(string star)
        {
            for (int i = 0; i < Stars.Length; i++)
            {
                if (Stars[i].Name == star)
                {
                    return i;
                }
            }

            throw new ArgumentException("No star " + star + " in " + Name);
        }

        /// <summary>
        /// The stars as pixel offsets from the middle of the constellation (east is to the left, as on a sky chart),
        /// <paramref name="scale"/> pixels to the degree.
        /// </summary>
        public Vector2[] Project(float scale)
        {
            float ra0 = 0, dec0 = 0;
            foreach (SkyStar s in Stars)
            {
                ra0 += s.Ra;
                dec0 += s.Dec;
            }

            ra0 /= Stars.Length;
            dec0 /= Stars.Length;
            float squash = (float)Math.Cos(dec0 * Math.PI / 180.0);
            var points = new Vector2[Stars.Length];
            for (int i = 0; i < Stars.Length; i++)
            {
                points[i] = new Vector2(-(Stars[i].Ra - ra0) * 15f * squash * scale, -(Stars[i].Dec - dec0) * scale);
            }

            return points;
        }
    }

    /// <summary>The constellations the skies can use.</summary>
    static class Constellations
    {
        public static readonly Constellation Orion = new Constellation("Orion", new[]
        {
            new SkyStar("Betelgeuse", 5.919f, 7.41f, 0.5f), new SkyStar("Rigel", 5.242f, -8.20f, 0.1f),
            new SkyStar("Bellatrix", 5.419f, 6.35f, 1.6f), new SkyStar("Saiph", 5.796f, -9.67f, 2.1f),
            new SkyStar("Alnitak", 5.679f, -1.94f, 1.7f), new SkyStar("Alnilam", 5.603f, -1.20f, 1.7f),
            new SkyStar("Mintaka", 5.533f, -0.30f, 2.2f), new SkyStar("Meissa", 5.585f, 9.93f, 3.5f),
        },
            ("Meissa", "Betelgeuse"), ("Meissa", "Bellatrix"), ("Betelgeuse", "Alnitak"), ("Bellatrix", "Mintaka"),
            ("Mintaka", "Alnilam"), ("Alnilam", "Alnitak"), ("Alnitak", "Saiph"), ("Mintaka", "Rigel"));

        public static readonly Constellation Plough = new Constellation("Big Dipper", new[]
        {
            new SkyStar("Dubhe", 11.062f, 61.75f, 1.8f), new SkyStar("Merak", 11.031f, 56.38f, 2.4f),
            new SkyStar("Phecda", 11.897f, 53.69f, 2.4f), new SkyStar("Megrez", 12.257f, 57.03f, 3.3f),
            new SkyStar("Alioth", 12.900f, 55.96f, 1.8f), new SkyStar("Mizar", 13.399f, 54.93f, 2.2f),
            new SkyStar("Alkaid", 13.792f, 49.31f, 1.9f),
        },
            ("Dubhe", "Merak"), ("Merak", "Phecda"), ("Phecda", "Megrez"), ("Megrez", "Dubhe"),
            ("Megrez", "Alioth"), ("Alioth", "Mizar"), ("Mizar", "Alkaid"));

        public static readonly Constellation Cassiopeia = new Constellation("Cassiopeia", new[]
        {
            new SkyStar("Caph", 0.153f, 59.15f, 2.3f), new SkyStar("Schedar", 0.675f, 56.54f, 2.2f),
            new SkyStar("Gamma Cas", 0.945f, 60.72f, 2.5f), new SkyStar("Ruchbah", 1.430f, 60.24f, 2.7f),
            new SkyStar("Segin", 1.907f, 63.67f, 3.4f),
        },
            ("Caph", "Schedar"), ("Schedar", "Gamma Cas"), ("Gamma Cas", "Ruchbah"), ("Ruchbah", "Segin"));

        public static readonly Constellation SouthernCross = new Constellation("Southern Cross", new[]
        {
            new SkyStar("Acrux", 12.443f, -63.10f, 0.8f), new SkyStar("Mimosa", 12.795f, -59.69f, 1.3f),
            new SkyStar("Gacrux", 12.519f, -57.11f, 1.6f), new SkyStar("Delta Crucis", 12.252f, -58.75f, 2.8f),
        },
            ("Acrux", "Gacrux"), ("Mimosa", "Delta Crucis"));

        public static readonly Constellation Cygnus = new Constellation("Cygnus", new[]
        {
            new SkyStar("Deneb", 20.690f, 45.28f, 1.3f), new SkyStar("Sadr", 20.370f, 40.26f, 2.2f),
            new SkyStar("Albireo", 19.512f, 27.96f, 3.1f), new SkyStar("Gienah", 20.770f, 33.97f, 2.5f),
            new SkyStar("Fawaris", 19.749f, 45.13f, 2.9f),
        },
            ("Deneb", "Sadr"), ("Sadr", "Albireo"), ("Gienah", "Sadr"), ("Sadr", "Fawaris"));

        public static readonly Constellation Scorpius = new Constellation("Scorpius", new[]
        {
            new SkyStar("Graffias", 16.090f, -19.81f, 2.6f), new SkyStar("Dschubba", 16.005f, -22.62f, 2.3f),
            new SkyStar("Pi Sco", 15.981f, -26.11f, 2.9f), new SkyStar("Sigma Sco", 16.353f, -25.59f, 2.9f),
            new SkyStar("Antares", 16.490f, -26.43f, 1.1f), new SkyStar("Tau Sco", 16.598f, -28.22f, 2.8f),
            new SkyStar("Epsilon Sco", 16.836f, -34.29f, 2.3f), new SkyStar("Mu Sco", 16.864f, -38.05f, 3.0f),
            new SkyStar("Zeta Sco", 16.910f, -42.36f, 3.6f), new SkyStar("Lesath", 17.508f, -37.30f, 2.7f),
            new SkyStar("Shaula", 17.560f, -37.10f, 1.6f),
        },
            ("Graffias", "Dschubba"), ("Dschubba", "Pi Sco"), ("Dschubba", "Sigma Sco"), ("Sigma Sco", "Antares"),
            ("Antares", "Tau Sco"), ("Tau Sco", "Epsilon Sco"), ("Epsilon Sco", "Mu Sco"), ("Mu Sco", "Zeta Sco"),
            ("Zeta Sco", "Lesath"), ("Lesath", "Shaula"));

        public static readonly Constellation Lyra = new Constellation("Lyra", new[]
        {
            new SkyStar("Vega", 18.616f, 38.78f, 0.0f), new SkyStar("Zeta Lyr", 18.746f, 37.60f, 4.1f),
            new SkyStar("Delta Lyr", 18.908f, 36.90f, 4.3f), new SkyStar("Sulafat", 18.982f, 32.69f, 3.2f),
            new SkyStar("Sheliak", 18.835f, 33.36f, 3.5f),
        },
            ("Vega", "Zeta Lyr"), ("Zeta Lyr", "Sheliak"), ("Sheliak", "Sulafat"), ("Sulafat", "Delta Lyr"),
            ("Delta Lyr", "Zeta Lyr"));
    }

    /// <summary>A constellation set on the screen: where, how big, and at what depth (how much it moves with the ships).</summary>
    sealed class SkyConstellation
    {
        public readonly Constellation Shape;
        public readonly float X, Y, Depth, Scale;

        public SkyConstellation(Constellation shape, float x, float y, float depth, float scale)
        {
            Shape = shape;
            X = x;
            Y = y;
            Depth = depth;
            Scale = scale;
        }
    }

    /// <summary>A cloud of the nebula: a bell shaped swarm of faint dots, and a soft glow of the same colour under it.</summary>
    readonly struct SkyCloud
    {
        public readonly float X, Y, Width, Height, Hue;

        public SkyCloud(float x, float y, float width, float height, float hue)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Hue = hue;
        }
    }

    /// <summary>A planet: a dot (Saturn has a thin ring) that drifts slowly.</summary>
    sealed class SkyPlanet
    {
        public readonly string Name;
        public readonly float X, Y, Radius, Depth;
        public readonly Color Colour;
        public readonly bool Ring;

        public SkyPlanet(string name, float x, float y, Color colour, float radius, float depth, bool ring = false)
        {
            Name = name;
            X = x;
            Y = y;
            Colour = colour;
            Radius = radius;
            Depth = depth;
            Ring = ring;
        }
    }

    /// <summary>One hand made sky: everything is fixed, so the same entry looks the same on every machine.</summary>
    sealed class SkyEntry
    {
        public int Seed;
        public SkyCloud[] Clouds;
        public SkyConstellation[] Constellations;
        public SkyPlanet[] Planets;

        /// <summary>The comet's path across the screen, from start to end.</summary>
        public Vector2 CometFrom, CometTo;
    }

    /// <summary>
    /// The hand made skies. A sky is chosen by its number, which fits in one byte, so the host can send it in the match
    /// setup (a later step). Every nebula hue is violet (285), magenta (320) or teal (190), so it is not taken for the
    /// blue walls.
    /// </summary>
    static class SkyPool
    {
        const float Violet = 285f, Magenta = 320f, Teal = 190f;

        static readonly SkyPlanet Venus = new SkyPlanet("Venus", 230, 120, new Color(255, 243, 196), 2.6f, 0.12f);
        static readonly SkyPlanet Mars = new SkyPlanet("Mars", 860, 470, new Color(255, 154, 106), 2.2f, 0.12f);
        static readonly SkyPlanet Jupiter = new SkyPlanet("Jupiter", 560, 610, new Color(255, 226, 176), 3.4f, 0.25f);
        static readonly SkyPlanet Saturn = new SkyPlanet("Saturn", 130, 560, new Color(242, 220, 154), 3f, 0.25f, ring: true);

        static SkyPlanet At(SkyPlanet p, float x, float y) => new SkyPlanet(p.Name, x, y, p.Colour, p.Radius, p.Depth, p.Ring);

        static readonly SkyEntry[] entries =
        {
            new SkyEntry
            {
                Seed = 11,
                Clouds = new[] { new SkyCloud(430, 500, 170, 90, Violet), new SkyCloud(560, 540, 130, 80, Magenta), new SkyCloud(330, 560, 110, 70, Teal) },
                Constellations = new[]
                {
                    new SkyConstellation(Constellations.Orion, 330, 230, 0.04f, 9f),
                    new SkyConstellation(Constellations.Plough, 1000, 150, 0.12f, 6f),
                    new SkyConstellation(Constellations.Cassiopeia, 680, 95, 0.25f, 12f),
                    new SkyConstellation(Constellations.SouthernCross, 1090, 560, 0.12f, 14f),
                },
                Planets = new[] { Venus, Mars, Jupiter, Saturn },
                CometFrom = new Vector2(-100, 80), CometTo = new Vector2(1400, 340),
            },
            new SkyEntry
            {
                Seed = 23,
                Clouds = new[] { new SkyCloud(900, 200, 190, 90, Magenta), new SkyCloud(1010, 260, 120, 70, Violet), new SkyCloud(250, 600, 140, 80, Teal) },
                Constellations = new[]
                {
                    new SkyConstellation(Constellations.Scorpius, 380, 400, 0.12f, 5.5f),
                    new SkyConstellation(Constellations.Lyra, 1010, 560, 0.25f, 22f),
                    new SkyConstellation(Constellations.Cygnus, 640, 130, 0.04f, 8f),
                },
                Planets = new[] { At(Mars, 200, 160), At(Jupiter, 1180, 420), At(Saturn, 700, 650) },
                CometFrom = new Vector2(1400, 120), CometTo = new Vector2(-100, 420),
            },
            new SkyEntry
            {
                Seed = 37,
                Clouds = new[] { new SkyCloud(250, 180, 180, 100, Teal), new SkyCloud(380, 230, 140, 80, Violet), new SkyCloud(1000, 600, 160, 80, Magenta) },
                Constellations = new[]
                {
                    new SkyConstellation(Constellations.Cygnus, 900, 200, 0.12f, 9f),
                    new SkyConstellation(Constellations.Cassiopeia, 300, 560, 0.04f, 10f),
                    new SkyConstellation(Constellations.Orion, 1060, 520, 0.25f, 8f),
                },
                Planets = new[] { At(Venus, 640, 80), At(Saturn, 120, 400) },
                CometFrom = new Vector2(-100, 600), CometTo = new Vector2(1400, 90),
            },
            new SkyEntry
            {
                Seed = 51,
                Clouds = new[] { new SkyCloud(640, 160, 200, 70, Violet), new SkyCloud(900, 200, 130, 60, Magenta), new SkyCloud(220, 520, 120, 90, Teal), new SkyCloud(420, 600, 110, 60, Magenta) },
                Constellations = new[]
                {
                    new SkyConstellation(Constellations.SouthernCross, 200, 200, 0.25f, 16f),
                    new SkyConstellation(Constellations.Scorpius, 960, 480, 0.04f, 5f),
                    new SkyConstellation(Constellations.Lyra, 620, 540, 0.12f, 22f),
                },
                Planets = new[] { At(Jupiter, 1150, 120), At(Mars, 400, 380), At(Venus, 820, 640) },
                CometFrom = new Vector2(300, -80), CometTo = new Vector2(1100, 800),
            },
            new SkyEntry
            {
                Seed = 67,
                Clouds = new[] { new SkyCloud(1050, 560, 190, 90, Magenta), new SkyCloud(900, 520, 130, 70, Teal), new SkyCloud(260, 220, 120, 70, Violet) },
                Constellations = new[]
                {
                    new SkyConstellation(Constellations.Plough, 520, 170, 0.25f, 7f),
                    new SkyConstellation(Constellations.Orion, 190, 520, 0.12f, 8f),
                    new SkyConstellation(Constellations.Lyra, 1010, 130, 0.04f, 20f),
                    new SkyConstellation(Constellations.Cassiopeia, 760, 360, 0.04f, 9f),
                },
                Planets = new[] { At(Saturn, 1160, 330), At(Venus, 90, 120), At(Mars, 600, 660) },
                CometFrom = new Vector2(1400, 700), CometTo = new Vector2(-100, 200),
            },
        };

        /// <summary>How many skies there are (never more than 256, so a sky's number is one byte).</summary>
        public static int Count => entries.Length;

        /// <summary>The sky with this number; any byte is valid (a number past the end wraps round).</summary>
        public static SkyEntry Get(byte index) => entries[index % entries.Length];

        /// <summary>A random sky's number.</summary>
        public static byte Pick(Random random) => (byte)random.Next(entries.Length);
    }
}
