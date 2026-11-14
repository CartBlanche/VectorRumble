using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>Progress: trophies and the leaderboard. Both are placeholders for now.</summary>
    class ProgressMenuScreen : FrameMenuScreen
    {
        public ProgressMenuScreen()
        {
            Heading = Strings.Progress;
            Rows.Add(Soon(Strings.Trophies, Strings.Desc_Trophies, FrameIcon.Trophy));
            Rows.Add(Soon(Strings.Leaderboard, Strings.Desc_Leaderboard, FrameIcon.Board));
        }

        static FrameRow Soon(string label, string description, FrameIcon icon)
        {
            var row = MainMenuScreen.Entry(label, description, icon, () => { });
            row.Meta = Strings.Soon;
            return row;
        }
    }
}
