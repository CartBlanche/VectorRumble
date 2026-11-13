using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VectorEngine.Rendering
{
    /// <summary>
    /// The game is drawn at one fixed size, whatever the screen is: a world of the same shape on every
    /// device (which a network match needs), and menus and touch controls laid out once. The picture is
    /// then scaled to fit the real screen, with bars at the sides or the top and bottom to keep its shape.
    /// 
    /// Positions on the real screen (a touch, the mouse) are turned back into the game's own units with
    /// <see cref="ToVirtual"/>.
    /// </summary>
    public static class Display
    {
        /// <summary>The default drawing size: 720p.</summary>
        public const int DefaultWidth = 1280;
        public const int DefaultHeight = 720;

        /// <summary>The size the game is drawn at (720p unless <see cref="Configure"/> says otherwise).</summary>
        public static int BaseWidth { get; private set; } = DefaultWidth;
        public static int BaseHeight { get; private set; } = DefaultHeight;

        /// <summary>
        /// Sets the size the game is drawn at (for example 1920x1080). Call once at start-up, before anything reads
        /// <see cref="BaseWidth"/>, and before the first <see cref="Update"/>.
        /// </summary>
        public static void Configure(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new System.ArgumentOutOfRangeException(width <= 0 ? nameof(width) : nameof(height), "The drawing size must be positive.");
            BaseWidth = width;
            BaseHeight = height;
            Scale = 1f;
            Offset = Vector2.Zero;
            Destination = new Rectangle(0, 0, width, height);
        }

        /// <summary>The size the game is drawn at, as a vector.</summary>
        public static Vector2 BaseSize => new Vector2(BaseWidth, BaseHeight);

        /// <summary>The area the game is drawn in, which screens use in place of the device's viewport.</summary>
        public static Viewport Viewport => new Viewport(0, 0, BaseWidth, BaseHeight);

        /// <summary>How much the picture is scaled to fit the real screen.</summary>
        public static float Scale { get; private set; } = 1f;

        /// <summary>Where the picture's top left is on the real screen.</summary>
        public static Vector2 Offset { get; private set; } = Vector2.Zero;

        /// <summary>The part of the real screen the picture fills.</summary>
        public static Rectangle Destination { get; private set; } = new Rectangle(0, 0, DefaultWidth, DefaultHeight);

        /// <summary>
        /// Works out how the picture fits a screen of the given size. Call whenever the screen's size may
        /// have changed (the screen manager does it every frame).
        /// </summary>
        public static void Update(int screenWidth, int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
                return;

            Fit(screenWidth, screenHeight, out float scale, out Vector2 offset);
            Scale = scale;
            Offset = offset;
            Destination = new Rectangle((int)System.Math.Round(offset.X), (int)System.Math.Round(offset.Y),
                (int)System.Math.Round(BaseWidth * scale), (int)System.Math.Round(BaseHeight * scale));
        }

        /// <summary>
        /// The scale and offset that fit the picture inside a screen, keeping its shape and centring it.
        /// </summary>
        public static void Fit(int screenWidth, int screenHeight, out float scale, out Vector2 offset)
        {
            float scaleX = screenWidth / (float)BaseWidth;
            float scaleY = screenHeight / (float)BaseHeight;

            // the smaller of the two is the one that fits; the other axis gets the bars
            scale = System.Math.Min(scaleX, scaleY);
            offset = new Vector2((screenWidth - BaseWidth * scale) / 2f, (screenHeight - BaseHeight * scale) / 2f);
        }

        /// <summary>
        /// A position on the real screen in the game's own units. A position over the bars comes out
        /// outside the picture, so it hits nothing.
        /// </summary>
        public static Vector2 ToVirtual(Vector2 screenPosition)
        {
            return (screenPosition - Offset) / Scale;
        }
    }
}
