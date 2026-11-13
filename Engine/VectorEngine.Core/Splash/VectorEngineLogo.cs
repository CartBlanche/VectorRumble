using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace VectorEngine.Splash;

/// <summary>
/// The VectorEngine wordmark and mark as straight line segments, on a 1280 x 720 design canvas. The letters are drawn
/// on a 4 x 6 grid; the mark is a V whose right arm is also the spine of an E ("V+E 2": level prongs).
/// </summary>
public static class VectorEngineLogo
{
    public const float Width = 1280f;
    public const float Height = 720f;

    /// <summary>A line of the logo, and which letter it belongs to (-1 for the mark).</summary>
    public readonly record struct Segment(Vector2 A, Vector2 B, int Letter);

    // grid 4 x 6 (I is 2 wide)
    static readonly Dictionary<char, (int Width, int[][][] Polys)> Glyphs = new()
    {
        ['V'] = (4, new[] { new[] { new[] { 0, 0 }, new[] { 2, 6 }, new[] { 4, 0 } } }),
        ['E'] = (4, new[] { new[] { new[] { 4, 0 }, new[] { 0, 0 }, new[] { 0, 6 }, new[] { 4, 6 } }, new[] { new[] { 0, 3 }, new[] { 3, 3 } } }),
        ['C'] = (4, new[] { new[] { new[] { 4, 0 }, new[] { 1, 0 }, new[] { 0, 1 }, new[] { 0, 5 }, new[] { 1, 6 }, new[] { 4, 6 } } }),
        ['T'] = (4, new[] { new[] { new[] { 0, 0 }, new[] { 4, 0 } }, new[] { new[] { 2, 0 }, new[] { 2, 6 } } }),
        ['O'] = (4, new[] { new[] { new[] { 1, 0 }, new[] { 3, 0 }, new[] { 4, 1 }, new[] { 4, 5 }, new[] { 3, 6 }, new[] { 1, 6 }, new[] { 0, 5 }, new[] { 0, 1 }, new[] { 1, 0 } } }),
        ['R'] = (4, new[] { new[] { new[] { 0, 6 }, new[] { 0, 0 }, new[] { 3, 0 }, new[] { 4, 1 }, new[] { 4, 2 }, new[] { 3, 3 }, new[] { 0, 3 } }, new[] { new[] { 2, 3 }, new[] { 4, 6 } } }),
        ['N'] = (4, new[] { new[] { new[] { 0, 6 }, new[] { 0, 0 }, new[] { 4, 6 }, new[] { 4, 0 } } }),
        ['G'] = (4, new[] { new[] { new[] { 4, 1 }, new[] { 3, 0 }, new[] { 1, 0 }, new[] { 0, 1 }, new[] { 0, 5 }, new[] { 1, 6 }, new[] { 3, 6 }, new[] { 4, 5 }, new[] { 4, 3 }, new[] { 2, 3 } } }),
        ['I'] = (2, new[] { new[] { new[] { 0, 0 }, new[] { 2, 0 } }, new[] { new[] { 1, 0 }, new[] { 1, 6 } }, new[] { new[] { 0, 6 }, new[] { 2, 6 } } }),
    };

    /// <summary>The centre and half-size of the mark on the design canvas.</summary>
    public static readonly Vector2 MarkCentre = new(Width / 2f, 268f);
    public const float MarkRadius = 88f;

    /// <summary>The letters of "VECTOR ENGINE", twelve of them, as line segments.</summary>
    public static IReadOnlyList<Segment> Wordmark { get; } = BuildWordmark();

    /// <summary>The V+E mark as line segments (unit space scaled to <see cref="MarkRadius"/> around <see cref="MarkCentre"/>).</summary>
    public static IReadOnlyList<Segment> Mark { get; } = BuildMark();

    /// <summary>How many letters the wordmark has.</summary>
    public const int LetterCount = 12;

    static IReadOnlyList<Segment> BuildWordmark()
    {
        const float u = 16f, gap = 1.3f, wordGap = 3.4f, top = 420f;
        string[] words = { "VECTOR", "ENGINE" };

        // total width first, so the words can be centred
        float x = 0f;
        foreach (string word in words)
        {
            foreach (char ch in word)
                x += Glyphs[ch].Width + gap;
            x += wordGap - gap;
        }
        float total = x - wordGap;
        float left = Width / 2f - total * u / 2f;

        var segments = new List<Segment>();
        x = 0f;
        int letter = 0;
        foreach (string word in words)
        {
            foreach (char ch in word)
            {
                var (width, polys) = Glyphs[ch];
                foreach (int[][] poly in polys)
                    for (int i = 0; i < poly.Length - 1; i++)
                        segments.Add(new Segment(
                            new Vector2(left + (x + poly[i][0]) * u, top + poly[i][1] * u),
                            new Vector2(left + (x + poly[i + 1][0]) * u, top + poly[i + 1][1] * u), letter));
                x += width + gap;
                letter++;
            }
            x += wordGap - gap;
        }
        return segments;
    }

    static IReadOnlyList<Segment> BuildMark()
    {
        // The V's left arm runs from L down to the point B, then up the right arm to T, which is also the E's spine.
        // The E's three prongs are level and leave the spine at 0, 1/2 and 1 of the way up.
        Vector2 l = new(-.9f, -.9f), b = new(-.45f, .7f), t = new(.2f, -.9f);
        float[] along = { 0f, .5f, 1f };
        const float prong = .6f;

        var polys = new List<Vector2[]> { new[] { l, b, t } };
        foreach (float f in along)
        {
            Vector2 p = b + (t - b) * f;
            polys.Add(new[] { p, p + new Vector2(prong, 0f) });
        }

        // centre it and scale to fit -1..1
        float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
        foreach (var poly in polys)
            foreach (var p in poly)
            {
                x0 = Math.Min(x0, p.X); x1 = Math.Max(x1, p.X);
                y0 = Math.Min(y0, p.Y); y1 = Math.Max(y1, p.Y);
            }
        float scale = 1.9f / Math.Max(x1 - x0, y1 - y0);
        Vector2 centre = new((x0 + x1) / 2f, (y0 + y1) / 2f);

        var segments = new List<Segment>();
        foreach (var poly in polys)
            for (int i = 0; i < poly.Length - 1; i++)
                segments.Add(new Segment(
                    MarkCentre + (poly[i] - centre) * scale * MarkRadius,
                    MarkCentre + (poly[i + 1] - centre) * scale * MarkRadius, -1));
        return segments;
    }
}
