using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using System;
using System.Collections.Generic;

namespace VectorEngine.Input;

/// <summary>
/// Helper for reading input from keyboard, gamepad, and touch input. This class 
/// tracks both the current and previous state of the input devices, and implements 
/// query methods for high level input actions such as "move up through the menu"
/// or "pause the game".
/// </summary>
public class InputState
{
    public const int MaxInputs = 4;

    public AccelerometerState CurrentAccelerometerState;
    public readonly GamePadState[] CurrentGamePadStates;
    public readonly KeyboardState[] CurrentKeyboardStates;
    public MouseState CurrentMouseState;
    public TouchCollection CurrentTouchState;

    public AccelerometerState LastAccelerometerState;
    public readonly GamePadState[] LastGamePadStates;
    public readonly KeyboardState[] LastKeyboardStates;
    public MouseState LastMouseState;
    public TouchCollection LastTouchState;

    public readonly bool[] GamePadWasConnected;

    public readonly List<GestureSample> Gestures = new List<GestureSample>();

    /// <summary>
    /// Cursor move speed in pixels per second of the 800x480 frame at the default setting (Settings, Cursor Speed 100%)
    /// </summary>
    public const float BaseCursorSpeed = 200.0f;

    /// <summary>The player's Cursor Speed setting as a multiplier of <see cref="BaseCursorSpeed"/> (1 = 100%).</summary>
    public float CursorSpeedScale { get; set; } = 1f;

    private Vector2 currentCursorLocation;
    /// <summary>
    /// Current location of our Cursor
    /// </summary>
    public Vector2 CurrentCursorLocation => currentCursorLocation;

    private bool isMouseWheelScrolledDown;
    /// <summary>
    /// Has the user scrolled the mouse wheel down?
    /// </summary>
    public bool IsMouseWheelScrolledDown => isMouseWheelScrolledDown;

    private bool isMouseWheelScrolledUp;
    private Matrix inputTransformation;

    /// <summary>
    /// Has the user scrolled the mouse wheel up?
    /// </summary>
    public bool IsMouseWheelScrolledUp => isMouseWheelScrolledUp;

    /// <summary>Pixels (in the virtual frame) one wheel notch scrolls.</summary>
    private const float WheelNotchPixels = 40f;

    /// <summary>
    /// How far the view should move this frame, in virtual-frame pixels (positive X is right, positive Y is down), from whatever the device
    /// offers: a two-finger drag on a touch screen (the content follows the fingers), a trackpad's two-finger scroll or a mouse wheel
    /// (vertical), a horizontal wheel or a trackpad's sideways scroll, and Shift with the wheel (sideways). Zero when nothing scrolls.
    /// A one-finger drag is not part of it: that is the pointer, and each screen already reads it.
    /// </summary>
    public Vector2 ScrollDelta { get; private set; }

    /// <summary>True while two or more fingers are on the screen: the pointer's finger then belongs to a scroll, not to a tap or a drag.</summary>
    public bool TwoFingerActive { get; private set; }

    /// <summary>
    /// Constructs a new input state.
    /// </summary>
    public InputState()
    {
        CurrentKeyboardStates = new KeyboardState[MaxInputs];
        CurrentGamePadStates = new GamePadState[MaxInputs];

        LastKeyboardStates = new KeyboardState[MaxInputs];
        LastGamePadStates = new GamePadState[MaxInputs];

        GamePadWasConnected = new bool[MaxInputs];

        if (Platform.IsMobile)
        {
            TouchPanel.EnabledGestures = GestureType.Tap;
        }
        else if (Platform.IsDesktop)
        {

        }
        else
        {
            // For now, we'll throw an exception if we don't know the platform
            throw new PlatformNotSupportedException();
        }
    }

