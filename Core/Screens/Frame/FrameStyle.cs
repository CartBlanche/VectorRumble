using System;
using Microsoft.Xna.Framework;

namespace VectorRumble;

/// <summary>
/// The colours and words of the "Arena Frame" menus (<see cref="FrameScreen"/>), in Vector Rumble's own colours and strings.
/// </summary>
public class FrameStyle
{
    /// <summary>The selected entry, the corner studs and the Next link when hovered.</summary>
    public Color Hot { get; set; } = new Color(255, 154, 31);

    /// <summary>The spark that runs along the laser underline.</summary>
    public Color Spark { get; set; } = new Color(255, 210, 63);

    /// <summary>The outer frame.</summary>
    public Color Frame { get; set; } = new Color(79, 107, 255);

    /// <summary>The preview panel and the inner rule.</summary>
    public Color Panel { get; set; } = new Color(138, 92, 255);

    /// <summary>Text and unselected entries.</summary>
    public Color Text { get; set; } = new Color(241, 238, 252);

    /// <summary>Quiet text: values of unselected rows, key hints, captions.</summary>
    public Color Dim { get; set; } = new Color(139, 137, 176);

    /// <summary>The pilots' colours, in order.</summary>
    public Color[] Pilots { get; set; } =
    {
        new Color(255, 154, 31), new Color(53, 242, 255), new Color(255, 79, 208), new Color(141, 255, 61),
    };

    /// <summary>Text of the key hints at the bottom. Functions, so they follow a change of language.</summary>
    public Func<string> ChooseText { get; set; } = () => Strings.Hint_Choose;

    /// <summary></summary>
    public Func<string> ChangeText { get; set; } = () => Strings.Hint_Change;

    /// <summary></summary>
    public Func<string> SelectText { get; set; } = () => Strings.Hint_Select;

    /// <summary></summary>
    public Func<string> BackText { get; set; } = () => Strings.Back;

    /// <summary></summary>
    public Func<string> NextText { get; set; } = () => Strings.Hint_Next;

    /// <summary>The first row of a screen sits this far from the top, in virtual pixels.</summary>
    public float ContentTop { get; set; } = 252f;
}
