using System;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>
    /// The first step of a single player game: the rules. What to play to, and under "More rules" the asteroids and
    /// whether the sticks can fire in any direction. The next step picks the ship and the arena.
    /// </summary>
    class SinglePlayerSetupScreen : FrameMenuScreen
    {
        static readonly string[] asteroidDensities =
        {
            Strings.Asteroid_Density_None,
            Strings.Asteroid_Density_Low,
            Strings.Asteroid_Density_Medium,
            Strings.Asteroid_Density_High,
        };

        const int ScoreLimitStep = 5;

        bool moreRules;

        public SinglePlayerSetupScreen()
        {
            Heading = Strings.Single_Player;
            Layout = FrameLayout.Rows;

            var firstTo = new FrameRow(Strings.Label_FirstTo, FrameRowKind.Choice)
            {
                Value = () => string.Format(Strings.Value_Kills, MatchRules.Current.ScoreLimit),
            };
            firstTo.Adjusted += ChangeScoreLimit;
            Rows.Add(firstTo);

            var more = new FrameRow(Strings.Label_MoreRules, FrameRowKind.Expander) { Expanded = () => moreRules };
            more.Selected += () => moreRules = !moreRules;
            Rows.Add(more);

            var asteroids = new FrameRow(Strings.Label_Asteroids, FrameRowKind.Choice)
            {
                Value = () => asteroidDensities[(int)MatchRules.Current.AsteroidDensity],
                Hidden = () => !moreRules,
            };
            asteroids.Adjusted += ChangeAsteroidDensity;
            Rows.Add(asteroids);

            var freeAim = new FrameRow(Strings.Label_FreeAim, FrameRowKind.Toggle)
            {
                Value = () => MatchRules.Current.ControllersCanShootInAllDirections ? Strings.Value_On : Strings.Value_Off,
                Hidden = () => !moreRules,
            };
            freeAim.Adjusted += d => MatchRules.Current.ControllersCanShootInAllDirections = !MatchRules.Current.ControllersCanShootInAllDirections;
            Rows.Add(freeAim);

            Next = new FrameNext
            {
                Step = 1,
                Steps = 2,
                Action = () => ScreenManager.AddScreen(new ShipArenaScreen()),
            };
        }

        static void ChangeScoreLimit(int direction)
        {
            int limit = MatchRules.Current.ScoreLimit + direction * ScoreLimitStep;
            if (limit > MatchRules.MaxScoreLimit)
                limit = MatchRules.MinScoreLimit;
            else if (limit < MatchRules.MinScoreLimit)
                limit = MatchRules.MaxScoreLimit;
            MatchRules.Current.ScoreLimit = limit;
        }

        static void ChangeAsteroidDensity(int direction)
        {
            int count = asteroidDensities.Length;
            MatchRules.Current.AsteroidDensity = (AsteroidDensity)(((int)MatchRules.Current.AsteroidDensity + direction + count) % count);
        }

        protected override void DrawPanel(FrameCanvas c, Rectangle area, FrameRow selected)
        {
            c.Icon(FrameIcon.Duel, new Vector2(area.Center.X, area.Y + 134), 92f, FrameScreen.Theme.Hot, Time);
        }

        protected override void DrawPanelText(FrameCanvas c, Rectangle area, FrameRow selected)
        {
            var theme = FrameScreen.Theme;
            c.Text(c.Display, string.Format(Strings.Score_Limit, MatchRules.Current.ScoreLimit), new Vector2(area.Center.X, area.Y + 258), theme.Text, 26f, FrameAlign.Centre);
            c.Text(c.Large, Strings.Setup_Next_Hint, new Vector2(area.Center.X, area.Bottom - 56), theme.Dim, 18f, FrameAlign.Centre);
        }
    }
}