    /// <summary>
    /// Reads the latest state of all the inputs.
    /// </summary>
    public void Update(GameTime gameTime, Viewport viewport)
    {
        CurrentAccelerometerState = Accelerometer.IsInitialized ? Accelerometer.GetState() : default;

        for (int i = 0; i < MaxInputs; i++)
        {
            LastKeyboardStates[i] = CurrentKeyboardStates[i];
            LastGamePadStates[i] = CurrentGamePadStates[i];

            CurrentKeyboardStates[i] = Keyboard.GetState();
            CurrentGamePadStates[i] = GamePad.GetState((PlayerIndex)i);
            if (GamePadFilter != null)
                CurrentGamePadStates[i] = GamePadFilter((PlayerIndex)i, CurrentGamePadStates[i]);

            // Keep track of whether a gamepad has ever been
            // connected, so we can detect if it is unplugged.
            if (CurrentGamePadStates[i].IsConnected)
            {
                GamePadWasConnected[i] = true;
            }
        }

        LastMouseState = CurrentMouseState;
        CurrentMouseState = Mouse.GetState();

        int touchCount = 0;
        LastTouchState = CurrentTouchState;
        CurrentTouchState = TouchPanel.GetState();

        Gestures.Clear();
        while (TouchPanel.IsGestureAvailable)
        {
            Gestures.Add(TouchPanel.ReadGesture());
        }

        foreach (TouchLocation location in CurrentTouchState)
        {
            switch (location.State)
            {
                case TouchLocationState.Pressed:
                    touchCount++;

                    currentCursorLocation = TransformCursorLocation(location.Position);
                    break;
                case TouchLocationState.Moved:
                    break;
                case TouchLocationState.Released:
                    break;
            }
        }

        UpdatePointer();

        if (IsLeftMouseButtonClicked())
        {
            currentCursorLocation = TransformCursorLocation(new Vector2(CurrentMouseState.X, CurrentMouseState.Y));
            
            touchCount = 1;
        }

        if (IsMiddleMouseButtonClicked())
        {
            touchCount = 2;
        }

        if (IsRightMoustButtonClicked())
        {
            touchCount = 3;
        }

        isMouseWheelScrolledUp = false;
        isMouseWheelScrolledDown = false;

        if (CurrentMouseState.ScrollWheelValue != LastMouseState.ScrollWheelValue)
        {
            int scrollWheelDelta = CurrentMouseState.ScrollWheelValue - LastMouseState.ScrollWheelValue;

            // Handle the scroll wheel event based on the delta
            if (scrollWheelDelta > 0)
            {
                // Mouse wheel scrolled up
                isMouseWheelScrolledUp = true;
            }
            else if (scrollWheelDelta < 0)
            {
                // Mouse wheel scrolled down
                isMouseWheelScrolledDown = true;
            }
        }

        UpdateScroll();

        // Update the cursor location by listening for left thumbstick input on
        // the 1st GamePad and direction key input on the Keyboard, making sure to
        // keep the cursor inside the screen boundary
        // The left trigger slows the cursor down for precise placing
        float elapsedTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float cursorSpeed = CursorFrozen ? 0f : BaseCursorSpeed * CursorSpeedScale * (CurrentGamePadStates[0].Triggers.Left > 0.3f ? cursorSlowFactor : 1f);
        currentCursorLocation.X += CurrentGamePadStates[0].ThumbSticks.Left.X * elapsedTime * cursorSpeed;
        currentCursorLocation.Y -= CurrentGamePadStates[0].ThumbSticks.Left.Y * elapsedTime * cursorSpeed;

        if (CurrentKeyboardStates[0].IsKeyDown(Keys.Up))
        {
            currentCursorLocation.Y -= elapsedTime * cursorSpeed;
        }
        if (CurrentKeyboardStates[0].IsKeyDown(Keys.Down))
        {
            currentCursorLocation.Y += elapsedTime * cursorSpeed;
        }
        if (CurrentKeyboardStates[0].IsKeyDown(Keys.Left))
        {
            currentCursorLocation.X -= elapsedTime * cursorSpeed;
        }
        if (CurrentKeyboardStates[0].IsKeyDown(Keys.Right))
        {
            currentCursorLocation.X += elapsedTime * cursorSpeed;
        }

        // Inside the virtual frame the screens draw in (the window's pixel size until the screen manager says what that is)
        var bounds = CursorBounds != Vector2.Zero ? CursorBounds : new Vector2(viewport.Width, viewport.Height);
        if (!cursorPlaced && CursorBounds != Vector2.Zero)
        {
            // The first arrow key or stick push starts from the middle of the screen, not the corner
            cursorPlaced = true;
            currentCursorLocation = bounds / 2f;
        }
        currentCursorLocation.X = MathHelper.Clamp(currentCursorLocation.X, 0f, bounds.X);
        currentCursorLocation.Y = MathHelper.Clamp(currentCursorLocation.Y, 0f, bounds.Y);

        UpdatePadPointer(viewport);
    }

    private const float cursorSlowFactor = 0.4f;

