using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VectorRumble;

/// <summary>Where text is anchored to the position it is drawn at.</summary>
public enum FrameAlign { Left, Centre, Right }

/// <summary>The little pictures of the Arena Frame menus, drawn as glowing lines.</summary>
public enum FrameIcon { None, Ship, Duel, Trophy, Board, Gear, Info, Exit, Wifi, Mark }

/// <summary>
/// What a <see cref="FrameScreen"/> draws with: thick glowing lines (through the line batch), filled shapes and text,
/// all in the virtual 1280x720 screen. Games use it to draw their own preview panels.
/// </summary>
public sealed class FrameCanvas
{
    readonly VertexPositionColor[] quad = new VertexPositionColor[6];

    /// <summary>The line batch, begun by <see cref="BeginShapes"/>.</summary>
    public LineBatch Lines { get; }

    /// <summary>The sprite batch, begun by <see cref="BeginText"/>.</summary>
    public SpriteBatch Sprites { get; }

    /// <summary>The style in use.</summary>
    public FrameStyle Style { get; }

    /// <summary>The display font: headings, entries, the Back and Next links.</summary>
    public SpriteFont Display { get; set; }

    /// <summary>Fonts: large and medium (descriptions and values) and small (hints).</summary>
    public SpriteFont Large { get; set; }

    /// <summary></summary>
    public SpriteFont Medium { get; set; }

    /// <summary></summary>
    public SpriteFont Small { get; set; }

    /// <summary>0..1, multiplies everything drawn (the screen's fade).</summary>
    public float Alpha { get; set; } = 1f;

    /// <summary>1 for full neon glow, 0 for plain lines.</summary>
    public float Glow { get; set; } = 1f;

    /// <summary>Creates a canvas.</summary>
    public FrameCanvas(LineBatch lines, SpriteBatch sprites, FrameStyle style)
    {
        Lines = lines;
        Sprites = sprites;
        Style = style;
    }

    /// <summary>Starts drawing lines and shapes.</summary>
    public void BeginShapes() => Lines.Begin(Matrix.Identity);

    /// <summary>Finishes lines and shapes.</summary>
    public void EndShapes() => Lines.End();

    /// <summary>Starts drawing text.</summary>
    public void BeginText() => Sprites.Begin();

    /// <summary>Finishes text.</summary>
    public void EndText() => Sprites.End();

    Color Fade(Color c, float a) => new Color(c.R, c.G, c.B, (byte)MathHelper.Clamp(255f * a * Alpha, 0, 255));

    /// <summary>One thick line with square ends.</summary>
    public void Line(Vector2 a, Vector2 b, Color colour, float width, float alpha = 1f)
    {
        Vector2 d = b - a;
        float len = d.Length();
        if (len < 0.001f) return;
        d /= len;
        a -= d * width * 0.5f; b += d * width * 0.5f;
        Vector2 n = new Vector2(-d.Y, d.X) * (width * 0.5f);
        Color c = Fade(colour, alpha);
        // clockwise, as the line batch's triangles must be to survive back-face culling
        quad[0] = new VertexPositionColor(new Vector3(a - n, 0), c);
        quad[1] = new VertexPositionColor(new Vector3(b - n, 0), c);
        quad[2] = new VertexPositionColor(new Vector3(b + n, 0), c);
        quad[3] = quad[0];
        quad[4] = quad[2];
        quad[5] = new VertexPositionColor(new Vector3(a + n, 0), c);
        Lines.DrawTriangleVertices(quad, 6);
    }

    /// <summary>A neon line: a wide faint stroke, a narrower one, then the bright core.</summary>
    public void Neon(Vector2 a, Vector2 b, Color colour, float width = 2f, float glow = 1f)
    {
        float g = Glow * glow;
        if (g > 0f)
        {
            Line(a, b, colour, width * 4.5f, 0.14f * g);
            Line(a, b, colour, width * 2.2f, 0.30f * g);
        }
        Line(a, b, colour, width);
    }

    /// <summary>A neon outline through the points.</summary>
    public void Poly(IList<Vector2> p, bool closed, Color colour, float width = 2f, float glow = 1f)
    {
        for (int i = 0; i + 1 < p.Count; i++) Neon(p[i], p[i + 1], colour, width, glow);
        if (closed && p.Count > 2) Neon(p[p.Count - 1], p[0], colour, width, glow);
    }

