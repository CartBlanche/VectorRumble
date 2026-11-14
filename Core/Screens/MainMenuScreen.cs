using System;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>
    /// The main menu: Play, Progress, Settings, About and Exit (Exit is left out on iOS).
    /// </summary>
    class MainMenuScreen : FrameMenuScreen
    {
        public MainMenuScreen()
        {
            ShowBack = false;
            BuildRows();
        }

        // the rows hold the words of the language they were built in, so a change of language builds them again
        void BuildRows()
        {
            int selected = SelectedIndex;
            Rows.Clear();

            var play = Entry(Strings.Play_Game, Strings.Desc_Play, FrameIcon.Duel, PlayEntrySelected);
            play.PanelArt = MenuDuel.Draw;
            Rows.Add(play);
            Rows.Add(Entry(Strings.Progress, Strings.Desc_Progress, FrameIcon.Trophy, () => ScreenManager.AddScreen(new ProgressMenuScreen())));
            Rows.Add(Entry(Strings.Settings, Strings.Desc_Settings, FrameIcon.Gear, () => ScreenManager.AddScreen(new SettingsScreen())));
            Rows.Add(Entry(Strings.About, Strings.Desc_About, FrameIcon.Info, () => ScreenManager.AddScreen(new AboutMenuScreen())));
            if (!OperatingSystem.IsIOS())
            {
                Rows.Add(Entry(Strings.Exit, Strings.Desc_Exit, FrameIcon.Exit, OnCancel));
            }

            SelectedIndex = selected;
        }

        protected override void RebuildForLanguage() => BuildRows();

        internal static FrameRow Entry(string label, string description, FrameIcon icon, Action selected)
        {
            var row = new FrameRow(label) { Description = description, Icon = icon };
            row.Selected += selected;
            return row;
        }

        public override void LoadContent()
        {
            base.Initialize();
            World.ArenaManager.LoadContent(ScreenManager.Game.Content);
            base.LoadContent();
        }

        void PlayEntrySelected()
        {
            ScreenManager.AddScreen(new PlayMenuScreen());
        }

        protected override void DrawBodyText(FrameCanvas c)
        {
            c.Text(c.Display, Strings.Tagline, new Vector2(96, 160), FrameScreen.Theme.Text, 20f);
        }

        /// <summary>When the user cancels the main menu, ask if they want to exit.</summary>
        protected override void OnCancel()
        {
            if (OperatingSystem.IsIOS())
            {
                return;
            }

            MessageBoxScreen messageBox = new MessageBoxScreen(Strings.Confirm_ExitTitle, "", Strings.Exit, Strings.Confirm_Stay);
            messageBox.Accepted += (s, e) => ScreenManager.Game.Exit();
            ScreenManager.AddScreen(messageBox);
        }
    }
}