    /// <summary>
    /// Set by the screen taking input: while true the stick and arrow keys do not move the cursor (the waypoint menu uses them to
    /// step through its segments). A mouse or touch still sets the cursor where it points.
    /// </summary>
    public bool CursorFrozen { get; set; }

    /// <summary>The size of the virtual frame (800x480), set by the screen manager, that the cursor is kept inside.</summary>
    public Vector2 CursorBounds { get; set; }

    /// <summary>
    /// Set by the screen that is taking input each frame: true on the planning screen and its waypoint menu, where the pad
    /// cursor and the Click action stand in for the mouse. While it is on, the Click buttons trigger nothing else.
    /// </summary>
    public bool PadPointerEnabled { get; set; }

    /// <summary>True when the pad (stick or Click) was the last pointing device used, false when the mouse or a touch was.</summary>
    public bool PadActive { get; private set; }

    /// <summary>True when the on-screen cursor should be drawn: the mouse inside the window (desktop) or the pad while the pad pointer is on.</summary>
    public bool CursorVisible { get; private set; }

    /// <summary>Where the on-screen cursor is, in the virtual frame.</summary>
    public Vector2 CursorPosition { get; private set; }

    private bool ArrowKeyDown()
    {
        var keys = CurrentKeyboardStates[0];
        return keys.IsKeyDown(Keys.Up) || keys.IsKeyDown(Keys.Down) || keys.IsKeyDown(Keys.Left) || keys.IsKeyDown(Keys.Right);
    }

    private bool cursorPlaced;
    private bool padClickWasDown;
    private bool mouseSeen;

    /// <summary>
    /// Works out which pointing device is in use, lets the pad stand in for the pointer when a screen asks for it (the stick moves
    /// the cursor, Click presses and releases at it), and decides whether the cursor is drawn.
    /// </summary>
    private void UpdatePadPointer(Viewport viewport)
    {
        bool clickDown = IsHeld(GameAction.Click);
        bool clickPressed = clickDown && !padClickWasDown;
        bool clickReleased = !clickDown && padClickWasDown;
        padClickWasDown = clickDown;

        bool mouseMoved = CurrentMouseState.X != LastMouseState.X || CurrentMouseState.Y != LastMouseState.Y;
        bool mouseButtons = CurrentMouseState.LeftButton == ButtonState.Pressed || IsLeftMouseButtonClicked();
        if (mouseMoved)
            mouseSeen = true;

        if (mouseMoved || mouseButtons || CurrentTouchState.Count > 0)
            PadActive = false;
        else if (CurrentGamePadStates[0].ThumbSticks.Left.LengthSquared() > 0.01f || clickPressed || ArrowKeyDown())
            PadActive = true;

        if (PadPointerEnabled && PadActive)
        {
            PointerPosition = currentCursorLocation;
            PointerDown = clickDown;
            PointerPressed = clickPressed;
            PointerReleased = clickReleased;
            if (clickPressed)
                PointerPressPosition = currentCursorLocation;
            if (clickReleased)
                PointerReleasePosition = currentCursorLocation;
        }

        CursorPosition = PadActive ? currentCursorLocation : PointerPosition;
        bool mouseInside = Platform.IsDesktop && mouseSeen &&
            CurrentMouseState.X >= 0 && CurrentMouseState.Y >= 0 && CurrentMouseState.X < viewport.Width && CurrentMouseState.Y < viewport.Height;
        CursorVisible = PadActive ? PadPointerEnabled : mouseInside;
    }

    /// <summary>
    /// Where the primary pointer is now, in the 800x480 virtual frame: the primary touch while one is down, otherwise the mouse
    /// (every frame, so hovering works). Not moved by the gamepad stick or arrow keys; see <see cref="CurrentCursorLocation"/>.
    /// </summary>
    public Vector2 PointerPosition { get; private set; }

    /// <summary>True while the primary touch or the left mouse button is held.</summary>
    public bool PointerDown { get; private set; }

    /// <summary>True only on the frame the primary touch lands or the left mouse button goes down.</summary>
    public bool PointerPressed { get; private set; }

    /// <summary>True only on the frame the primary touch lifts or the left mouse button comes up (a click or tap).</summary>
    public bool PointerReleased { get; private set; }

    /// <summary>Where the pointer was when <see cref="PointerPressed"/> last fired (virtual frame).</summary>
    public Vector2 PointerPressPosition { get; private set; }

    /// <summary>Where the pointer was when <see cref="PointerReleased"/> last fired (virtual frame).</summary>
    public Vector2 PointerReleasePosition { get; private set; }

