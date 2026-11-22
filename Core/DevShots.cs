#if DEVSHOT
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using VectorEngine.Screens;

namespace VectorRumble
{
    /// <summary>
    /// A way to drive the game from a script and take pictures of particular screens without anyone at the keyboard: for the
    /// store page, for checking a change to a screen in each language, and for looking at what a match does. It is compiled in
    /// only when the game is built with the DEVSHOT symbol:
    ///
    ///     dotnet build Desktop -p:DevShots=true
    ///
    /// and does nothing until environment variables ask it to. A DEVSHOT build never writes the player's settings file, so a
    /// run in another language leaves the real settings as they were.
    ///
    ///   VR_OPEN=main|play|setup|pick|options|host|browse|about|progress   open that screen over the main menu
    ///   VR_AUTOSTART=1            on the "pick" screen, start the single player match after 8 seconds
    ///   VR_THEN=pause|over|overnet|confirm|exit|notice   after 14 seconds, open that screen over whatever is showing
    ///   VR_ACT=0,2                after VR_THEN, select these rows of the top screen, one every 4 seconds
    ///   VR_LANG=de                the language to run in (a culture name)
    ///   VR_LANGSTEP=1             after 8 seconds, step the Settings language by that much (what its arrows do)
    ///   VR_NETGO=<seconds>        on the waiting room: the host starts the match, anyone else says Ready
    ///   VR_NOSKIP=1               the splash cannot be skipped
    ///   VR_SHOT=<png> VR_SHOT_AT=<seconds>   save the game's picture after that long and quit (10 seconds if VR_SHOT_AT is not set)
    ///   VR_SPLASH=<seconds> VR_OUT=<png>     save the picture when the splash has run that long, and quit
    ///   VR_ALLAI=1                on the "pick" screen, the human ship is flown by an AI pilot too (a match with nobody at the keyboard that still looks busy)
    ///   VR_AI=<0..3>              how many AI rivals (MatchRules.AIPilots)
    ///   VR_DIEAT=<s>              every ship is destroyed s seconds in
    ///   VR_NAME=<name>            the player's name in the lobby and the match (instead of the computer's user name)
    ///   VR_WIN=<x>,<y>,<w>,<h>    put the window here, this size, in points (to tile several games on one screen)
    ///   VR_TOUR=<rows>            from VR_TOURSTART seconds (default 4), pick these menu rows in turn, as a person would: for example
    ///                             "Play Game,Multiplayer,Host a Game" (the highlight moves, a pause, then it is pressed); after the last,
    ///                             on the host's rules screen, start hosting VR_RULESHOLD seconds later (default 3)
    ///   VR_DEMOHOP=1              two AI pilots hop every 10 to 15 s (for the video)
    ///   VR_ENDAFTER=<s>           quit s seconds after the match starts (VR_AUTOSTART); prints "[DEVSHOT] match-start unix-ms N" when it does
    ///   VR_CLEANSETTINGS=1        use the default sky layers and volumes, whatever the settings file says (for the video)
    ///   VR_HOPAT=<s>              every ship hops from s seconds on, again every 2 s
    ///   VR_SKY=<n>                the sky behind the arena (0 to 4; random when not set)
    ///   VR_SEED=<n>               the AI pilots' random seed, so a run can be repeated
    ///   VR_PICK=<ship>,<colour>,<arena>   indexes into the pick screen's lists (ship and colour as listed there; arena in the arena list, 0 is the first, "Random" is last of the built in ones)
    ///   VR_BURST=<start>,<count>,<interval>  from <start> seconds, save <count> pictures <interval> seconds apart into VR_BURST_DIR (default ./burst), numbered, then quit
    ///   VR_LOCK=1                 frame-locked time: each drawn frame is exactly one 60 Hz tick of the game, however long saving pictures takes (use it with VR_BURST for smooth sequences; VR_BURST's times are then game seconds)
    ///   VR_FIREWORKS=0            run with the "Menu fireworks" setting off (the real settings file is not touched), for clean menu pictures
    ///   VR_LOG=1                  print the input the game sees, and the splash's state, to the console
    ///   VR_AIDEBUG=1              (not here: see AIPilot) draw each AI pilot's state and route
    /// </summary>
    sealed class DevShots
    {
        const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

        readonly ScreenManager screens;
        readonly GraphicsDevice device;
        readonly Action exit;

        double matchStartedAt, tourNext;
        int tourStep, tourPhase;
        double tourDoneAt = -1;
        bool hostingStarted, arenaToLobby;
        bool autoStarted, netGone, langStepped, thenDone, picked;
        int burstTaken;
        int actIndex;

