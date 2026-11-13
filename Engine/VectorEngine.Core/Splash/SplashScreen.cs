using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using VectorEngine.Input;
using VectorEngine.Rendering;
using VectorEngine.Screens;

namespace VectorEngine.Splash;

/// <summary>
/// The VectorEngine start-up splash: loose lines come together as the V+E mark and the words "VECTOR ENGINE", hold, then
/// fade out. Add it first and give it a callback that adds the game's first screens: <c>AddScreen(new SplashScreen(options, () => { ... }))</c>.
/// Any key, click, tap or pad button skips to the fade-out (<see cref="SplashOptions.Skippable"/>).
/// </summary>
public class SplashScreen : GameScreen
{
    readonly SplashOptions options;
    readonly Action onFinished;
    readonly List<SplashLine> lines = new();
    readonly VertexPositionColor[] quad = new VertexPositionColor[6];
    SplashAnimation animation;
    float elapsed;
    bool finished;

    /// <param name="options">How it looks; null for the engine's own colours</param>
    /// <param name="onFinished">Called once, when the splash is over (after it fades, or at once if it is skipped through to the end)</param>
    public SplashScreen(SplashOptions options = null, Action onFinished = null)
    {
        this.options = options ?? new SplashOptions();
        this.onFinished = onFinished;
        TransitionOnTime = TimeSpan.Zero;
        TransitionOffTime = TimeSpan.Zero;
    }

    /// <summary>
    /// Shows the splash, then runs <paramref name="first"/> (which adds the game's first screens). Setting the environment variable
    /// <c>VECTORENGINE_NO_SPLASH=1</c> skips it, for quick test runs.
    /// </summary>
    public static void Start(ScreenManager manager, Action first, SplashOptions options = null, PlayerIndex? player = null)
    {
        if (Environment.GetEnvironmentVariable("VECTORENGINE_NO_SPLASH") == "1")
            first();
        else
            manager.AddScreen(new SplashScreen(options, first), player);
    }

    float FadeStart => SplashAnimation.Seconds + options.HoldSeconds;
    float End => FadeStart + options.FadeOutSeconds;

    /// <summary>Seconds into the splash.</summary>
    public float Elapsed => elapsed;

    public override void Initialize() => animation = new SplashAnimation(options);

    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

        if (finished)
            return;

        elapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (elapsed >= End)
            Finish();
    }

    public override void HandleInput(GameTime gameTime, InputState input)
    {
        if (options.Skippable && !finished && elapsed < FadeStart && AnyNewInput(input))
            Skip();
    }

    /// <summary>Jumps to the fade-out.</summary>
    public void Skip() => elapsed = Math.Max(elapsed, FadeStart);

    void Finish()
    {
        finished = true;
        ExitScreen();
        onFinished?.Invoke();
    }

    /// <summary>Whether anything was pressed this frame that was not pressed the last: a key, a mouse button, a touch or a pad button.</summary>
    public static bool AnyNewInput(InputState input)
    {
        for (int i = 0; i < input.CurrentKeyboardStates.Length; i++)
            if (input.CurrentKeyboardStates[i].GetPressedKeyCount() > input.LastKeyboardStates[i].GetPressedKeyCount())
                return true;

        if (input.IsLeftMouseButtonClicked() || input.IsRightMoustButtonClicked())
            return true;

        foreach (var touch in input.CurrentTouchState)
            if (touch.State == Microsoft.Xna.Framework.Input.Touch.TouchLocationState.Pressed)
                return true;

        for (int i = 0; i < input.CurrentGamePadStates.Length; i++)
        {
            var now = input.CurrentGamePadStates[i];
            var before = input.LastGamePadStates[i];
            foreach (Buttons button in Enum.GetValues<Buttons>())
                if (now.IsButtonDown(button) && before.IsButtonUp(button))
                    return true;
        }
        return false;
    }

    public override void Draw(GameTime gameTime)
    {
        if (animation == null)
            return;

        // the splash owns the whole screen
        ScreenManager.FadeBackBufferToBlack(1f);

        animation.Build(Math.Min(elapsed, SplashAnimation.Seconds), lines);
        float fade = elapsed > FadeStart ? Math.Max(0f, 1f - (elapsed - FadeStart) / Math.Max(options.FadeOutSeconds, 0.001f)) : 1f;

        // fit the 1280 x 720 design canvas into the game's virtual screen, centred
        float scale = Math.Min(Display.BaseWidth / VectorEngineLogo.Width, Display.BaseHeight / VectorEngineLogo.Height);
        Vector2 origin = new(Display.BaseWidth / 2f - VectorEngineLogo.Width * scale / 2f,
                             Display.BaseHeight / 2f - VectorEngineLogo.Height * scale / 2f);

        bool bloom = options.UseBloom && ScreenManager.BloomComponent != null;
        if (bloom)
            ScreenManager.BloomComponent.BeginDraw();

        var batch = ScreenManager.LineBatch;
        batch.Begin(ScreenManager.GlobalTransformation);
        foreach (var line in lines)
        {
            float alpha = line.Alpha * fade;
            if (alpha <= 0.004f)
                continue;

            Vector2 a = origin + line.A * scale, b = origin + line.B * scale;
            Vector3 colour = line.Color;
            // a wide faint halo, a mid glow, the stroke itself and a hot white core
            Stroke(batch, a, b, colour, line.Width * 5f * scale, alpha * .10f);
            Stroke(batch, a, b, colour, line.Width * 2.2f * scale, alpha * .32f);
            Stroke(batch, a, b, Vector3.Lerp(colour, new Vector3(255f), .15f), line.Width * scale, alpha * .95f);
            Stroke(batch, a, b, Vector3.Lerp(colour, new Vector3(255f), .85f), line.Width * .4f * scale, alpha * .8f);
        }
        batch.End();

        if (bloom)
            ScreenManager.BloomComponent.Draw(gameTime);
    }

    /// <summary>A line of the given pixel width, as a rectangle with square ends a half-width long.</summary>
    void Stroke(LineBatch batch, Vector2 a, Vector2 b, Vector3 colour, float width, float alpha)
    {
        Vector2 along = b - a;
        if (along.LengthSquared() < 0.0001f)
            return;
        along.Normalize();
        Vector2 half = new Vector2(-along.Y, along.X) * Math.Max(width, 1f) / 2f;
        Vector2 cap = along * Math.Max(width, 1f) / 2f;
        Vector2 p0 = a - cap + half, p1 = a - cap - half, p2 = b + cap + half, p3 = b + cap - half;

        var c = new Color(colour.X / 255f, colour.Y / 255f, colour.Z / 255f, MathHelper.Clamp(alpha, 0f, 1f));
        quad[0] = new VertexPositionColor(new Vector3(p0, 0f), c);
        quad[1] = new VertexPositionColor(new Vector3(p1, 0f), c);
        quad[2] = new VertexPositionColor(new Vector3(p2, 0f), c);
        quad[3] = new VertexPositionColor(new Vector3(p2, 0f), c);
        quad[4] = new VertexPositionColor(new Vector3(p1, 0f), c);
        quad[5] = new VertexPositionColor(new Vector3(p3, 0f), c);
        batch.DrawTriangleVertices(quad, 6);
    }
}
