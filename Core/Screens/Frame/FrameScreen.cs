using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VectorRumble;

/// <summary>How a <see cref="FrameScreen"/> lays out its lines.</summary>
public enum FrameLayout
{
    /// <summary>Big entries on the left; the selected one is explained in a picture panel on the right.</summary>
    List,
    /// <summary>Rows with values (settings, wizard steps) on the left; an optional panel on the right.</summary>
    Rows,
}

/// <summary>
/// A menu in the "Arena Frame" look: a chamfered frame with corner studs, big left-aligned entries, a ship that
/// points at the one you are on and a laser line under it, and a panel on the right. A step of a wizard has a
/// "Next" link bottom right. It works with a keyboard, a mouse, a touch screen or a gamepad.
/// </summary>
public abstract class FrameScreen : VectorEngine.Screens.GameScreen
{
    /// <summary>Where the content column starts.</summary>
    protected const float Left = 86f;

    readonly List<FrameRow> rows = new();
    readonly List<Rectangle> rects = new();
    FrameCanvas canvas;
    LineBatch lineBatch;
    AudioManager audio;
    int selected;
    Vector2 lastPointer = new(-1, -1);
    Rectangle backRect, nextRect;
    bool hoverBack, hoverNext;
    float time;

    /// <summary>The lines of the screen.</summary>
    protected IList<FrameRow> Rows => rows;

    /// <summary>The heading. Empty on the main menu, which shows the game's big title instead.</summary>
    protected string Heading { get; set; } = "";

    /// <summary>How the lines are laid out.</summary>
    protected FrameLayout Layout { get; set; } = FrameLayout.List;

    /// <summary>The wizard's Next link, or null.</summary>
    protected FrameNext Next { get; set; }

    /// <summary>Whether the Back link shows (and cancel goes back).</summary>
    protected bool ShowBack { get; set; } = true;

    /// <summary>Whether the panel on the right is drawn (rows layout: only if <see cref="DrawPanel"/> draws something).</summary>
    protected bool ShowPanel { get; set; } = true;

    /// <summary>How wide the column of rows is: narrower when a panel sits beside it.</summary>
    protected virtual float RowsWidth => ShowPanel ? 600f : 840f;

    /// <summary>The line the player is on, among the visible lines.</summary>
    protected int SelectedIndex
    {
        get => selected;
        set => selected = Math.Max(0, Math.Min(value, Math.Max(0, Visible().Count - 1)));
    }

    /// <summary>The seconds since the screen opened, for animation.</summary>
    protected float Time => time;

    /// <summary>Creates the screen.</summary>
    protected FrameScreen()
    {
        TransitionOnTime = TimeSpan.FromSeconds(0.45);
        TransitionOffTime = TimeSpan.FromSeconds(0.3);
    }

    /// <summary>The colours and words every frame screen shares.</summary>
    public static FrameStyle Theme { get; } = new FrameStyle();

    FrameStyle Style => Theme;

    /// <summary>The visible lines.</summary>
    protected List<FrameRow> Visible() => rows.Where(r => !r.IsHidden).ToList();

    /// <inheritdoc />
    public override void LoadContent()
    {
        audio = (AudioManager)ScreenManager.Game.Services.GetService(typeof(AudioManager));
        lineBatch = new LineBatch(ScreenManager.GraphicsDevice);
        lineBatch.SetProjection(Matrix.CreateOrthographicOffCenter(0f, Display.Viewport.Width, Display.Viewport.Height, 0f, 0f, 1f));
        canvas = new FrameCanvas(lineBatch, ScreenManager.SpriteBatch, Style)
        {
            Large = ScreenManager.Font,
            // the display font has Latin letters only: Chinese, Japanese and Korean keep the pixel font, which has their characters
            Display = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is "zh" or "ja" or "ko"
                ? ScreenManager.Game.Content.LoadLocalized<SpriteFont>("Fonts/retroLarge")
                : ScreenManager.Game.Content.Load<SpriteFont>("Fonts/displayLarge"),
            Medium = ScreenManager.Game.Content.LoadLocalized<SpriteFont>("Fonts/retroMedium"),
            Small = ScreenManager.Game.Content.LoadLocalized<SpriteFont>("Fonts/retroSmall"),
        };
        base.LoadContent();
    }

