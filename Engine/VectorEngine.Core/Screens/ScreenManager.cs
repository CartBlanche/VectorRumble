using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using VectorEngine.Diagnostics;
using VectorEngine.Input;
using VectorEngine.Rendering;

namespace VectorEngine.Screens;

/// <summary>
/// How a game wants its <see cref="ScreenManager"/> set up.
/// </summary>
public class ScreenManagerOptions
{
    /// <summary>Loads the font every screen shares. The default is <c>Fonts/retroLarge</c>.</summary>
    public Func<ContentManager, SpriteFont> LoadFont { get; set; } = content => content.Load<SpriteFont>("Fonts/retroLarge");

    /// <summary>The content path of the plain white texture every screen shares.</summary>
    public string BlankTexturePath { get; set; } = "Textures/blank";

    /// <summary>
    /// False (the default): screens draw straight to the back buffer, and <see cref="ScreenManager.GlobalTransformation"/>
    /// scales and centres the virtual screen onto the window.
    /// True: the game draws at its own fixed size into a render target (with the bloom drawn into it), and that target is
    /// then scaled to fit the window as a whole; <see cref="ScreenManager.GlobalTransformation"/> is the identity.
    /// </summary>
    public bool RenderToVirtualTarget { get; set; }

    /// <summary>Size of the shared line batch's vertex arrays.</summary>
    public int LineBatchVertexCount { get; set; } = LineBatch.DefaultMaxVertexCount;

    /// <summary>Draw an on-screen cursor for the mouse (and the pad, when the pad pointer is on), instead of the system pointer.</summary>
    public bool DrawCursor { get; set; }

    /// <summary>How the engine's menus, message boxes and loading screen look and sound.</summary>
    public MenuStyle MenuStyle { get; set; } = new MenuStyle();

    /// <summary>Show the frame time / GC / draw call readout (F3 toggles it); only present in Debug and PerfOverlay builds.</summary>
    public bool PerfOverlay { get; set; }
}

/// <summary>
/// The screen manager is a component which manages one or more <see cref="GameScreen"/> instances: it keeps the stack of
/// screens, calls their Update and Draw at the right times, routes input to the topmost active one, and owns what they
/// share (sprite batch, line batch, font, blank texture, bloom, input).
/// </summary>
/// <remarks>Based on a class in the Game State Management sample.</remarks>
public class ScreenManager : DrawableGameComponent, IRenderContext
{
    readonly List<GameScreen> screens = new();
    readonly List<GameScreen> screensToUpdate = new();
    readonly ScreenManagerOptions options;

    /// <summary>How the engine's menus, message boxes and loading screen look and sound.</summary>
    public MenuStyle MenuStyle => options.MenuStyle;

    readonly InputState inputState = new() { GamePadFilter = VirtualGamePad.Apply };

    SpriteFont font;
    Texture2D blankTexture;
    SpriteBatch spriteBatch;
    LineBatch lineBatch;
    RenderTarget2D gameTarget;
    GameScreen[] screensSnapshot;
    Matrix globalTransformation = Matrix.Identity;
    int backbufferWidth, backbufferHeight;

    bool isInitialized;
    bool traceEnabled;

    // frame time, GC and draw call readout, when the game asks for it (an empty stub outside Debug and PerfOverlay builds; see PerfOverlay)
    readonly PerfOverlay perfOverlay;

#if DEBUG
    /// <summary>Debug only (a determinism check): treat the game as focused even when its window is not the active one.</summary>
    public static bool DebugIgnoreFocus;
#endif

    /// <summary>Set by a screenshot runner (any build): treat the game as focused even when its window is not the active one.</summary>
    public static bool IgnoreFocus;

    /// <summary>Set by a screenshot runner: draw no on-screen cursor, so a picture shows only the game.</summary>
    public static bool HideCursor;

    /// <summary>
    /// Raised once the manager's own content is loaded (the shared font, textures and batches exist), for the game to
    /// finish setting up whatever it keeps beside the manager.
    /// </summary>
    public event Action<ScreenManager> ContentLoaded;

