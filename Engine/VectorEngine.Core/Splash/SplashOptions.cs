using Microsoft.Xna.Framework;

namespace VectorEngine.Splash;

/// <summary>How the VectorEngine start-up splash looks. The defaults are the engine's own colours; a game can tint them.</summary>
public sealed class SplashOptions
{
    /// <summary>The colour the letters settle into.</summary>
    public Color TextColor { get; init; } = new(0x35, 0xf2, 0xff);

    /// <summary>The colour the V+E mark settles into.</summary>
    public Color MarkColor { get; init; } = new(0xff, 0x4f, 0xd0);

    /// <summary>The colours of the loose lines before they come together.</summary>
    public Color[] DriftColors { get; init; } =
    {
        new(0x35, 0xf2, 0xff), new(0xff, 0x4f, 0xd0), new(0xff, 0xe1, 0x4a), new(0x7d, 0xff, 0x6a), new(0x9a, 0x85, 0xff),
    };

    /// <summary>Seconds the finished logo stays up before it fades out.</summary>
    public float HoldSeconds { get; init; } = 0.7f;

    /// <summary>Seconds the logo takes to fade out at the end (also what a skip does).</summary>
    public float FadeOutSeconds { get; init; } = 0.4f;

    /// <summary>Any key, click, tap or pad button skips ahead to the fade-out.</summary>
    public bool Skippable { get; init; } = true;

    /// <summary>Seeds the random lines, so the same seed gives the same animation every time.</summary>
    public int Seed { get; init; } = 11;

    /// <summary>Draws through the screen manager's bloom component for the glow. Turn off for a game that draws straight to the back buffer
    /// (the splash then glows from layered wide, faint strokes only).</summary>
    public bool UseBloom { get; init; } = true;
}
