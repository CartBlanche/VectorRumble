using System;
using System.Linq;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>
    /// The last step before a match: which ship, in which colour, in which arena. The ship turns in its hangar and the
    /// arena is drawn from its own walls, so there are no pictures to ship. The Next link reads Start.
    /// </summary>
    class ShipArenaScreen : FrameMenuScreen
    {
        // what was chosen last time is what is offered next time
        static int shipIndex;
        static int colourIndex;

        public ShipArenaScreen()
        {
            Heading = Strings.Ship_And_Arena;
            Layout = FrameLayout.Rows;
            ShowPanel = false;

            var ship = new FrameRow(Strings.Label_Ship, FrameRowKind.Choice) { Value = () => CurrentShip.Name };
            ship.Adjusted += d => shipIndex = Wrap(shipIndex + d, World.ShipManager.Ships.Length);
            Rows.Add(ship);

            var colour = new FrameRow(Strings.Label_Colour, FrameRowKind.Choice)
            {
                Value = () => ShipPalette.Name(colourIndex),
                Adornment = (canvas, at) => canvas.Polygon(CurrentShip.Shape.Points, at, MathF.PI / 2f, 11f / Reach(CurrentShip), CurrentColour, 2f),
            };
            colour.Adjusted += d => colourIndex = Wrap(colourIndex + d, ShipPalette.Colours.Length);
            Rows.Add(colour);

            var arena = new FrameRow(Strings.Label_Arena, FrameRowKind.Choice) { Value = () => MatchRules.Current.Arena };
            arena.Adjusted += ChangeArena;
            Rows.Add(arena);

            Next = new FrameNext
            {
                Label = () => Strings.Hint_Start,
                Step = 2,
                Steps = 2,
                Action = Start,
            };
        }

        // the hangar and the arena take the right-hand side
        protected override float RowsWidth => 600f;

        public override void LoadContent()
        {
            // the ships a player can choose between are loaded fresh for each game
            World.ShipManager.LoadContent(ScreenManager.Game.Content);
            shipIndex = Math.Min(shipIndex, World.ShipManager.Ships.Length - 1);
            if (World.ArenaManager.Arenas.All(a => a.Name != MatchRules.Current.Arena))
            {
                MatchRules.Current.Arena = Strings.Arena_Random;
            }

            base.LoadContent();
        }

        static int Wrap(int value, int count) => ((value % count) + count) % count;

        static Ship CurrentShip => World.ShipManager.Ships[shipIndex];

        static Color CurrentColour => ShipPalette.Colours[colourIndex];

        static float Reach(Ship ship) => Math.Max(1f, ship.Shape.Points.Max(p => p.Length()));

        static void ChangeArena(int direction)
        {
            string[] arenas = World.ArenaManager.Arenas.Select(a => a.Name).ToArray();
            int index = Array.IndexOf(arenas, MatchRules.Current.Arena);
            MatchRules.Current.Arena = arenas[Wrap(index + direction, arenas.Length)];
        }

        /// <summary>
        /// Puts the player's ship, alone, into the match and loads the game. A single player game has one pilot on
        /// this machine, flying with whatever they have to hand (the keyboard, a gamepad or the touch controls).
        /// </summary>
        void Start()
        {
            ShipManager manager = World.ShipManager;
            foreach (Ship ship in manager.Ships)
            {
                ship.PlayerIndex = string.Empty;
                ship.Selected = false;
            }

            Ship pilot = CurrentShip;
            pilot.PlayerIndex = "One";
            pilot.SetColor(CurrentColour);
            pilot.Controls = ShipControls.ForLocalPlayer();
            pilot.Selected = true;
            manager.UpdateSelectedList();

            // a new match picks its arena afresh (and a "Random" arena picks again)
            World.SelectedArena = null;
            World.Match = null;

            LoadingScreen.Load(ScreenManager, LoadGameplayScreen, true);
        }

        void LoadGameplayScreen(object sender, EventArgs e)
        {
            ScreenManager.AddScreen(new GameplayScreen { ScreenManager = this.ScreenManager });
        }

        #region The panel: hangar above, arena below

        static void Box(FrameCanvas c, float x, float y, float w, float h)
        {
            var frame = FrameCanvas.Chamfer(x, y, w, h, 26);
            c.Fill(frame, new Color(8, 8, 26), 0.5f);
            c.Poly(frame, true, FrameScreen.Theme.Panel, 1.8f, .7f);
        }

        protected override void DrawBody(FrameCanvas c)
        {
            const float x = 740, w = 440;
            Box(c, x, 196, w, 190);
            Box(c, x, 404, w, 156);

            // the ship, turning slowly in its colour
            Ship ship = CurrentShip;
            c.Polygon(ship.Shape.Points, new Vector2(x + w / 2, 291), Time * 0.7f, 52f / Reach(ship), CurrentColour, 3f);

            // the arena, from its own walls
            Arena arena = PreviewPainter.FindArena(MatchRules.Current.Arena);
            if (arena?.Walls != null && arena.Walls.Length >= 2)
            {
                const float ix = x + 90, iy = 420, iw = w - 180, ih = 124;
                for (int i = 0; i + 1 < arena.Walls.Length; i += 2)
                {
                    Vector2 a = new Vector2(ix + arena.Walls[i].X * iw, iy + arena.Walls[i].Y * ih);
                    Vector2 b = new Vector2(ix + arena.Walls[i + 1].X * iw, iy + arena.Walls[i + 1].Y * ih);
                    c.Neon(a, b, arena.Color, 2f);
                }
            }
        }

        protected override void DrawBodyText(FrameCanvas c)
        {
            var theme = FrameScreen.Theme;
            const float x = 740, w = 440;
            Arena arena = PreviewPainter.FindArena(MatchRules.Current.Arena);
            if (arena?.Walls == null || arena.Walls.Length < 2)
            {
                // a "Random" arena has no walls to show
                c.Text(c.Display, "?", new Vector2(x + w / 2, 458), theme.Text, 48f, FrameAlign.Centre);
            }
        }

        #endregion
    }
}