    /// <summary>Raised at the start of every update, before input is read.</summary>
    public event Action<ScreenManager, GameTime> Updating;

    /// <summary>Raised during the draw, after the screens and before the cursor: where a game draws what sits above every screen.</summary>
    public event Action<ScreenManager, GameTime> DrawingOverlays;

    public ScreenManager(Game game, ScreenManagerOptions options = null)
        : base(game)
    {
        this.options = options ?? new ScreenManagerOptions();
        if (this.options.PerfOverlay)
            perfOverlay = new PerfOverlay();

        // we must set EnabledGestures before we can query for them, but
        // we don't assume the game wants to read them.
        TouchPanel.EnabledGestures = GestureType.None;
    }

    /// <summary>What the player's devices did this update, and what triggers each action.</summary>
    public InputState Input => inputState;

    /// <summary>What triggers each action (a Controls screen draws it).</summary>
    public ActionBindings InputBindings => inputState.Bindings;

    /// <summary>The size of the window's back buffer, as of the last time the presentation was fitted.</summary>
    public int BackbufferWidth => backbufferWidth;

    /// <summary>The size of the window's back buffer, as of the last time the presentation was fitted.</summary>
    public int BackbufferHeight => backbufferHeight;

    /// <summary>The size the game is drawn at (see <see cref="Display"/>).</summary>
    public Vector2 BaseScreenSize => Display.BaseSize;

    /// <summary>
    /// Maps the virtual screen onto what screens draw to: the window (scale and centre) when drawing straight to the back
    /// buffer, or the identity when drawing into the fixed-size target.
    /// </summary>
    public Matrix GlobalTransformation => globalTransformation;

    /// <summary>
    /// A default SpriteBatch shared by all the screens. This saves
    /// each screen having to bother creating their own local instance.
    /// </summary>
    public SpriteBatch SpriteBatch => spriteBatch;

    /// <summary>A default LineBatch shared by all the screens.</summary>
    public LineBatch LineBatch => lineBatch;

    /// <summary>A default font shared by all the screens.</summary>
    public SpriteFont Font => font;

    /// <summary>A small plain white texture that all the screens can use, for tinted rectangles and particles.</summary>
    public Texture2D BlankTexture => blankTexture;

    /// <summary>The bloom post-process the screens draw their glow through.</summary>
    public BloomComponent BloomComponent { get; private set; }

    /// <summary>
    /// If true, the manager prints out a list of all the screens each time it is updated. This can be useful for making
    /// sure everything is being added and removed at the right times.
    /// </summary>
    public bool TraceEnabled
    {
        get { return traceEnabled; }
        set { traceEnabled = value; }
    }

    /// <summary>
    /// Initializes the screen manager component.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        Accelerometer.Initialize();