    private int primaryTouchId = -1;

    /// <summary>Works out <see cref="ScrollDelta"/> and <see cref="TwoFingerActive"/> for this frame.</summary>
    private void UpdateScroll()
    {
        var delta = Vector2.Zero;

        // Two fingers: the average of how far each moved, the content following them (so the view moves the other way)
        int fingers = 0, moving = 0;
        var moved = Vector2.Zero;
        foreach (TouchLocation location in CurrentTouchState)
        {
            if (location.State is TouchLocationState.Pressed or TouchLocationState.Moved)
            {
                fingers++;
                if (location.State == TouchLocationState.Moved && location.TryGetPreviousLocation(out var previous))
                {
                    moved += TransformCursorLocation(location.Position) - TransformCursorLocation(previous.Position);
                    moving++;
                }
            }
        }
        TwoFingerActive = fingers >= 2;
        if (TwoFingerActive && moving > 0)
            delta -= moved / moving;

        // A mouse wheel or a trackpad: up is negative Y; a sideways scroll is positive X to the right. Shift turns the wheel sideways.
        float vertical = (CurrentMouseState.ScrollWheelValue - LastMouseState.ScrollWheelValue) / 120f * WheelNotchPixels;
        float horizontal = (CurrentMouseState.HorizontalScrollWheelValue - LastMouseState.HorizontalScrollWheelValue) / 120f * WheelNotchPixels;
        bool shift = CurrentKeyboardStates[0].IsKeyDown(Keys.LeftShift) || CurrentKeyboardStates[0].IsKeyDown(Keys.RightShift);
        if (shift)
        {
            horizontal -= vertical;
            vertical = 0f;
        }
        delta += new Vector2(horizontal, -vertical);

        ScrollDelta = delta;
    }