    /// <summary>A filled convex shape (a fan of triangles).</summary>
    public void Fill(IList<Vector2> p, Color colour, float alpha = 1f)
    {
        Color c = Fade(colour, alpha);
        for (int i = 1; i + 1 < p.Count; i++)
        {
            quad[0] = new VertexPositionColor(new Vector3(p[0], 0), c);
            quad[1] = new VertexPositionColor(new Vector3(p[i], 0), c);
            quad[2] = new VertexPositionColor(new Vector3(p[i + 1], 0), c);
            Lines.DrawTriangleVertices(quad, 3);
        }
    }

    /// <summary>A rectangle with its corners cut off.</summary>
    public static Vector2[] Chamfer(float x, float y, float w, float h, float c) => new[]
    {
        new Vector2(x + c, y), new Vector2(x + w - c, y), new Vector2(x + w, y + c), new Vector2(x + w, y + h - c),
        new Vector2(x + w - c, y + h), new Vector2(x + c, y + h), new Vector2(x, y + h - c), new Vector2(x, y + c),
    };

    /// <summary>The ship shapes the menus can draw: 0 is the classic dart.</summary>
    public static readonly Vector2[][] ShipShapes =
    {
        new[] { new Vector2(1, 0), new Vector2(-.8f, .7f), new Vector2(-.4f, 0), new Vector2(-.8f, -.7f) },
        new[] { new Vector2(1, 0), new Vector2(-.2f, .95f), new Vector2(-.7f, .3f), new Vector2(-.7f, -.3f), new Vector2(-.2f, -.95f) },
        new[] { new Vector2(1.1f, 0), new Vector2(-.6f, .45f), new Vector2(-.9f, .9f), new Vector2(-.5f, 0), new Vector2(-.9f, -.9f), new Vector2(-.6f, -.45f) },
    };

    /// <summary>The corners of a ship drawn at <paramref name="at"/>.</summary>
    public static Vector2[] ShipPoints(Vector2 at, float angle, float size, int shape = 0)
    {
        Vector2[] s = ShipShapes[Math.Abs(shape) % ShipShapes.Length], r = new Vector2[s.Length];
        float c = MathF.Cos(angle), sn = MathF.Sin(angle);
        for (int i = 0; i < s.Length; i++)
            r[i] = at + new Vector2(s[i].X * size * c - s[i].Y * size * sn, s[i].X * size * sn + s[i].Y * size * c);
        return r;
    }

    /// <summary>A ship outline.</summary>
    public void Ship(Vector2 at, float angle, float size, Color colour, int shape = 0, float width = 2f) =>
        Poly(ShipPoints(at, angle, size, shape), true, colour, width);

    /// <summary>
    /// A ship (or any outline) from its own points, which are drawn pointing up: scaled, turned and moved. An angle of
    /// zero leaves the nose up; a quarter turn (half pi) points it right.
    /// </summary>
    public void Polygon(IList<Vector2> shape, Vector2 at, float angle, float scale, Color colour, float width = 2f)
    {
        float c = MathF.Cos(angle), s = MathF.Sin(angle);
        var p = new Vector2[shape.Count];
        for (int i = 0; i < p.Length; i++)
            p[i] = at + new Vector2(shape[i].X * c - shape[i].Y * s, shape[i].X * s + shape[i].Y * c) * scale;
        Poly(p, true, colour, width);
    }

    /// <summary>A circle outline.</summary>
    public void Circle(Vector2 c, float r, Color colour, float width = 2f, int steps = 28)
    {
        var p = new Vector2[steps];
        for (int i = 0; i < steps; i++) { float a = i * MathF.Tau / steps; p[i] = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r; }
        Poly(p, true, colour, width);
    }

