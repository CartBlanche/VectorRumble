using Microsoft.Xna.Framework;
using VectorEngine.Audio;
using VectorEngine.Screens;

namespace VectorEngine;

/// <summary>
/// How a <see cref="VectorEngineGame"/> sets up its window and graphics device.
/// </summary>
public class VectorEngineGameOptions
{
    /// <summary>
    /// The size of the virtual screen the game is laid out in (see <see cref="Rendering.Display"/>). Zero keeps what
    /// <see cref="Rendering.Display"/> already has, which is 1280x720 unless the game has configured it.
    /// </summary>
    public int VirtualWidth { get; set; }

    /// <summary>See <see cref="VirtualWidth"/>.</summary>
    public int VirtualHeight { get; set; }

    /// <summary>The desktop window's size. Zero uses the virtual screen's size.</summary>
    public int WindowWidth { get; set; }

    /// <summary>See <see cref="WindowWidth"/>.</summary>
    public int WindowHeight { get; set; }

    /// <summary>Show the system mouse pointer on desktop. False when the game draws its own cursor.</summary>
    public bool MouseVisible { get; set; } = true;

    /// <summary>Let the player resize the desktop window.</summary>
    public bool AllowUserResizing { get; set; }

    /// <summary>Wait for the display's vertical retrace.</summary>
    public bool VSync { get; set; } = true;

    /// <summary>Go full screen on a phone or tablet (the game is drawn at a fixed size and scaled to the device's screen).</summary>
    public bool FullScreenOnMobile { get; set; } = true;

    /// <summary>The orientations a phone or tablet may use. The default is landscape; null leaves the platform's default.</summary>
    public DisplayOrientation? MobileOrientations { get; set; } = DisplayOrientation.LandscapeLeft;

    /// <summary>
    /// Whether going full screen on desktop changes the display's mode (true) or just fills the screen with a borderless window
    /// (false, the default).
    /// </summary>
    public bool HardwareModeSwitch { get; set; }

    /// <summary>The orientations the desktop window reports. Null leaves MonoGame's default.</summary>
    public DisplayOrientation? DesktopOrientations { get; set; }

    /// <summary>
    /// How the screen manager is set up. Null skips creating one (the game makes its own and adds it to
    /// <see cref="Game.Components"/>).
    /// </summary>
    public ScreenManagerOptions ScreenManager { get; set; } = new ScreenManagerOptions();

    /// <summary>
    /// The name of the game's audio project: the engine loads <c>Content/Audio/{name}.xgs</c>, <c>.xwb</c> and <c>.xsb</c>.
    /// Null or empty skips creating an <see cref="Audio.AudioManager"/>.
    /// </summary>
    public string AudioProject { get; set; }

    /// <summary>Leave the window and device settings to the platform's defaults (for debugging device problems).</summary>
    public bool UseDefaultDeviceSettings { get; set; }
}

/// <summary>
/// The shared start of a Vector Engine game: the graphics device manager (registered as a service so everything shares
/// one), the content root and the window. A game derives from it, passes its options, and adds its own screen manager,
/// audio and settings in its constructor.
/// </summary>
public abstract class VectorEngineGame : Game
{
    /// <summary>The graphics device manager; it is also registered in <see cref="Game.Services"/>.</summary>
    protected GraphicsDeviceManager Graphics { get; }

    /// <summary>The screen manager (null when <see cref="VectorEngineGameOptions.ScreenManager"/> was null).</summary>
    protected Screens.ScreenManager ScreenManager { get; }

    /// <summary>The audio manager (null when no <see cref="VectorEngineGameOptions.AudioProject"/> was given); also in <see cref="Game.Services"/>.</summary>
    protected AudioManager Audio { get; }

    protected VectorEngineGame(VectorEngineGameOptions options = null)
    {
        options ??= new VectorEngineGameOptions();

        if (options.VirtualWidth > 0 && options.VirtualHeight > 0)
            Rendering.Display.Configure(options.VirtualWidth, options.VirtualHeight);

        Graphics = new GraphicsDeviceManager(this);
        // Added as a service so one instance is shared and any component can grab it
        Services.AddService(typeof(GraphicsDeviceManager), Graphics);
        Content.RootDirectory = "Content";

        if (!options.UseDefaultDeviceSettings)
        {
            if (Platform.IsMobile)
            {
                // the device's own screen: the game is drawn at a fixed size and scaled to it (see Display)
                Graphics.IsFullScreen = options.FullScreenOnMobile;
                if (options.MobileOrientations.HasValue)
                    Graphics.SupportedOrientations = options.MobileOrientations.Value;
            }
            else
            {
                Graphics.PreferredBackBufferWidth = options.WindowWidth > 0 ? options.WindowWidth : Rendering.Display.BaseWidth;
                Graphics.PreferredBackBufferHeight = options.WindowHeight > 0 ? options.WindowHeight : Rendering.Display.BaseHeight;
                Graphics.HardwareModeSwitch = options.HardwareModeSwitch;
                Graphics.IsFullScreen = false;
                IsMouseVisible = options.MouseVisible;
                if (options.DesktopOrientations.HasValue)
                    Graphics.SupportedOrientations = options.DesktopOrientations.Value;
            }
        }

        Graphics.SynchronizeWithVerticalRetrace = options.VSync;
        Window.AllowUserResizing = options.AllowUserResizing;

        if (options.ScreenManager != null)
        {
            ScreenManager = new Screens.ScreenManager(this, options.ScreenManager);
            Components.Add(ScreenManager);
        }

        if (!string.IsNullOrEmpty(options.AudioProject))
        {
            Audio = new AudioManager(this,
                $"Content/Audio/{options.AudioProject}.xgs",
                $"Content/Audio/{options.AudioProject}.xwb",
                $"Content/Audio/{options.AudioProject}.xsb");
            Services.AddService(typeof(AudioManager), Audio);
        }
    }

    /// <summary>Updates the audio first, then the components (the screens).</summary>
    protected override void Update(GameTime gameTime)
    {
        UpdateAudio(gameTime);
        base.Update(gameTime);
    }

    /// <summary>Updates the audio manager. A game can override it to measure or wrap the audio update.</summary>
    protected virtual void UpdateAudio(GameTime gameTime) => Audio?.Update(gameTime);
}