    /// <summary>
    /// Builds the unified pointer from the mouse and the touch panel. The primary touch is the first finger down, followed by
    /// its id so a second finger cannot take over; a touch wins over the mouse on the frames it is active.
    /// </summary>
    private void UpdatePointer()
    {
        bool touchActive = false, touchPressed = false, touchReleased = false;
        Vector2 touchPosition = Vector2.Zero;

        if (primaryTouchId >= 0)
        {
            bool found = false;
            foreach (TouchLocation location in CurrentTouchState)
            {
                if (location.Id != primaryTouchId)
                    continue;

                found = true;
                touchPosition = TransformCursorLocation(location.Position);
                if (location.State == TouchLocationState.Pressed || location.State == TouchLocationState.Moved)
                    touchActive = true;
                else
                    touchReleased = true;
                break;
            }

            // A finger that vanished without a Released sample still ends the press
            if (!found)
                touchReleased = true;
            if (touchReleased)
            {
                touchActive = false;
                if (!found)
                    touchPosition = PointerPosition;
                primaryTouchId = -1;
            }
        }
        else
        {
            foreach (TouchLocation location in CurrentTouchState)
            {
                if (location.State != TouchLocationState.Pressed)
                    continue;

                primaryTouchId = location.Id;
                touchActive = touchPressed = true;
                touchPosition = TransformCursorLocation(location.Position);
                break;
            }
        }

        bool mouseDown = CurrentMouseState.LeftButton == ButtonState.Pressed;
        bool mousePressed = mouseDown && LastMouseState.LeftButton == ButtonState.Released;
        bool mouseReleased = !mouseDown && LastMouseState.LeftButton == ButtonState.Pressed;
        Vector2 mousePosition = TransformCursorLocation(new Vector2(CurrentMouseState.X, CurrentMouseState.Y));

        bool touchEvent = touchActive || touchReleased;
        PointerPosition = touchEvent ? touchPosition : mousePosition;
        PointerDown = touchActive || mouseDown;
        PointerPressed = touchPressed || mousePressed;
        PointerReleased = touchReleased || mouseReleased;

        if (PointerPressed)
            PointerPressPosition = touchPressed ? touchPosition : mousePosition;
        if (PointerReleased)
            PointerReleasePosition = touchReleased ? touchPosition : mousePosition;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public bool IsLeftMouseButtonClicked()
    {
        return CurrentMouseState.LeftButton == ButtonState.Released && LastMouseState.LeftButton == ButtonState.Pressed;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public bool IsMiddleMouseButtonClicked()
    {
        return CurrentMouseState.MiddleButton == ButtonState.Released && LastMouseState.MiddleButton == ButtonState.Pressed;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public bool IsRightMoustButtonClicked()
    {
        return CurrentMouseState.RightButton == ButtonState.Released && LastMouseState.RightButton == ButtonState.Pressed;
    }

    /// <summary>
    /// Helper for checking if a key was newly pressed during this update. The
    /// controllingPlayer parameter specifies which player to read input for.
    /// If this is null, it will accept input from any player. When a keypress
    /// is detected, the output playerIndex reports which player pressed it.
    /// </summary>
    public bool IsNewKeyPress(Keys key, PlayerIndex? controllingPlayer,
                                        out PlayerIndex playerIndex)
    {
        if (controllingPlayer.HasValue)
        {
            // Read input from the specified player.
            playerIndex = controllingPlayer.Value;

            int i = (int)playerIndex;

            return (CurrentKeyboardStates[i].IsKeyDown(key) &&
                    LastKeyboardStates[i].IsKeyUp(key));
        }
        else
        {
            // Accept input from any player.
            return (IsNewKeyPress(key, PlayerIndex.One, out playerIndex) ||
                    IsNewKeyPress(key, PlayerIndex.Two, out playerIndex) ||
                    IsNewKeyPress(key, PlayerIndex.Three, out playerIndex) ||
                    IsNewKeyPress(key, PlayerIndex.Four, out playerIndex));
        }
    }


    /// <summary>
    /// Helper for checking if a button was newly pressed during this update.
    /// The controllingPlayer parameter specifies which player to read input for.
    /// If this is null, it will accept input from any player. When a button press
    /// is detected, the output playerIndex reports which player pressed it.
    /// </summary>
    public bool IsNewButtonPress(Buttons button, PlayerIndex? controllingPlayer,
                                                 out PlayerIndex playerIndex)
    {
        if (controllingPlayer.HasValue)
        {
            // Read input from the specified player.
            playerIndex = controllingPlayer.Value;

            int i = (int)playerIndex;

            return (CurrentGamePadStates[i].IsButtonDown(button) &&
                    LastGamePadStates[i].IsButtonUp(button));
        }
        else
        {
            // Accept input from any player.
            return (IsNewButtonPress(button, PlayerIndex.One, out playerIndex) ||
                    IsNewButtonPress(button, PlayerIndex.Two, out playerIndex) ||
                    IsNewButtonPress(button, PlayerIndex.Three, out playerIndex) ||
                    IsNewButtonPress(button, PlayerIndex.Four, out playerIndex));
        }
    }


    /// <summary>
    /// Lets the game adjust each player's gamepad state as it is read, for example to add an on-screen touch pad's
    /// sticks and buttons to it. Null (the default) leaves the state as the device reported it.
    /// </summary>
    public Func<PlayerIndex, GamePadState, GamePadState> GamePadFilter { get; set; }

    /// <summary>What triggers each <see cref="GameAction"/>: the built-in table, replaced by Content/UI/Inputs.xml once loaded.</summary>
    public readonly ActionBindings Bindings = new ActionBindings();

    /// <summary>
    /// True on the frame an action's binding starts: any of its keys or buttons newly down, or the wheel turned its way.
    /// The controllingPlayer parameter specifies which player to read input for; null accepts any player, and playerIndex
    /// reports which player triggered it.
    /// </summary>
    public bool IsPressed(ActionId action, PlayerIndex? controllingPlayer, out PlayerIndex playerIndex)
    {
        playerIndex = controllingPlayer ?? PlayerIndex.One;
        var binding = Bindings[action];

        foreach (var key in binding.Keys)
        {
            // Likewise a key bound to Click (Space) is the keyboard cursor's click
            if (action != GameAction.Click && PadPointerEnabled && Bindings[GameAction.Click].Keys.Contains(key))
                continue;
            if (IsNewKeyPress(key, controllingPlayer, out playerIndex))
                return true;
        }
        foreach (var button in binding.Buttons)
        {
            // With the pad pointer on, a button bound to Click is the pad's mouse button and does nothing else
            if (action != GameAction.Click && PadPointerEnabled && Bindings[GameAction.Click].Buttons.Contains(button))
                continue;
            if (IsNewButtonPress(button, controllingPlayer, out playerIndex))
                return true;
        }

        return binding.Wheel == WheelDirection.Up && IsMouseWheelScrolledUp ||
               binding.Wheel == WheelDirection.Down && IsMouseWheelScrolledDown;
    }

    /// <summary>True while any of the action's keys or buttons is held (the wheel has no held state).</summary>
    public bool IsHeld(ActionId action, PlayerIndex controllingPlayer = PlayerIndex.One)
    {
        int i = (int)controllingPlayer;
        var binding = Bindings[action];

        foreach (var key in binding.Keys)
            if (CurrentKeyboardStates[i].IsKeyDown(key))
                return true;
        foreach (var button in binding.Buttons)
            if (CurrentGamePadStates[i].IsButtonDown(button))
                return true;
        return false;
    }

    /// <summary>Checks whether any key or button bound to the action was newly pressed, by any player.</summary>
    public bool IsPressed(ActionId action) => IsPressed(action, null, out _);

    /// <summary>Checks for a "menu up" input action, from any player.</summary>
    public bool MenuUp => IsPressed(GameAction.Up);

    /// <summary>Checks for a "menu down" input action, from any player.</summary>
    public bool MenuDown => IsPressed(GameAction.Down);

    /// <summary>Checks for a "previous value" input action, from any player.</summary>
    public bool SelectPrevious => IsPressed(GameAction.Previous);

    /// <summary>Checks for a "next value" input action, from any player.</summary>
    public bool SelectNext => IsPressed(GameAction.Next);

    /// <summary>Checks for a "menu select" input action, from any player.</summary>
    public bool MenuSelect => IsPressed(GameAction.Select);

    /// <summary>Checks for a "menu cancel" input action, from any player.</summary>
    public bool MenuCancel => IsPressed(GameAction.Cancel);

    /// <summary>Checks for a "pause the game" input action, from any player.</summary>
    public bool PauseGame => IsPressed(GameAction.Pause);

    /// <summary>True if the key was newly pressed during this update, by any player.</summary>
    public bool IsNewKeyPress(Keys key) => IsNewKeyPress(key, null, out _);

    /// <summary>True if the key was newly pressed during this update by the given player.</summary>
    public bool IsNewKeyPress(Keys key, PlayerIndex playerIndex) => IsNewKeyPress(key, playerIndex, out _);

    /// <summary>True if the button was newly pressed during this update, by any player.</summary>
    public bool IsNewButtonPress(Buttons button) => IsNewButtonPress(button, null, out _);

    /// <summary>True if the button was newly pressed during this update by the given player.</summary>
    public bool IsNewButtonPress(Buttons button, PlayerIndex playerIndex) => IsNewButtonPress(button, playerIndex, out _);

    // The screens' names for the actions; the bindings behind them are in Bindings

    /// <summary>Checks for a "menu select" input action.</summary>
    public bool IsMenuSelect(PlayerIndex? controllingPlayer, out PlayerIndex playerIndex) =>
        IsPressed(GameAction.Select, controllingPlayer, out playerIndex);

    /// <summary>Checks for a "menu cancel" input action.</summary>
    public bool IsMenuCancel(PlayerIndex? controllingPlayer, out PlayerIndex playerIndex) =>
        IsPressed(GameAction.Cancel, controllingPlayer, out playerIndex);

    /// <summary>Checks for a "menu up" input action.</summary>
    public bool IsMenuUp(PlayerIndex? controllingPlayer) => IsPressed(GameAction.Up, controllingPlayer, out _);

    /// <summary>Checks for a "menu down" input action.</summary>
    public bool IsMenuDown(PlayerIndex? controllingPlayer) => IsPressed(GameAction.Down, controllingPlayer, out _);

    /// <summary>Checks for a "pause the game" input action.</summary>
    public bool IsPauseGame(PlayerIndex? controllingPlayer) => IsPressed(GameAction.Pause, controllingPlayer, out _);

    /// <summary>Checks if the player has selected next (right).</summary>
    public bool IsSelectNext(PlayerIndex? controllingPlayer) => IsPressed(GameAction.Next, controllingPlayer, out _);

    /// <summary>Checks if the player has selected previous (left).</summary>
    public bool IsSelectPrevious(PlayerIndex? controllingPlayer) => IsPressed(GameAction.Previous, controllingPlayer, out _);

    public void UpdateInputTransformation(Matrix inputTransformation)
    {
        this.inputTransformation = inputTransformation;
    }

    public Vector2 TransformCursorLocation(Vector2 mousePosition)
    {
        // Transform back to cursor location
        return Vector2.Transform(mousePosition, inputTransformation);
    }
}