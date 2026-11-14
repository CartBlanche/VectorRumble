using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using VectorEngine.Rendering;
using VectorEngine.Screens;

namespace VectorRumble
{
    /// <summary>
    /// F12 (or Print Screen) on the desktop saves a picture of the game to the Pictures folder, in a folder of its own, and
    /// says so for a moment. The picture is the game as drawn, without the letterbox bars the window may have around it and
    /// without the mouse pointer, so that players can post it as it is.
    /// </summary>
    sealed class Screenshots
    {
        const string FolderName = "VectorRumble";
        const float ToastSeconds = 2.5f;

        KeyboardState lastKeys;
        bool requested;
        string toast;
        float toastLeft;

        /// <summary>Screenshots are for the desktop: a phone has its own way, and no keyboard.</summary>
        public static bool Available => !VectorEngine.Platform.IsMobile;

        /// <summary>Where the pictures go: a folder of the game's own in the player's Pictures (their home folder if the system has none).</summary>
        public static string Folder()
        {
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrEmpty(pictures))
            {
                pictures = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            return Path.Combine(pictures, FolderName);
        }

        /// <summary>A name that sorts by time and has not been used: "VectorRumble-2026-11-14-221530.png", then "-2", "-3" for several in a second.</summary>
        public static string FileName(DateTime now, Func<string, bool> exists)
        {
            string stem = "VectorRumble-" + now.ToString("yyyy-MM-dd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string name = stem + ".png";
            for (int n = 2; exists(name); n++)
            {
                name = stem + "-" + n + ".png";
            }

            return name;
        }

        /// <summary>The part of the window the game is drawn in, kept inside the window.</summary>
        public static Rectangle GameArea(Rectangle destination, int windowWidth, int windowHeight)
        {
            return Rectangle.Intersect(destination, new Rectangle(0, 0, windowWidth, windowHeight));
        }

        /// <summary>A back buffer can come back with no alpha; a picture must not be see-through.</summary>
        public static void MakeOpaque(Color[] pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].A = 255;
            }
        }

        /// <summary>Call each update: notices the key.</summary>
        public void Update()
        {
            if (!Available)
            {
                return;
            }

            KeyboardState keys = Keyboard.GetState();
            bool pressed = (keys.IsKeyDown(Keys.F12) && !lastKeys.IsKeyDown(Keys.F12))
                        || (keys.IsKeyDown(Keys.PrintScreen) && !lastKeys.IsKeyDown(Keys.PrintScreen));
            lastKeys = keys;
            if (pressed)
            {
                requested = true;
            }
        }

        /// <summary>Takes a picture at the end of this frame (what F12 does).</summary>
        public void Request() => requested = Available;

        /// <summary>Call before the frame is drawn: the pointer is left out of a picture.</summary>
        public void BeginFrame()
        {
            if (requested)
            {
                ScreenManager.HideCursor = true;
            }
        }

        /// <summary>Call after the frame is drawn: takes the picture if one was asked for.</summary>
        public void EndFrame(GraphicsDevice device)
        {
            if (!requested)
            {
                return;
            }

            requested = false;
            ScreenManager.HideCursor = false;
            toastLeft = ToastSeconds;

            try
            {
                string folder = Folder();
                Directory.CreateDirectory(folder);
                string name = FileName(DateTime.Now, n => File.Exists(Path.Combine(folder, n)));
                Save(device, Path.Combine(folder, name));
                toast = Strings.Screenshot_Saved + "\n" + name;
            }
            catch (Exception)
            {
                // a read-only folder, a full disk: say so, and carry on playing
                toast = Strings.Screenshot_Failed;
            }
        }

        static void Save(GraphicsDevice device, string path)
        {
            PresentationParameters pp = device.PresentationParameters;
            Rectangle area = GameArea(Display.Destination, pp.BackBufferWidth, pp.BackBufferHeight);
            var pixels = new Color[area.Width * area.Height];
            device.GetBackBufferData(area, pixels, 0, pixels.Length);
            MakeOpaque(pixels);

            using (var texture = new Texture2D(device, area.Width, area.Height))
            using (var stream = File.Create(path))
            {
                texture.SetData(pixels);
                texture.SaveAsPng(stream, area.Width, area.Height);
            }
        }

        /// <summary>Drawn over the game (see ScreenManager.DrawingOverlays): the note that the picture was saved.</summary>
        public void DrawToast(ScreenManager screens, GameTime gameTime)
        {
            if (toast == null || toastLeft <= 0f)
            {
                return;
            }

            toastLeft -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            float fade = Math.Min(1f, toastLeft / 0.5f);

            SpriteFont font = screens.Font;
            const float scale = 0.55f;
            string[] lines = toast.Split('\n');
            float y = Display.BaseHeight - 28f - lines.Length * font.LineSpacing * scale;

            SpriteBatch batch = screens.SpriteBatch;
            batch.Begin();
            foreach (string line in lines)
            {
                Vector2 size = font.MeasureString(line) * scale;
                var at = new Vector2((Display.BaseWidth - size.X) / 2f, y);
                batch.DrawString(font, line, at + new Vector2(2, 2), Color.Black * (0.7f * fade), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                batch.DrawString(font, line, at, Color.White * fade, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                y += font.LineSpacing * scale;
            }
            batch.End();
        }
    }
}
