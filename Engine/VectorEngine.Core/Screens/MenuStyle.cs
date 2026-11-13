using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace VectorEngine.Screens;

/// <summary>
/// How the engine's menus, message boxes and loading screen look and sound. The defaults are Vector Rumble's traditional
/// menu; a game sets <see cref="ScreenManagerOptions.MenuStyle"/> to change them, or does not use the engine's menus
/// at all (SolidOps has its own radial menus).
/// </summary>
public class MenuStyle
{
    /// <summary>The default for <see cref="MenuTop"/>, for code that lays things out around the menu.</summary>
    public const float DefaultMenuTop = 0.45f;

    /// <summary>Where the first entry is, as a fraction of the screen's height.</summary>
    public float MenuTop { get; set; } = DefaultMenuTop;

    /// <summary>The colour of an entry.</summary>
    public Color NormalColor { get; set; } = Color.White;

    /// <summary>The colour of the entry the player is on.</summary>
    public Color SelectedColor { get; set; } = Color.Orange;

    /// <summary>How fast the selected entry pulses, in radians a second.</summary>
    public float PulseSpeed { get; set; } = 6f;

    /// <summary>How much the selected entry grows when it pulses (0.05 is 5%).</summary>
    public float PulseAmount { get; set; } = 0.05f;

    /// <summary>How far, in pixels, the menu slides when it comes on.</summary>
    public float SlideOnDistance { get; set; } = 256f;

    /// <summary>How far, in pixels, the menu slides when it goes off.</summary>
    public float SlideOffDistance { get; set; } = 512f;

    /// <summary>The audio cue for moving between entries (empty for none).</summary>
    public string MoveCue { get; set; } = "menuMove";

    /// <summary>The audio cue for choosing an entry (empty for none).</summary>
    public string SelectCue { get; set; } = "menuSelect";

    /// <summary>Loads the smaller font the message box's usage text is drawn in.</summary>
    public Func<ContentManager, SpriteFont> LoadSmallFont { get; set; } = content => content.Load<SpriteFont>("Fonts/retroSmall");

    /// <summary>The text under a message box's message. A function, so it follows a change of language.</summary>
    public Func<string> MessageBoxUsageText { get; set; } = () => "A, Space, Enter: OK\nB, Esc: Cancel";

    /// <summary>The text of the slow loading screen. A function, so it follows a change of language.</summary>
    public Func<string> LoadingText { get; set; } = () => "Loading...";

    /// <summary>The colour of a message box's outline.</summary>
    public Func<Color> MessageBoxBorderColor { get; set; } = () => Color.Blue;
}