    /// <summary>The screen's picture panel, to the right of the lines. The default draws the selected entry's icon and description.</summary>
    protected virtual void DrawPanel(FrameCanvas c, Rectangle area, FrameRow selectedRow)
    {
        if (selectedRow == null) return;
        if (selectedRow.PanelArt != null) { selectedRow.PanelArt(c, area, time); return; }
        c.Icon(selectedRow.Icon, new Vector2(area.Center.X, area.Y + 160), 92f, Style.Hot, time);
    }

    /// <summary>Text for the panel: the selected entry's description, drawn centred near the bottom.</summary>
    protected virtual void DrawPanelText(FrameCanvas c, Rectangle area, FrameRow selectedRow)
    {
        if (selectedRow?.Description == null) return;
        string[] lines = Wrap(selectedRow.Description, area.Width - 64f, c.Large, 25f);
        for (int i = 0; i < lines.Length; i++)
            c.Text(c.Large, lines[i], new Vector2(area.Center.X, area.Bottom - 80 - (lines.Length - 1) * 30 + i * 30), Style.Text, 25f, FrameAlign.Centre);
    }

    /// <summary>Extra shapes a screen draws itself (the About screen's mark). Called inside the shapes pass.</summary>
    protected virtual void DrawBody(FrameCanvas c) { }

    /// <summary>Extra text a screen draws itself. Called inside the text pass.</summary>
    protected virtual void DrawBodyText(FrameCanvas c) { }

    /// <summary>Draws the game's title: big on a screen without a heading, small above a heading.</summary>
    protected virtual void DrawTitle(bool large) { }

    static string[] Wrap(string s, float width, SpriteFont font, float height)
    {
        var lines = new List<string>(); string cur = "";
        foreach (string w in s.Split(' '))
        {
            string t = cur.Length == 0 ? w : cur + " " + w;
            if (cur.Length > 0 && FrameCanvas.Measure(font, t, height) > width) { lines.Add(cur); cur = w; } else cur = t;
        }
        if (cur.Length > 0) lines.Add(cur);
        return lines.ToArray();
    }

    /// <summary>Where the panel is: the right-hand side.</summary>
    protected virtual Rectangle PanelArea => new(720, 196, 470, 400);

    /// <summary>The player cancelled (Esc, B, the Back link).</summary>
    protected virtual void OnCancel() => ExitScreen();

    #region Input

    /// <inheritdoc />
    public override void HandleInput(GameTime gameTime, InputState input)
    {
        var vis = Visible();
        if (vis.Count > 0) selected = Math.Max(0, Math.Min(selected, vis.Count - 1));

        Vector2 pointer = input.PointerPosition;
        // a pointer resting where it was when the screen opened does not choose an entry: only moving it does
        bool moved = lastPointer.X >= 0 && pointer != lastPointer;
        lastPointer = pointer;
        Point p = new((int)pointer.X, (int)pointer.Y);
        hoverBack = ShowBack && backRect.Contains(p);
        hoverNext = Next != null && nextRect.Contains(p);

        if (moved && !hoverBack && !hoverNext)
        {
            for (int i = 0; i < rects.Count && i < vis.Count; i++)
                if (rects[i].Contains(p)) { if (selected != i) { selected = i; Cue(false); } break; }
        }

        if (input.PointerPressed)
        {
            Point press = new((int)input.PointerPressPosition.X, (int)input.PointerPressPosition.Y);
            if (ShowBack && backRect.Contains(press)) { OnCancel(); return; }
            if (Next != null && nextRect.Contains(press)) { Cue(true); Next.Action?.Invoke(); return; }
            for (int i = 0; i < rects.Count && i < vis.Count; i++)
            {
                if (!rects[i].Contains(press)) continue;
                selected = i;
                FrameRow r = vis[i];
                if (r.Steps) r.Adjust(press.X < rects[i].Right - 170 ? -1 : 1);
                else Activate(r);
                return;
            }
        }

        if (vis.Count > 0)
        {
            if (input.MenuUp) { selected = (selected - 1 + vis.Count) % vis.Count; Cue(false); }
            if (input.MenuDown) { selected = (selected + 1) % vis.Count; Cue(false); }
            if (input.SelectPrevious) vis[selected].Adjust(-1);
            else if (input.SelectNext) vis[selected].Adjust(1);
        }

        if (input.MenuSelect)
        {
            FrameRow r = vis.Count > 0 ? vis[selected] : null;
            if (Next != null && (r == null || r.Kind != FrameRowKind.Expander && r.Kind != FrameRowKind.Action && r.Kind != FrameRowKind.Entry)) { Cue(true); Next.Action?.Invoke(); }
            else if (r != null) Activate(r);
        }
        else if (input.MenuCancel && ShowBack) OnCancel();
        else if (input.MenuCancel) OnCancel();
    }