        public DevShots(ScreenManager screens, GraphicsDevice device, Action exit)
        {
            this.screens = screens;
            this.device = device;
            this.exit = exit;
        }

        static string Env(string name) => Environment.GetEnvironmentVariable(name);

        static double Seconds(string name, double fallback) =>
            double.TryParse(Env(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : fallback;

        /// <summary>True unless VR_NOSKIP: the splash can be skipped by any input, which a script's mouse or keys might do.</summary>
        public static bool SplashSkippable => Env("VR_NOSKIP") == null;

        /// <summary>VR_LOCK: the game runs one 60 Hz tick per drawn frame, so a burst of pictures has none missing.</summary>
        public static bool FrameLock => Env("VR_LOCK") != null;

        static long frame;
        static GameTime tick = new GameTime();

        /// <summary>The next tick of the locked clock (called once per Update).</summary>
        public static GameTime NextTick()
        {
            frame++;
            tick = new GameTime(TimeSpan.FromTicks(frame * TimeSpan.TicksPerSecond / 60), TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60));
            return tick;
        }

        /// <summary>The tick the last Update ran, for the Draw that follows.</summary>
        public static GameTime CurrentTick => tick;

        /// <summary>VR_ALLAI: the human ship is flown by an AI pilot too.</summary>
        public static bool AllAI => Env("VR_ALLAI") != null;

        /// <summary>VR_SEED: the AI pilots' seed, or null for a random one.</summary>
        public static int? Seed => int.TryParse(Env("VR_SEED"), out int v) ? v : null;

        /// <summary>VR_DIEAT: every ship is destroyed this many seconds into the match (to see the explosions).</summary>
        public static float? DieAt => float.TryParse(Env("VR_DIEAT"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : null;

        /// <summary>VR_DEMOHOP: two of the computer's pilots hop now and then (every 10 to 15 s, when a shot comes at them if one does within 3 s), for the video.</summary>
        public static bool DemoHop => Env("VR_DEMOHOP") != null;

        /// <summary>VR_ENDAFTER: the game quits this many seconds after the match starts (VR_AUTOSTART).</summary>
        public static float? EndAfter => float.TryParse(Env("VR_ENDAFTER"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : null;

        /// <summary>VR_HOPAT: every ship hops this many seconds into the match, and again every 2 s (no pilot hops yet).</summary>
        public static float? HopAt => float.TryParse(Env("VR_HOPAT"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : null;

        /// <summary>VR_SKY: the number of the sky to use, or null for a random one.</summary>
        public static byte? SkyIndex => byte.TryParse(Env("VR_SKY"), out byte v) ? v : null;

        /// <summary>A DEVSHOT build leaves the player's settings file alone.</summary>
        public static bool DoNotSave => true;

        /// <summary>Before the game starts: the language, if one was asked for.</summary>
        public static void ApplyEarly()
        {
            if (Env("VR_LANG") is string language)
            {
                LocalizationManager.SetCulture(language);
            }
        }

        /// <summary>After the screen manager exists.</summary>
        public void Attach()
        {
            if (Env("VR_FIREWORKS") == "0")
            {
                UserSettings.Current.MenuFireworks = false;
            }

            if (Env("VR_NAME") is string name)
            {
                // the lobby takes the player's name from the signed in gamer
                object gamer = Microsoft.Xna.Framework.GamerServices.SignedInGamer.Current;
                MethodInfo setName = typeof(Microsoft.Xna.Framework.GamerServices.SignedInGamer).GetMethod("SetGamertag",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (setName == null)
                {
                    Console.WriteLine("[DEVSHOT] VR_NAME: SignedInGamer.SetGamertag not found");
                }

                setName?.Invoke(gamer, new object[] { name });
            }

            if (Env("VR_WIN") is string win)
            {
                string[] w = win.Split(',');
                var graphics = screens.Game.Services.GetService<GraphicsDeviceManager>();
                graphics.PreferredBackBufferWidth = int.Parse(w[2]);
                graphics.PreferredBackBufferHeight = int.Parse(w[3]);
                graphics.ApplyChanges();
                screens.Game.Window.Position = new Point(int.Parse(w[0]), int.Parse(w[1]));
            }

            if (Env("VR_CLEANSETTINGS") != null)
            {
                var d = new VectorRumbleSettings();
                VectorRumbleSettings s = UserSettings.Current;
                s.SkyStars = d.SkyStars; s.SkyConstellations = d.SkyConstellations; s.SkyNames = d.SkyNames;
                s.SkyPlanets = d.SkyPlanets; s.SkyNebula = d.SkyNebula; s.SkyGlow = d.SkyGlow; s.SkyComet = d.SkyComet;
                s.MusicPercent = d.MusicPercent; s.EffectsPercent = d.EffectsPercent; s.DirectionalSound = d.DirectionalSound;
                s.NeonEffect = d.NeonEffect; s.MotionBlur = d.MotionBlur; s.BlurIntensity = d.BlurIntensity;
            }

            if (Env("VR_LOG") == null)
            {
                return;
            }

            screens.Updating += (sm, gt) =>
            {
                var input = sm.Input;
                bool key = input.CurrentKeyboardStates[0].GetPressedKeyCount() > input.LastKeyboardStates[0].GetPressedKeyCount();
                bool mouse = input.IsLeftMouseButtonClicked() || input.IsRightMoustButtonClicked();
                bool pad = false;
                string which = "";
                for (int g = 0; g < input.CurrentGamePadStates.Length; g++)
                {
                    foreach (Buttons b in Enum.GetValues<Buttons>())
                    {
                        if (input.CurrentGamePadStates[g].IsButtonDown(b) && input.LastGamePadStates[g].IsButtonUp(b))
                        {
                            pad = true;
                            which += g + ":" + b + " ";
                        }
                    }
                }

                if (key || mouse || pad)
                {
                    Console.WriteLine($"INPUT at {gt.TotalGameTime.TotalSeconds:F3}: key {key} mouse {mouse} pad {pad} {which}");
                }
            };
        }

        /// <summary>Once the splash is over and the main menu is up: VR_OPEN.</summary>
        public void OpenScreens()
        {
            switch (Env("VR_OPEN"))
            {
                case "play": screens.AddScreen(new PlayMenuScreen()); break;
                case "progress": screens.AddScreen(new ProgressMenuScreen()); break;
                case "about": screens.AddScreen(new AboutMenuScreen()); break;
                case "options": screens.AddScreen(new SettingsScreen()); break;
                case "setup": screens.AddScreen(new MatchRulesScreen(false)); break;
                case "pick": screens.AddScreen(new ShipArenaScreen()); break;
                case "host": screens.AddScreen(new MatchRulesScreen(true)); break;
                case "browse": screens.AddScreen(new LanBrowserScreen()); break;
            }
        }

        /// <summary>After each frame is drawn: the scripted steps and the pictures.</summary>
        public void AfterDraw(GameTime gameTime)
        {
            double now = gameTime.TotalGameTime.TotalSeconds;
            string open = Env("VR_OPEN");

            if (Env("VR_LOG") != null)
            {
                foreach (var screen in screens.GetScreens())
                {
                    if (screen is VectorEngine.Splash.SplashScreen splash)
                    {
                        var input = screens.Input;
                        Console.WriteLine($"splash draw: elapsed {splash.Elapsed:F3} dt {gameTime.ElapsedGameTime.TotalMilliseconds:F1} any {VectorEngine.Splash.SplashScreen.AnyNewInput(input)} "
                            + $"kb {input.CurrentKeyboardStates[0].GetPressedKeyCount()}/{input.LastKeyboardStates[0].GetPressedKeyCount()} mouseL {input.IsLeftMouseButtonClicked()} touches {input.CurrentTouchState.Count}");
                    }
                }
            }

            if (Env("VR_AI") is string ai && int.TryParse(ai, out int aiCount))
            {
                MatchRules.Current.AIPilots = Math.Max(0, Math.Min(MatchRules.MaxAIPilots, aiCount));
            }

            if (Env("VR_PICK") is string pickSpec && !picked)
            {
                foreach (var screen in screens.GetScreens())
                {
                    if (screen is ShipArenaScreen room)
                    {
                        picked = true;
                        string[] parts = pickSpec.Split(',');
                        if (parts.Length > 0 && int.TryParse(parts[0], out int ship))
                        {
                            typeof(ShipArenaScreen).GetField("shipIndex", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, ship);
                        }
                        if (parts.Length > 1 && int.TryParse(parts[1], out int colour))
                        {
                            typeof(ShipArenaScreen).GetField("colourIndex", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, colour);
                        }
                        if (parts.Length > 2 && int.TryParse(parts[2], out int arena))
                        {
                            var arenas = FrameMenuScreen.World.ArenaManager.Arenas;
                            typeof(ShipArenaScreen).GetMethod("SetArena", Hidden).Invoke(room, new object[] { arenas[Math.Max(0, Math.Min(arenas.Length - 1, arena))] });
                            arenaToLobby = true;
                        }
                    }
                }
            }

            // in a network lobby the host's arena choice also goes to the lobby, as the screen's own arena buttons do
            if (arenaToLobby)
            {
                foreach (var screen in screens.GetScreens())
                {
                    if (screen is ShipArenaScreen room && typeof(ShipArenaScreen).GetField("lobby", Hidden).GetValue(room) is Networking.LanLobby lobby)
                    {
                        lobby.SetRules(MatchRules.Current.Clone());
                        arenaToLobby = false;
                    }
                }
            }

            if (Env("VR_BURST") is string burst)
            {
                string[] b = burst.Split(',');
                double start = double.Parse(b[0], System.Globalization.CultureInfo.InvariantCulture);
                int count = int.Parse(b[1]);
                double interval = double.Parse(b[2], System.Globalization.CultureInfo.InvariantCulture);
                if (now + 1e-6 >= start + burstTaken * interval)
                {
                    string dir = Env("VR_BURST_DIR") ?? "burst";
                    System.IO.Directory.CreateDirectory(dir);
                    Screenshots.Save(device, System.IO.Path.Combine(dir, $"frame{burstTaken:D3}.png"));
                    if (++burstTaken >= count)
                    {
                        exit();
                    }
                }
            }

            if (Env("VR_TOUR") is string tour)
            {
                RunTour(now, tour.Split(','), open);
            }

            if (EndAfter is float endAfter && autoStarted && now > matchStartedAt + endAfter)
            {
                exit();
            }

            if (Env("VR_AUTOSTART") != null && now > 8 && !autoStarted)
            {
                foreach (var screen in screens.GetScreens())
                {
                    if (screen is ShipArenaScreen pick)
                    {
                        autoStarted = true;
                        matchStartedAt = now;
                        Console.WriteLine("[DEVSHOT] match-start unix-ms " + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                        Call(pick, "StartAlone");
                        break;
                    }
                }
            }

            if (open == "host" && now > 8 && !autoStarted)
            {
                foreach (var screen in screens.GetScreens())
                {
                    if (screen is MatchRulesScreen rules)
                    {
                        autoStarted = true;
                        Call(rules, "StartHosting");
                    }
                }
            }

            if (open == "browse" && now > 13 && !autoStarted)
            {
                foreach (var screen in screens.GetScreens())
                {
                    if (screen is LanBrowserScreen browser)
                    {
                        var sessions = (IList)typeof(LanBrowserScreen).GetField("sessions", Hidden).GetValue(browser);
                        if (sessions.Count > 0)
                        {
                            autoStarted = true;
                            typeof(LanBrowserScreen).GetMethod("Join", Hidden).Invoke(browser, new[] { sessions[0] });
                        }
                    }
                }
            }

            if (Env("VR_NETGO") != null && now > Seconds("VR_NETGO", 0) && !netGone)
            {
                foreach (var screen in screens.GetScreens())
                {
                    if (screen is ShipArenaScreen room)
                    {
                        netGone = true;
                        autoStarted = true;
                        matchStartedAt = now;
                        Console.WriteLine("[DEVSHOT] match-start unix-ms " + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                        Call(room, open == "host" || Env("VR_TOUR") != null ? "StartNetworkMatch" : "ToggleReady");
                    }
                }
            }

            if (Env("VR_LANGSTEP") is string step && now > 8 && !langStepped)
            {
                foreach (var screen in screens.GetScreens())
                {
                    if (screen is SettingsScreen settings)
                    {
                        langStepped = true;
                        typeof(SettingsScreen).GetMethod("StepLanguage", Hidden).Invoke(settings, new object[] { int.Parse(step) });
                    }
                }
            }

            if (Env("VR_THEN") is string then && now > 14 && !thenDone)
            {
                thenDone = true;
                OpenOver(then);
            }

            if (Env("VR_ACT") is string act && thenDone)
            {
                string[] rowsToSelect = act.Split(',');
                if (actIndex < rowsToSelect.Length && now > 18 + actIndex * 4)
                {
                    var all = screens.GetScreens();
                    var top = all[all.Length - 1];
                    var rows = (IList)typeof(FrameScreen).GetProperty("Rows", Hidden).GetValue(top);
                    var row = rows[int.Parse(rowsToSelect[actIndex])];
                    row.GetType().GetMethod("Select", Hidden).Invoke(row, null);
                    Console.WriteLine($"ACT {actIndex}: {top.GetType().Name} row {rowsToSelect[actIndex]}");
                    actIndex++;
                }
            }

            if (Env("VR_SPLASH") != null && Env("VR_OUT") != null)
            {
                foreach (var screen in screens.GetScreens())
                {
                    if (screen is VectorEngine.Splash.SplashScreen splash && splash.Elapsed >= Seconds("VR_SPLASH", 0))
                    {
                        Screenshots.Save(device, Env("VR_OUT"));
                        Console.WriteLine("elapsed " + splash.Elapsed);
                        exit();
                    }
                }
            }

            if (Env("VR_SHOT") is string path && now > Seconds("VR_SHOT_AT", 10))
            {
                Screenshots.Save(device, path);
                exit();
            }
        }

        // The scripted walk through the menus: the highlight moves to the row, rests a moment, then the row is pressed
        void RunTour(double now, string[] steps, string open)
        {
            if (tourNext == 0)
            {
                tourNext = Seconds("VR_TOURSTART", 4);
            }

            if (now < tourNext)
            {
                return;
            }

            var top = screens.GetScreens();
            FrameScreen menu = null;
            for (int i = top.Length - 1; i >= 0 && menu == null; i--)
            {
                menu = top[i] as FrameMenuScreen;
            }

            if (tourStep >= steps.Length)
            {
                // the end of the walk: on the host's rules screen, rest a moment and start hosting
                if (menu is MatchRulesScreen rules && !hostingStarted)
                {
                    if (tourDoneAt < 0)
                    {
                        tourDoneAt = now;
                    }

                    if (now > tourDoneAt + Seconds("VR_RULESHOLD", 3))
                    {
                        hostingStarted = true;
                        Call(rules, "StartHosting");
                    }
                }

                return;
            }

            if (menu == null)
            {
                return;
            }

            string wanted = steps[tourStep].Trim();
            var rows = (IList<FrameRow>)typeof(FrameScreen).GetProperty("Rows", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(menu);
            int index = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].Label, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                }
            }

            if (index < 0)
            {
                // the screen is still sliding in, or the row is not on this screen: look again shortly
                tourNext = now + 0.5;
                return;
            }

            if (tourPhase == 0)
            {
                typeof(FrameScreen).GetProperty("SelectedIndex", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(menu, index);
                tourPhase = 1;
                tourNext = now + 1.4;
            }
            else
            {
                rows[index].Select();
                tourStep++;
                tourPhase = 0;
                tourNext = now + 2.4;
            }
        }

        static void Call(object screen, string method) => screen.GetType().GetMethod(method, Hidden).Invoke(screen, null);

        // a screen over whatever is showing, with made-up scores where it needs them
        void OpenOver(string which)
        {
            var shape = new[] { new Vector2(0, -10), new Vector2(-5, 0), new Vector2(-2, 10), new Vector2(0, 4), new Vector2(2, 10), new Vector2(5, 0) };
            var orange = new Color(255, 154, 31);
            var cyan = new Color(53, 242, 255);
            string winner = string.Format(Strings.Player_X_Wins, "dominique");

            switch (which)
            {
                case "pause": screens.AddScreen(new PauseMenuScreen()); break;
                case "confirm": screens.AddScreen(new MessageBoxScreen(Strings.Confirm_LeaveTitle, "", Strings.Confirm_Leave, Strings.Confirm_Stay)); break;
                case "exit": screens.AddScreen(new MessageBoxScreen(Strings.Exit_Prompt, "", Strings.Confirm_ExitButton, Strings.Confirm_Stay, acceptIsDefault: true)); break;
                case "notice": screens.AddScreen(new MessageBoxScreen(Strings.Lan_Already_Started)); break;
                case "overnet":
                    screens.AddScreen(new GameOverScreen(winner,
                        new List<ScoreLine> { new ScoreLine { Name = "dominique", Score = 10, Colour = orange, Shape = shape } }, false, false));
                    break;
                case "over":
                    screens.AddScreen(new GameOverScreen(winner, new List<ScoreLine>
                    {
                        new ScoreLine { Name = "dominique", Score = 10, Colour = orange, Shape = shape },
                        new ScoreLine { Name = "Alexandra the Great", Score = 7, Colour = cyan, Shape = shape },
                    }, true, true));
                    break;
            }
        }
    }
}
#endif
