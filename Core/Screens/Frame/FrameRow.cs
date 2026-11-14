using System;
using Microsoft.Xna.Framework;

namespace VectorRumble;

/// <summary>What a <see cref="FrameRow"/> does when the player steps it.</summary>
public enum FrameRowKind
{
    /// <summary>A big entry of a list screen: select it to go somewhere.</summary>
    Entry,
    /// <summary>A row you press to do something (optionally with a chevron).</summary>
    Action,
    /// <summary>A row whose value steps through choices with left and right.</summary>
    Choice,
    /// <summary>A row that is on or off.</summary>
    Toggle,
    /// <summary>A row with a ten-segment bar.</summary>
    Slider,
    /// <summary>A row that opens or closes the rows under it.</summary>
    Expander,
}

/// <summary>One line of a <see cref="FrameScreen"/>: a big list entry or a row with a value.</summary>
public class FrameRow
{
    /// <summary>The label.</summary>
    public string Label { get; set; }

    /// <summary>Small print shown in the preview panel while the entry is selected (list layout).</summary>
    public string Description { get; set; }

    /// <summary>Quiet text at the right end of an entry, such as "2 of 4" or "Soon".</summary>
    public string Meta { get; set; }

    /// <summary>The picture in the preview panel (list layout).</summary>
    public FrameIcon Icon { get; set; }

    /// <summary>Draws this entry's own picture in the panel instead of its <see cref="Icon"/>: the canvas, the panel's area and the screen's time.</summary>
    public Action<FrameCanvas, Rectangle, float> PanelArt { get; set; }

    /// <summary>What the row does.</summary>
    public FrameRowKind Kind { get; set; }

    /// <summary>The value shown at the right of a row. A function, so it follows the setting and the language.</summary>
    public Func<string> Value { get; set; }

    /// <summary>For sliders: 0 to 1.</summary>
    public Func<float> Fraction { get; set; }

    /// <summary>For expanders: whether the rows under it are showing.</summary>
    public Func<bool> Expanded { get; set; }

    /// <summary>A row that is not shown (such as the rules under a closed "More").</summary>
    public Func<bool> Hidden { get; set; }

    /// <summary>A value the player cannot change. The row shows <see cref="LockedNote"/> beside it.</summary>
    public Func<bool> Locked { get; set; }

    /// <summary>The words shown beside a locked value.</summary>
    public string LockedNote { get; set; }

    /// <summary>Draws a small picture to the left of the value (a colour swatch, a ship).</summary>
    public Action<FrameCanvas, Vector2> Adornment { get; set; }

    /// <summary>Shows a chevron at the right end of an action row.</summary>
    public bool Chevron { get; set; }

    /// <summary>The row leaves the game (opens a web page): its chevron is drawn in the link colour.</summary>
    public bool External { get; set; }

    /// <summary>The row was chosen.</summary>
    public event Action Selected;

    /// <summary>The player stepped the row: -1 or +1.</summary>
    public event Action<int> Adjusted;

    /// <summary>Creates a row.</summary>
    public FrameRow(string label, FrameRowKind kind = FrameRowKind.Entry)
    {
        Label = label;
        Kind = kind;
    }

    internal bool IsHidden => Hidden != null && Hidden();
    internal bool IsLocked => Locked != null && Locked();

    internal void Select() => Selected?.Invoke();

    internal void Adjust(int direction)
    {
        if (!IsLocked) Adjusted?.Invoke(direction);
    }

    internal bool Steps => Kind is FrameRowKind.Choice or FrameRowKind.Toggle or FrameRowKind.Slider;
}

/// <summary>The "Next" link at the bottom right of a wizard step. On the last step it reads Start.</summary>
public class FrameNext
{
    /// <summary>The words; empty means the style's "Next".</summary>
    public Func<string> Label { get; set; }

    /// <summary>What it does.</summary>
    public Action Action { get; set; }

    /// <summary>This step's number, from 1. Zero hides the step markers.</summary>
    public int Step { get; set; }

    /// <summary>How many steps there are.</summary>
    public int Steps { get; set; } = 2;
}