    void Activate(FrameRow r)
    {
        if (r.Kind == FrameRowKind.Choice || r.Kind == FrameRowKind.Toggle || r.Kind == FrameRowKind.Slider) { r.Adjust(1); return; }
        Cue(true);
        r.Select();
    }

    void Cue(bool select) => audio?.PlayCue(select ? ScreenManager.MenuStyle.SelectCue : ScreenManager.MenuStyle.MoveCue);

    #endregion

    /// <inheritdoc />
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
        time += (float)gameTime.ElapsedGameTime.TotalSeconds;
    }

    #region Draw

    /// <inheritdoc />
    public override void Draw(GameTime gameTime)
    {
        if (canvas == null) return;
        var vis = Visible();
        if (vis.Count > 0) selected = Math.Max(0, Math.Min(selected, vis.Count - 1));
        FrameRow sel = vis.Count > 0 ? vis[selected] : null;
        canvas.Alpha = TransitionAlpha;
        canvas.Glow = 1f;
        Color hot = Style.Hot;

        DrawTitle(string.IsNullOrEmpty(Heading));

        // shapes: the glow is drawn into the lines themselves (the bloom pass would overwrite what is behind this screen)
        canvas.BeginShapes();
        DrawChrome();
        rects.Clear();
        if (Layout == FrameLayout.List) DrawListShapes(vis, selected, hot); else DrawRowShapes(vis, selected, hot);
        Rectangle area = PanelArea;
        if (ShowPanel)
        {
            var frame = FrameCanvas.Chamfer(area.X, area.Y, area.Width, area.Height, 28);
            canvas.Fill(frame, new Color(8, 8, 26), 0.5f);
            canvas.Poly(frame, true, Style.Panel, 1.8f, .7f);
            DrawPanel(canvas, area, sel);
        }
        DrawBody(canvas);
        DrawFooterShapes();
        canvas.EndShapes();

        // text on top
        canvas.BeginText();
        DrawHeading();
        if (Layout == FrameLayout.List) DrawListText(vis, selected, hot); else DrawRowText(vis, selected, hot);
        if (ShowPanel) DrawPanelText(canvas, area, sel);
        DrawBodyText(canvas);
        DrawFooterText();
        canvas.EndText();
    }

    void DrawChrome()
    {
        const float c = 46f;
        var frame = FrameCanvas.Chamfer(26, 26, Display.BaseWidth - 52, Display.BaseHeight - 52, c);
        canvas.Poly(frame, true, Style.Frame, 2.2f, .9f);
    }

    void DrawHeading()
    {
        if (string.IsNullOrEmpty(Heading)) return;
        canvas.Text(canvas.Display, Heading.ToUpperInvariant(), new Vector2(88, 128), Style.Text, 50f);
    }

    float Slide(int i) => (1f - MathHelper.Clamp((1f - TransitionPosition) * 1.6f - i * 0.1f, 0f, 1f)) * 70f;

    #region list layout

    float ListStep(bool main) => string.IsNullOrEmpty(Heading) ? 82f : 96f;
    float ListTop => string.IsNullOrEmpty(Heading) ? 262f : 272f;

    void DrawListShapes(List<FrameRow> vis, int sel, Color hot)
    {
        for (int i = 0; i < vis.Count; i++)
        {
            float y = ListTop + i * ListStep(false), dx = Slide(i);
            rects.Add(new Rectangle(70, (int)(y - 44), 600, 76));
            if (i != sel) continue;
            string lab = vis[i].Label.ToUpperInvariant();
            float lw = Math.Max(FrameCanvas.Measure(canvas.Display, lab, 42f), 260f);
            canvas.Neon(new Vector2(128 + dx, y + 20), new Vector2(128 + lw + dx, y + 20), hot, 2.4f);
            float k = (time * 1.2f) % 1f;
            canvas.Neon(new Vector2(128 + k * lw + dx, y + 20), new Vector2(128 + k * lw + 24 + dx, y + 20), Style.Spark, 3f, .8f);
        }
    }

    void DrawListText(List<FrameRow> vis, int sel, Color hot)
    {
        for (int i = 0; i < vis.Count; i++)
        {
            float y = ListTop + i * ListStep(false), dx = Slide(i);
            bool on = i == sel && IsActive;
            canvas.Text(canvas.Display, vis[i].Label.ToUpperInvariant(), new Vector2(128 + dx, y - 34), on ? hot : Style.Text, 42f);
            if (vis[i].Meta != null)
                canvas.Text(canvas.Medium, vis[i].Meta, new Vector2(640 + dx, y - 22), on ? Style.Text : Style.Dim, 22f, FrameAlign.Right);
        }
    }

    #endregion

    #region rows layout

    int RowCount(List<FrameRow> vis) => vis.Count;

    float RowStep(List<FrameRow> vis, float basic)
    {
        float bottom = 604f, y0 = Style.ContentTop;
        return vis.Count > 1 ? Math.Min(basic, (bottom - y0) / (vis.Count - 1)) : basic;
    }

    void DrawRowShapes(List<FrameRow> vis, int sel, Color hot)
    {
        float step = RowStep(vis, ShowPanel ? 70f : 74f), x = Left, w = RowsWidth;
        for (int i = 0; i < vis.Count; i++)
        {
            float y = Style.ContentTop + i * step, dx = Slide(i);
            var rect = new Rectangle((int)x, (int)(y - step / 2 + 4), (int)w, (int)(step - 8));
            rects.Add(rect);
            FrameRow r = vis[i];
            if (i == sel)
            {
                canvas.Fill(new[] { new Vector2(rect.X + dx, rect.Y), new Vector2(rect.Right + dx, rect.Y), new Vector2(rect.Right + dx, rect.Bottom), new Vector2(rect.X + dx, rect.Bottom) }, hot, .08f);
                canvas.Poly(new[] { new Vector2(rect.X + dx, rect.Y + 14), new Vector2(rect.X + dx, rect.Y), new Vector2(rect.X + 16 + dx, rect.Y) }, false, hot, 2f, .9f);
                canvas.Poly(new[] { new Vector2(rect.Right + dx, rect.Bottom - 14), new Vector2(rect.Right + dx, rect.Bottom), new Vector2(rect.Right - 16 + dx, rect.Bottom) }, false, hot, 2f, .9f);
            }
            else canvas.Line(new Vector2(rect.X + 6 + dx, rect.Bottom), new Vector2(rect.Right - 6 + dx, rect.Bottom), Style.Panel, 1f, .16f);

            float vx2 = x + w - 18 + dx, vx1 = vx2 - 340, cy = y;
            bool on = i == sel;
            Color arrowCol = on ? hot : new Color(58, 58, 102);
            bool locked = r.IsLocked;
            switch (r.Kind)
            {
                case FrameRowKind.Slider:
                    SliderBar(vx1 + 24, cy - 7, 340 - 48, r.Fraction?.Invoke() ?? 0f, on ? hot : Style.Panel);
                    Arrows(vx1, vx2, cy, arrowCol, on);
                    break;
                case FrameRowKind.Action:
                    if (r.Chevron) canvas.Poly(new[] { new Vector2(vx2 - 10, cy - 11), new Vector2(vx2, cy), new Vector2(vx2 - 10, cy + 11) }, false, on ? hot : Style.Dim, 2f, on ? .8f : 0f);
                    break;
                case FrameRowKind.Expander:
                    bool open = r.Expanded?.Invoke() ?? false;
                    canvas.Poly(open ? new[] { new Vector2(vx2 - 22, cy - 5), new Vector2(vx2 - 11, cy + 6), new Vector2(vx2, cy - 5) }
                                     : new[] { new Vector2(vx2 - 18, cy - 11), new Vector2(vx2 - 7, cy), new Vector2(vx2 - 18, cy + 11) }, false, on ? hot : Style.Dim, 2f, on ? .8f : 0f);
                    break;
                default:
                    if (!locked) Arrows(vx1, vx2, cy, arrowCol, on);
                    if (r.Adornment != null)
                    {
                        float tw = FrameCanvas.Measure(canvas.Medium, r.Value?.Invoke() ?? "", 24f);
                        r.Adornment(canvas, new Vector2((vx1 + vx2) / 2 - tw / 2 - 30, cy));
                    }
                    break;
            }
        }
    }

    void Arrows(float x1, float x2, float y, Color col, bool on)
    {
        canvas.Poly(new[] { new Vector2(x1 + 8, y - 11), new Vector2(x1, y), new Vector2(x1 + 8, y + 11) }, false, col, 2f, on ? .8f : 0f);
        canvas.Poly(new[] { new Vector2(x2 - 8, y - 11), new Vector2(x2, y), new Vector2(x2 - 8, y + 11) }, false, col, 2f, on ? .8f : 0f);
    }

    void SliderBar(float x, float y, float w, float v, Color col)
    {
        const int n = 10; const float gap = 5f;
        float sw = (w - gap * (n - 1)) / n;
        for (int i = 0; i < n; i++)
        {
            bool lit = (i + 1f) / n <= v + 0.001f;
            float x0 = x + i * (sw + gap);
            var seg = new[] { new Vector2(x0 + 4, y + 16), new Vector2(x0 + sw, y + 16), new Vector2(x0 + sw - 4, y), new Vector2(x0, y) };
            canvas.Fill(seg, lit ? col : Style.Dim, lit ? 1f : .22f);
        }
    }

    void DrawRowText(List<FrameRow> vis, int sel, Color hot)
    {
        float step = RowStep(vis, ShowPanel ? 70f : 74f), x = Left, w = RowsWidth;
        for (int i = 0; i < vis.Count; i++)
        {
            float y = Style.ContentTop + i * step, dx = Slide(i);
            FrameRow r = vis[i];
            bool on = i == sel && IsActive;
            // a long label shrinks to fit, rather than running into the value or the panel
            string label = r.Label.ToUpperInvariant();
            float room = w - 44 - (r.Kind is FrameRowKind.Action or FrameRowKind.Expander ? 40f : 360f), lh = 27f;
            float lw = FrameCanvas.Measure(canvas.Display, label, lh);
            if (lw > room) lh = Math.Max(14f, lh * room / lw);
            canvas.Text(canvas.Display, label, new Vector2(x + 22 + dx, y - lh / 2f), on ? hot : Style.Text, lh);
            float vx2 = x + w - 18 + dx, vx1 = vx2 - 340, mid = (vx1 + vx2) / 2;
            string value = r.Value?.Invoke() ?? "";
            switch (r.Kind)
            {
                case FrameRowKind.Slider:
                    canvas.Text(canvas.Medium, value, new Vector2(vx1 - 14, y - 10), Style.Dim, 18f, FrameAlign.Right);
                    break;
                case FrameRowKind.Action: case FrameRowKind.Expander: break;
                default:
                    if (r.IsLocked)
                    {
                        canvas.Text(canvas.Small, (r.LockedNote ?? "").ToUpperInvariant(), new Vector2(vx1 + 4, y - 8), on ? hot : Style.Dim, 13f);
                        canvas.Text(canvas.Medium, value, new Vector2(vx2 - 8, y - 14), Style.Dim, 24f, FrameAlign.Right);
                    }
                    else canvas.Text(canvas.Medium, value, new Vector2(mid, y - 14), on ? Style.Text : Style.Dim, 24f, FrameAlign.Centre);
                    break;
            }
        }
    }

    #endregion

    #region footer

    void DrawFooterShapes()
    {
        float y = 664f;
        if (ShowBack)
        {
            Color c = hoverBack ? Style.Hot : Style.Text;
            canvas.Poly(new[] { new Vector2(88, y - 14), new Vector2(74, y), new Vector2(88, y + 14) }, false, c, 2f, .8f);
            backRect = new Rectangle(64, (int)y - 22, 130, 44);
        }
        else backRect = Rectangle.Empty;

        if (Next != null)
        {
            Color c = hoverNext ? Style.Hot : Style.Text;
            float right = Display.BaseWidth - 70f, lw = NextWidth();
            canvas.Poly(new[] { new Vector2(right - 18, y - 14), new Vector2(right - 4, y), new Vector2(right - 18, y + 14) }, false, c, 2f, .8f);
            nextRect = new Rectangle((int)(right - 34 - lw - 12), (int)y - 22, (int)(lw + 34 + 18), 44);
            if (Next.Step > 0)
            {
                float mx = right - 34 - lw - 44;
                for (int i = 0; i < Next.Steps; i++)
                {
                    float dx = mx - (Next.Steps - 1 - i) * 22; bool now = i + 1 == Next.Step;
                    canvas.Poly(new[] { new Vector2(dx, y - 7), new Vector2(dx + 7, y), new Vector2(dx, y + 7), new Vector2(dx - 7, y) }, true, now ? Style.Hot : Style.Dim, 2f, now ? .8f : 0f);
                }
            }
        }
        else nextRect = Rectangle.Empty;

        if (!Platform.IsMobile) DrawHintShapes();
    }

    string NextLabel() => (Next?.Label?.Invoke() ?? Style.NextText()).ToUpperInvariant();
    float NextWidth() => FrameCanvas.Measure(canvas.Display, NextLabel(), 20f);

    void DrawFooterText()
    {
        float y = 664f;
        if (ShowBack) canvas.Text(canvas.Display, Style.BackText().ToUpperInvariant(), new Vector2(104, y - 10), hoverBack ? Style.Hot : Style.Text, 20f);
        if (Next != null)
            canvas.Text(canvas.Display, NextLabel(), new Vector2(Display.BaseWidth - 70f - 34, y - 10), hoverNext ? Style.Hot : Style.Text, 20f, FrameAlign.Right);
        if (!Platform.IsMobile) DrawHintText();
    }

    // key hints, bottom centre: [up down] Choose  [left right] Change  [Enter] Select/Next  [Esc] Back
    List<(string key, string label)> Hints()
    {
        var l = new List<(string, string)> { ("UD", Style.ChooseText()) };
        if (Layout == FrameLayout.Rows) l.Add(("LR", Style.ChangeText()));
        l.Add(("Enter", Next != null ? NextLabelTitle() : Style.SelectText()));
        if (ShowBack) l.Add(("Esc", Style.BackText()));
        return l;
    }

    string NextLabelTitle() => Next?.Label?.Invoke() ?? Style.NextText();

    float KeyWidth(string key) => key is "UD" or "LR" ? 36f : Math.Max(30f, FrameCanvas.Measure(canvas.Small, key, 14f) + 16f);

    float HintTotal(List<(string key, string label)> hints)
    {
        float total = -30f;
        foreach (var (k, l) in hints) total += KeyWidth(k) + 8 + FrameCanvas.Measure(canvas.Small, l, 15f) + 30;
        return total;
    }

    void DrawHintShapes()
    {
        var hints = Hints(); float x = Display.BaseWidth / 2f - HintTotal(hints) / 2f, y = 666f;
        foreach (var (k, l) in hints)
        {
            float kw = KeyWidth(k);
            var box = FrameCanvas.Chamfer(x, y - 19, kw, 24, 5);
            canvas.Poly(box, true, new Color(58, 58, 102), 1.5f, 0f);
            Vector2 c = new(x + kw / 2, y - 7);
            if (k == "UD") { canvas.Poly(new[] { c + new Vector2(-9, 2), c + new Vector2(-5, -3), c + new Vector2(-1, 2) }, false, Style.Text, 1.5f, 0f); canvas.Poly(new[] { c + new Vector2(1, -3), c + new Vector2(5, 2), c + new Vector2(9, -3) }, false, Style.Text, 1.5f, 0f); }
            if (k == "LR") { canvas.Poly(new[] { c + new Vector2(-3, -5), c + new Vector2(-8, 0), c + new Vector2(-3, 5) }, false, Style.Text, 1.5f, 0f); canvas.Poly(new[] { c + new Vector2(3, -5), c + new Vector2(8, 0), c + new Vector2(3, 5) }, false, Style.Text, 1.5f, 0f); }
            x += kw + 8 + FrameCanvas.Measure(canvas.Small, l, 15f) + 30;
        }
    }

    void DrawHintText()
    {
        var hints = Hints(); float x = Display.BaseWidth / 2f - HintTotal(hints) / 2f, y = 666f;
        foreach (var (k, l) in hints)
        {
            float kw = KeyWidth(k);
            if (k is not ("UD" or "LR")) canvas.Text(canvas.Small, k, new Vector2(x + kw / 2, y - 15), Style.Text, 14f, FrameAlign.Centre);
            canvas.Text(canvas.Small, l, new Vector2(x + kw + 8, y - 16), Style.Dim, 15f);
            x += kw + 8 + FrameCanvas.Measure(canvas.Small, l, 15f) + 30;
        }
    }

    #endregion

    #endregion
}