    /// <summary>One of the menu pictures, centred at <paramref name="c"/>, <paramref name="s"/> pixels across a half.</summary>
    public void Icon(FrameIcon icon, Vector2 c, float s, Color col, float t, float w = 3f)
    {
        void P(float[] xy, bool close = false)
        {
            var pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = c + new Vector2(xy[i * 2], xy[i * 2 + 1]) * s;
            Poly(pts, close, col, w);
        }
        switch (icon)
        {
            case FrameIcon.Ship: Ship(c, t * .8f, s, col, 0, w); break;
            case FrameIcon.Duel:
            {
                float bob = MathF.Sin(t) * .12f;
                Ship(c + new Vector2(-.55f, .25f) * s, -.35f + bob, s * .5f, Style.Pilots[0], 0, w);
                Ship(c + new Vector2(.55f, -.25f) * s, MathF.PI - .35f + bob, s * .5f, Style.Pilots[1], 0, w);
                float k = (t * 1.6f) % 1f; Vector2 b = c + new Vector2(-.2f * s + k * s * .4f, .03f * s - k * s * .05f);
                Neon(b + new Vector2(-.12f * s, .02f * s), b + new Vector2(.06f * s, -.01f * s), Style.Spark, w);
                break;
            }
            case FrameIcon.Trophy:
                P(new float[] { -.6f, -.8f, .6f, -.8f, .45f, 0, .15f, .3f, .15f, .55f, .5f, .55f, .5f, .8f, -.5f, .8f, -.5f, .55f, -.15f, .55f, -.15f, .3f, -.45f, 0 }, true);
                P(new float[] { -.6f, -.6f, -.92f, -.6f, -.8f, -.1f, -.5f, -.05f }); P(new float[] { .6f, -.6f, .92f, -.6f, .8f, -.1f, .5f, -.05f }); break;
            case FrameIcon.Board:
                P(new float[] { -.8f, .8f, -.8f, .1f, -.4f, .1f, -.4f, .8f }, true); P(new float[] { -.2f, .8f, -.2f, -.4f, .2f, -.4f, .2f, .8f }, true);
                P(new float[] { .4f, .8f, .4f, .3f, .8f, .3f, .8f, .8f }, true); P(new float[] { -.9f, .8f, .9f, .8f }); break;
            case FrameIcon.Gear:
            {
                var pts = new List<float>();
                for (int i = 0; i < 16; i++) { float a = t * .4f + i * MathF.PI / 8f, r = i % 2 == 1 ? .78f : 1f; pts.Add(MathF.Cos(a) * r); pts.Add(MathF.Sin(a) * r); }
                P(pts.ToArray(), true); Circle(c, .36f * s, col, w); break;
            }
            case FrameIcon.Info:
                Circle(c, s, col, w); P(new float[] { 0, -.08f, 0, .5f }); P(new float[] { 0, -.46f, 0, -.43f }); break;
            case FrameIcon.Exit:
                P(new float[] { -.2f, -.8f, -.8f, -.8f, -.8f, .8f, -.2f, .8f }); P(new float[] { -.3f, 0, .8f, 0 }); P(new float[] { .45f, -.35f, .8f, 0, .45f, .35f }); break;
            case FrameIcon.Wifi:
                foreach (float r in new[] { .35f, .7f, 1.05f })
                {
                    var arc = new Vector2[9]; Vector2 o = c + new Vector2(0, .55f * s);
                    for (int i = 0; i < 9; i++) { float a = MathHelper.Lerp(-MathF.PI * .8f, -MathF.PI * .2f, i / 8f); arc[i] = o + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r * s; }
                    Poly(arc, false, col, w);
                }
                Neon(c + new Vector2(0, .55f * s), c + new Vector2(1, .55f * s), col, w * 1.6f); break;
            case FrameIcon.Mark:
            {
                float k = s / 92f; Vector2 o = c - new Vector2(640, 268) * k;
                foreach (var l in new[] { new[] { 556.4f, 189.318f, 600.659f, 346.682f }, new[] { 600.659f, 346.682f, 664.588f, 189.318f }, new[] { 600.659f, 346.682f, 659.671f, 346.682f }, new[] { 632.624f, 268f, 691.635f, 268f }, new[] { 664.588f, 189.318f, 723.6f, 189.318f } })
                    Neon(o + new Vector2(l[0], l[1]) * k, o + new Vector2(l[2], l[3]) * k, col, w * 1.2f);
                break;
            }
        }
    }

    /// <summary>Draws text whose letters are <paramref name="height"/> pixels tall, in the current text batch.</summary>
    public void Text(SpriteFont font, string text, Vector2 at, Color colour, float height, FrameAlign align = FrameAlign.Left, float alpha = 1f)
    {
        if (string.IsNullOrEmpty(text)) return;
        float scale = height / font.LineSpacing;
        float w = font.MeasureString(text).X * scale;
        float x = align == FrameAlign.Left ? at.X : align == FrameAlign.Centre ? at.X - w / 2f : at.X - w;
        Color c = colour * (alpha * Alpha);
        Sprites.DrawString(font, text, new Vector2(x, at.Y), c, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    /// <summary>How wide text would be at that height.</summary>
    public static float Measure(SpriteFont font, string text, float height) =>
        string.IsNullOrEmpty(text) ? 0f : font.MeasureString(text).X * height / font.LineSpacing;
}
