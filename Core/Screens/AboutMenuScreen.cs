using System;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>
    /// About: the version, who made the game and what it is built with. The links open in the browser.
    /// </summary>
    class AboutMenuScreen : FrameMenuScreen
    {
        const string MonoGameUri = "https://monogame.net";
        const string FontUri = "https://payhip.com/b/xNA0";
        const string GitHubUri = "https://github.com/CartBlanche/VectorRumble";
        const string EngineUri = "https://github.com/CartBlanche/VectorEngine";

        public AboutMenuScreen()
        {
            Heading = Strings.About;
            Layout = FrameLayout.Rows;
            Rows.Add(Link(Strings.Built_With_MonoGame, MonoGameUri));
            Rows.Add(Link(string.Format(Strings.Font_By, "GNU Unifont Glyphs", "Unifoundry.com"), FontUri));
            Rows.Add(Link(string.Format(Strings.Github_Repo, "CartBlanche/VectorRumble"), GitHubUri));
            Rows.Add(Link("VectorEngine", EngineUri));
        }

        static FrameRow Link(string label, string uri)
        {
            var row = new FrameRow(label, FrameRowKind.Action) { Chevron = true };
            row.Selected += () => ProcessStart(uri);
            return row;
        }

        protected override void DrawPanel(FrameCanvas c, Rectangle area, FrameRow selected)
        {
            // the engine's mark, at the foot of the panel
            c.Icon(FrameIcon.Mark, new Vector2(area.X + 70, area.Bottom - 56), 36f, new Color(255, 79, 208), Time, 2.4f);
        }

        protected override void DrawPanelText(FrameCanvas c, Rectangle area, FrameRow selected)
        {
            var style = ScreenManager.MenuStyle.Frame;
            float x = area.X + 36, y = area.Y + 28;
            string version = typeof(Arena).Assembly.GetName().Version.ToString(3);
            c.Text(c.Display, "VECTOR RUMBLE " + version, new Vector2(x, y), style.Hot, 30f);
            c.Text(c.Medium, Strings.About_Tagline, new Vector2(x, y + 44), style.Text, 23f);
            c.Text(c.Small, Strings.About_Code.ToUpperInvariant(), new Vector2(x, y + 104), style.Dim, 14f);
            c.Text(c.Medium, "Dean Ellis  ·  Dominique Louis", new Vector2(x, y + 126), style.Text, 25f);
            c.Text(c.Small, Strings.About_Thanks.ToUpperInvariant(), new Vector2(x, y + 184), style.Dim, 14f);
            c.Text(c.Medium, "Christian Thode  ·  Yuan-Zzzz", new Vector2(x, y + 206), style.Text, 25f);
            c.Text(c.Small, Strings.About_Powered.ToUpperInvariant(), new Vector2(area.X + 112, area.Bottom - 80), style.Dim, 13f);
            c.Text(c.Display, "VectorEngine", new Vector2(area.X + 112, area.Bottom - 62), new Color(53, 242, 255), 28f);
        }

        public static async void ProcessStart(string uri)
        {
            await Task.Run(() => { try { System.Diagnostics.Process.Start(uri); } catch { } });
        }
    }
}