        isInitialized = true;
    }

    /// <summary>
    /// Load your graphics content.
    /// </summary>
    protected override void LoadContent()
    {
        ContentManager content = Game.Content;

        spriteBatch = new SpriteBatch(GraphicsDevice);
        lineBatch = new LineBatch(GraphicsDevice, this, options.LineBatchVertexCount);

        font = options.LoadFont(content);
        blankTexture = content.Load<Texture2D>(options.BlankTexturePath);

        perfOverlay?.LoadContent(content);

        if (options.RenderToVirtualTarget)
        {
            // The game is drawn at a fixed size into this, then scaled to the screen (see Display).
            // Drawing switches away to the bloom's targets and back, so it has to keep what it holds.
            gameTarget = new RenderTarget2D(GraphicsDevice, Display.BaseWidth, Display.BaseHeight, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            lineBatch.SetProjection(Matrix.CreateOrthographicOffCenter(0f, Display.BaseWidth, Display.BaseHeight, 0f, 0f, 1f));
        }

        BloomComponent = new BloomComponent(Game, spriteBatch);
        if (options.RenderToVirtualTarget)
            BloomComponent.RenderSize = new Point(Display.BaseWidth, Display.BaseHeight);
        Game.Components.Add(BloomComponent);
        // do not automatically draw the bloom component
        BloomComponent.Visible = false;

        FitPresentation();

        ContentLoaded?.Invoke(this);

        // Tell each of the screens to load their content.
        foreach (GameScreen screen in screens)
        {
            screen.LoadContent();
        }
    }

    /// <summary>
    /// Unload your graphics content.
    /// </summary>
    protected override void UnloadContent()
    {
        gameTarget?.Dispose();
        gameTarget = null;

        // Tell each of the screens to unload their content.
        foreach (GameScreen screen in screens)
        {
            screen.UnloadContent();
        }
    }

    /// <summary>
    /// Works out how the virtual screen fits the window as it is now. Done at the start and whenever the window's size
    /// changes (a turn, a resize, full screen).
    /// </summary>
    void FitPresentation()
    {
        PresentationParameters pp = GraphicsDevice.PresentationParameters;
        backbufferWidth = pp.BackBufferWidth;
        backbufferHeight = pp.BackBufferHeight;
        Display.Update(backbufferWidth, backbufferHeight);

        if (options.RenderToVirtualTarget)
        {
            globalTransformation = Matrix.Identity;
        }
        else
        {
            globalTransformation = Matrix.CreateScale(Display.Scale) *
                                   Matrix.CreateTranslation(Display.Offset.X, Display.Offset.Y, 0f);
            // The line batch draws in back-buffer pixels (the global transformation takes virtual units there), so its projection follows the size
            lineBatch?.SetProjection(Matrix.CreateOrthographicOffCenter(0f, backbufferWidth, backbufferHeight, 0f, 0f, 1f));
        }
    }

    /// <summary>
    /// Allows each screen to run logic.
    /// </summary>
    public override void Update(GameTime gameTime)
    {
        perfOverlay?.BeginUpdate();

        // Fit the picture to the window as it is now (it changes with a turn, a resize or a full screen).
        PresentationParameters pp = GraphicsDevice.PresentationParameters;
        if (pp.BackBufferWidth != backbufferWidth || pp.BackBufferHeight != backbufferHeight)
            FitPresentation();

        Updating?.Invoke(this, gameTime);

        // Pointer positions read in the game's own units: window pixels back through the picture's offset and scale.
        inputState.CursorBounds = BaseScreenSize;
        inputState.UpdateInputTransformation(Matrix.CreateTranslation(-Display.Offset.X, -Display.Offset.Y, 0f) * Matrix.CreateScale(1f / Display.Scale));

        // Read the keyboard and gamepad.
        var inputUpdateProbe = AllocProbe.Begin();
        inputState.Update(gameTime, GraphicsDevice.Viewport);
        AllocProbe.End(typeof(ScreenManager), "InputUpdate", inputUpdateProbe);
        perfOverlay?.HandleInput(inputState);

        // Make a copy of the master screen list, to avoid confusion if
        // the process of updating one screen adds or removes others.
        screensToUpdate.Clear();

        foreach (GameScreen screen in screens)
            screensToUpdate.Add(screen);

#if DEBUG
        bool otherScreenHasFocus = !Game.IsActive && !DebugIgnoreFocus && !IgnoreFocus;
#else
        bool otherScreenHasFocus = !Game.IsActive && !IgnoreFocus;
#endif
        bool coveredByOtherScreen = false;

        // Loop as long as there are screens waiting to be updated.
        while (screensToUpdate.Count > 0)
        {
            // Pop the topmost screen off the waiting list.
            GameScreen screen = screensToUpdate[screensToUpdate.Count - 1];

            screensToUpdate.RemoveAt(screensToUpdate.Count - 1);

            // Update the screen.
            var probe = AllocProbe.Begin();
            screen.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
            AllocProbe.End(screen.GetType(), "Update", probe);

            if (screen.ScreenState == ScreenState.TransitionOn ||
                screen.ScreenState == ScreenState.Active)
            {
                // If this is the first active screen we came across,
                // give it a chance to handle input.
                if (!otherScreenHasFocus)
                {
                    inputState.PadPointerEnabled = screen.WantsPadPointer;
                    inputState.CursorFrozen = screen.FreezesCursor;
                    var inputProbe = AllocProbe.Begin();
                    screen.HandleInput(gameTime, inputState);
                    AllocProbe.End(screen.GetType(), "Input", inputProbe);

                    otherScreenHasFocus = true;
                }

                // If this is an active non-popup, inform any subsequent
                // screens that they are covered by it.
                if (!screen.IsPopup)
                    coveredByOtherScreen = true;
            }
        }

        // Print debug trace?
        if (traceEnabled)
            TraceScreens();

        perfOverlay?.EndUpdate();
    }

    /// <summary>
    /// Prints a list of all the screens, for debugging.
    /// </summary>
    void TraceScreens()
    {
        List<string> screenNames = new List<string>();

        foreach (GameScreen screen in screens)
            screenNames.Add(screen.GetType().Name);

        Debug.WriteLine(string.Join(", ", screenNames.ToArray()));
    }

    /// <summary>
    /// Tells each screen to draw itself.
    /// </summary>
    public override void Draw(GameTime gameTime)
    {
        perfOverlay?.BeginDraw();

        if (options.RenderToVirtualTarget)
        {
            // draw the game at its own size...
            GraphicsDevice.SetRenderTarget(gameTarget);
            GraphicsDevice.Clear(Color.Black);
            BloomComponent.OutputTarget = gameTarget;
        }

        foreach (GameScreen screen in screens)
        {
            if (screen.ScreenState == ScreenState.Hidden)
                continue;

            var drawProbe = AllocProbe.Begin();
            screen.Draw(gameTime);
            AllocProbe.End(screen.GetType(), "Draw", drawProbe);
        }

        var overlayProbe = AllocProbe.Begin();
        DrawingOverlays?.Invoke(this, gameTime);
        DrawCursor();
        AllocProbe.End(typeof(ScreenManager), "Overlays", overlayProbe);

        perfOverlay?.EndDraw(GraphicsDevice);
        perfOverlay?.Draw(spriteBatch, blankTexture);

        if (options.RenderToVirtualTarget)
        {
            // ...then scale it to fit the screen, with black bars where it does not reach
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Color.Black);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp, null, null);
            spriteBatch.Draw(gameTarget, Display.Destination, Color.White);
            spriteBatch.End();
        }
    }

    /// <summary>
    /// The on-screen cursor (the system pointer is hidden): a ring with a dot, outlined dark so it reads on every stage. Drawn for
    /// the mouse on desktop and for the pad while the pad pointer is on; never for a touch.
    /// </summary>
    void DrawCursor()
    {
        if (!options.DrawCursor || !inputState.CursorVisible || HideCursor)
            return;

        var centre = inputState.CursorPosition;
        const float radius = 7f;
        const float tick = 4f;
        const int segments = 16;

        lineBatch.Begin(GlobalTransformation);
        // The dark outline first (the same shape nudged a pixel each way), then the white shape on top
        foreach (var (offset, colour) in new[]
        {
            (new Vector2(-1, 0), Color.Black), (new Vector2(1, 0), Color.Black),
            (new Vector2(0, -1), Color.Black), (new Vector2(0, 1), Color.Black),
            (Vector2.Zero, Color.White),
        })
        {
            var c = centre + offset;
            for (int i = 0; i < segments; i++)
            {
                float a0 = MathHelper.TwoPi * i / segments, a1 = MathHelper.TwoPi * (i + 1) / segments;
                lineBatch.DrawLineList(c + new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0)) * radius,
                    c + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * radius, colour);
            }
            lineBatch.DrawLineList(c + new Vector2(-radius - tick, 0), c + new Vector2(-radius, 0), colour);
            lineBatch.DrawLineList(c + new Vector2(radius, 0), c + new Vector2(radius + tick, 0), colour);
            lineBatch.DrawLineList(c + new Vector2(0, -radius - tick), c + new Vector2(0, -radius), colour);
            lineBatch.DrawLineList(c + new Vector2(0, radius), c + new Vector2(0, radius + tick), colour);
            lineBatch.DrawLineList(c + new Vector2(-1, 0), c + new Vector2(1, 0), colour);
        }
        lineBatch.End();
    }

    /// <summary>
    /// Adds a new screen to the screen manager.
    /// </summary>
    public void AddScreen(GameScreen screen, PlayerIndex? playerIndex = null)
    {
        screen.ControllingPlayer = playerIndex;
        screen.ScreenManager = this;
        screen.IsExiting = false;

        // If we have a graphics device, tell the screen to load content.
        if (isInitialized)
        {
            // once only: a screen a game already initialised by hand must not set up its world twice
            if (!screen.initializedByManager)
            {
                screen.initializedByManager = true;
                screen.Initialize();
            }
            screen.LoadContent();
        }

        screens.Add(screen);
        screensSnapshot = null;

        // update the TouchPanel to respond to gestures this screen is interested in
        TouchPanel.EnabledGestures = screen.EnabledGestures;
    }

    /// <summary>
    /// Removes a screen from the screen manager. You should normally
    /// use GameScreen.ExitScreen instead of calling this directly, so
    /// the screen can gradually transition off rather than just being
    /// instantly removed.
    /// </summary>
    public void RemoveScreen(GameScreen screen)
    {
        // If we have a graphics device, tell the screen to unload content.
        if (isInitialized)
        {
            screen.UnloadContent();
        }

        screens.Remove(screen);
        screensSnapshot = null;
        screensToUpdate.Remove(screen);

        // if there is a screen still in the manager, update TouchPanel
        // to respond to gestures that screen is interested in.
        if (screens.Count > 0)
        {
            TouchPanel.EnabledGestures = screens[screens.Count - 1].EnabledGestures;
        }
    }

    /// <summary>
    /// Expose an array holding all the screens. We return a copy rather
    /// than the real master list, because screens should only ever be added
    /// or removed using the AddScreen and RemoveScreen methods.
    /// </summary>
    public GameScreen[] GetScreens()
    {
        // Screens may ask several times a frame; the list only changes in AddScreen and RemoveScreen, so keep the copy until then
        return screensSnapshot ??= screens.ToArray();
    }

    /// <summary>
    /// Tells every screen on the stack that keeps translated text (<see cref="ILanguageAware"/>) to read it again. Call it right
    /// after the culture changes so the screens behind the Settings screen show the new language at once.
    /// </summary>
    public void RefreshScreensAfterLanguageChange()
    {
        foreach (var screen in screens.ToArray())
            if (screen is ILanguageAware aware)
                aware.OnLanguageChanged();
    }

    /// <summary>
    /// Helper draws a translucent black fullscreen sprite, used for fading
    /// screens in and out, and for darkening the background behind popups.
    /// </summary>
    public void FadeBackBufferToBlack(float alpha)
    {
        Viewport viewport = GraphicsDevice.Viewport;

        spriteBatch.Begin();

        spriteBatch.Draw(blankTexture,
                         new Rectangle(0, 0, viewport.Width, viewport.Height),
                         Color.Black * alpha);

        spriteBatch.End();
    }

    /// <summary>
    /// Draw a filled rectangle of the given size and color.
    /// </summary>
    public void DrawRectangle(Rectangle rectangle, Color color)
    {
        SpriteBatch.Begin();
        SpriteBatch.Draw(blankTexture, rectangle, color);
        SpriteBatch.End();
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                spriteBatch?.Dispose();
                lineBatch?.Dispose();
                gameTarget?.Dispose();
                BloomComponent?.Dispose();
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
}
