using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace VectorEngine.Splash;

/// <summary>One line of a splash frame: where it is, what colour (0-255 per channel), how thick, how visible.</summary>
public readonly record struct SplashLine(Vector2 A, Vector2 B, Vector3 Color, float Width, float Alpha);

/// <summary>
/// The splash animation, as a function of time: loose lines of mixed colours drift across the canvas, then swing into
/// place as the letters of "VECTOR ENGINE" and the V+E mark, each flashing white as it locks. It has no graphics in it,
/// so it can be tested. Positions are on the 1280 x 720 design canvas of <see cref="VectorEngineLogo"/>.
/// </summary>
public sealed class SplashAnimation
{
    /// <summary>Seconds from the first line appearing until the last has locked and its flash has faded.</summary>
    public const float Seconds = 3.4f;

    static readonly Vector3 White = new(255f, 255f, 255f);

    sealed class Mover
    {
        public Vector2 A, B, Mid, Start, Drift;
        public Vector3 Colour, StartColour;
        public float Width, StartTime, Duration, Angle0, Spin, Length0, Length, Turn, FadeIn;
    }

    readonly Mover[] movers;

    public SplashAnimation(SplashOptions options = null)
    {
        options ??= new SplashOptions();
        var random = new SplashRandom(options.Seed);
        Vector3 text = ToVector(options.TextColor), mark = ToVector(options.MarkColor);
        Vector3[] palette = Array.ConvertAll(options.DriftColors, ToVector);

        var list = new List<Mover>();
        foreach (var s in VectorEngineLogo.Wordmark)
            list.Add(new Mover
            {
                A = s.A, B = s.B, Colour = text, Width = 5f,
                StartTime = .6f + s.Letter / 12f * .5f + random.Next() * .5f, Duration = .8f + random.Next() * .3f,
            });
        foreach (var s in VectorEngineLogo.Mark)
            list.Add(new Mover { A = s.A, B = s.B, Colour = mark, Width = 6.5f, StartTime = 1.7f + random.Next() * .4f, Duration = .8f });

        foreach (var m in list)
        {
            m.Start = new Vector2(random.Next() * VectorEngineLogo.Width, random.Next() * VectorEngineLogo.Height);
            m.Drift = new Vector2((random.Next() - .5f) * 90f, (random.Next() - .5f) * 90f);
            m.Angle0 = random.Next() * MathF.PI;
            m.Spin = (random.Next() - .5f) * 1.4f;
            m.Length0 = 30f + random.Next() * 160f;
            m.StartColour = palette[(int)MathF.Floor(random.Next() * palette.Length)];
            m.FadeIn = random.Next() * .4f;
            m.Mid = (m.A + m.B) / 2f;
            m.Length = Vector2.Distance(m.A, m.B);
            float target = MathF.Atan2(m.B.Y - m.A.Y, m.B.X - m.A.X);
            float d = target - (m.Angle0 + m.Spin * m.StartTime);
            // the shortest turn to line up with the target, either way round (a line looks the same flipped)
            m.Turn = ((d + MathF.PI / 2f) % MathF.PI + MathF.PI) % MathF.PI - MathF.PI / 2f;
        }
        movers = list.ToArray();
    }

    /// <summary>How many lines every frame has.</summary>
    public int LineCount => movers.Length;

    /// <summary>Fills <paramref name="lines"/> with the frame at <paramref name="t"/> seconds (cleared first).</summary>
    public void Build(float t, List<SplashLine> lines)
    {
        lines.Clear();
        foreach (var m in movers)
        {
            float u = EaseInOut(Clamp01((t - m.StartTime) / m.Duration));
            float tt = MathF.Min(t, m.StartTime);
            Vector2 centre = Vector2.Lerp(m.Start + m.Drift * tt, m.Mid, u);
            float angle = m.Angle0 + m.Spin * tt + m.Turn * u;
            float half = MathHelper.Lerp(m.Length0, m.Length, u) / 2f;
            Vector2 d = new(MathF.Cos(angle) * half, MathF.Sin(angle) * half);

            // a white flash as it locks, fading over a third of a second
            float flash = t > m.StartTime + m.Duration ? Clamp01(1f - (t - m.StartTime - m.Duration) / .35f) : 0f;
            Vector3 colour = Vector3.Lerp(Vector3.Lerp(m.StartColour, m.Colour, u), White, flash * .8f);
            lines.Add(new SplashLine(centre - d, centre + d, colour,
                MathHelper.Lerp(2.2f, m.Width, u) + flash * 2f,
                Clamp01((t - m.FadeIn) / .3f) * MathHelper.Lerp(.5f, 1f, u)));
        }
    }

    static Vector3 ToVector(Color c) => new(c.R, c.G, c.B);
    static float Clamp01(float x) => MathF.Min(1f, MathF.Max(0f, x));
    static float EaseInOut(float t) => t < .5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;
}

/// <summary>The small seeded random generator the splash uses (mulberry32), so a seed always gives the same lines.</summary>
sealed class SplashRandom
{
    uint state;

    public SplashRandom(int seed) => state = (uint)seed;

    /// <summary>The next number, from 0 up to but not including 1.</summary>
    public float Next()
    {
        unchecked
        {
            state += 0x6D2B79F5u;
            uint t = (state ^ (state >> 15)) * (1u | state);
            t = (t + (t ^ (t >> 7)) * (61u | t)) ^ t;
            return (float)((t ^ (t >> 14)) / 4294967296.0);
        }
    }
}
