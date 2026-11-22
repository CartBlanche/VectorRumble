using System;

namespace VectorRumble
{
    /// <summary>
    /// Settings, Sky: which parts of the sky behind the arena are drawn. Each change applies at once and is saved.
    /// </summary>
    class SkyScreen : FrameMenuScreen
    {
        public SkyScreen()
        {
            Layout = FrameLayout.Rows;
            ShowPanel = false;
            BuildRows();
        }

        // the rows hold the words of the language they were built in, so a change of language builds them again
        void BuildRows()
        {
            int selected = SelectedIndex;
            Heading = Strings.Label_Sky;
            Rows.Clear();
            Rows.Add(Toggle(Strings.Sky_Stars, s => s.SkyStars, (s, on) => s.SkyStars = on));
            Rows.Add(Toggle(Strings.Sky_Constellations, s => s.SkyConstellations, (s, on) => s.SkyConstellations = on));
            Rows.Add(Toggle(Strings.Sky_Names, s => s.SkyNames, (s, on) => s.SkyNames = on));
            Rows.Add(Toggle(Strings.Sky_Planets, s => s.SkyPlanets, (s, on) => s.SkyPlanets = on));
            Rows.Add(Toggle(Strings.Sky_Nebula, s => s.SkyNebula, (s, on) => s.SkyNebula = on));
            Rows.Add(Toggle(Strings.Sky_NebulaGlow, s => s.SkyGlow, (s, on) => s.SkyGlow = on));
            Rows.Add(Toggle(Strings.Sky_Comet, s => s.SkyComet, (s, on) => s.SkyComet = on));
            SelectedIndex = selected;
        }

        protected override void RebuildForLanguage() => BuildRows();

        static FrameRow Toggle(string label, Func<VectorRumbleSettings, bool> get, Action<VectorRumbleSettings, bool> set)
        {
            var row = new FrameRow(label, FrameRowKind.Toggle) { Value = () => get(UserSettings.Current) ? Strings.Value_On : Strings.Value_Off };
            row.Adjusted += d =>
            {
                set(UserSettings.Current, !get(UserSettings.Current));
                UserSettings.Save();
            };
            return row;
        }
    }
}
