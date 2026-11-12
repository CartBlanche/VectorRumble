using Microsoft.Xna.Framework;

namespace VectorEngine.Rendering;

/// <summary>
/// What the renderers need to know about the screen they draw to, so they do not depend on a concrete ScreenManager.
/// The game's screen manager implements this.
/// </summary>
public interface IRenderContext
{
    /// <summary>The size of the virtual (design-resolution) screen, before it is scaled to the window.</summary>
    Vector2 BaseScreenSize { get; }

    /// <summary>The transform that maps the virtual screen onto the window (scale and letterbox offset).</summary>
    Matrix GlobalTransformation { get; }
}
