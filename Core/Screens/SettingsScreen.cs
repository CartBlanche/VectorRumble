using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>
    /// Settings: language, music and effects volume, fullscreen, the neon glow, and under "More" the blur. Each change
    /// applies at once and is saved.
    /// </summary>
    class SettingsScreen : FrameMenuScreen
    {
        readonly List<CultureInfo> languages = UserSettings.Languages;
        GraphicsDeviceManager graphics;
        AudioManager audio;
        bool more;

        public SettingsScreen()
        {
            Layout = FrameLayout.Rows;
            ShowPanel = false;
            BuildRows();
        }

        public override void LoadContent()
        {
            graphics = ScreenManager.Game.Services.GetService<GraphicsDeviceManager>();
            audio = (AudioManager)ScreenManager.Game.Services.GetService(typeof(AudioManager));
            base.LoadContent();
        }

        // the rows hold the words of the language they were built in, so a change of language builds them again
        void BuildRows()
        {
            int selected = SelectedIndex;
            Heading = Strings.Settings;
            Rows.Clear();

            var language = new FrameRow(Strings.Label_Language, FrameRowKind.Choice) { Value = () => UserSettings.LanguageName(CurrentLanguage) };
            language.Adjusted += StepLanguage;
            Rows.Add(language);

            Rows.Add(Volume(Strings.Label_Music, () => UserSettings.Current.MusicPercent, p => UserSettings.SetMusic(audio, p)));
            Rows.Add(Volume(Strings.Label_Effects, () => UserSettings.Current.EffectsPercent, p => UserSettings.SetEffects(audio, p)));

#if !USE_DEFAULT_DEVICE_SETTINGS
            if (!VectorEngine.Platform.IsMobile)
            {
                var fullScreen = new FrameRow(Strings.Label_Fullscreen, FrameRowKind.Toggle) { Value = () => OnOff(graphics?.IsFullScreen ?? false) };
                fullScreen.Adjusted += d =>
                {
                    graphics?.ToggleFullScreen();
                    UserSettings.Current.FullScreen = graphics?.IsFullScreen ?? false;
                    UserSettings.Save();
                };
                Rows.Add(fullScreen);
            }
#endif

            var neon = new FrameRow(Strings.Label_NeonGlow, FrameRowKind.Toggle) { Value = () => OnOff(WorldRules.NeonEffect) };
            neon.Adjusted += d =>
            {
                WorldRules.NeonEffect = !WorldRules.NeonEffect;
                UserSettings.Current.NeonEffect = WorldRules.NeonEffect;
                UserSettings.Save();
            };
            Rows.Add(neon);

            var moreRow = new FrameRow(Strings.Label_More, FrameRowKind.Expander) { Expanded = () => more };
            moreRow.Selected += () => more = !more;
            Rows.Add(moreRow);

            var blur = new FrameRow(Strings.Label_BlurIntensity, FrameRowKind.Choice)
            {
                Value = () => WorldRules.BlurIntensity.ToString(CultureInfo.CurrentCulture),
                Hidden = () => !more,
            };
            blur.Adjusted += d =>
            {
                WorldRules.BlurIntensity = ((WorldRules.BlurIntensity - 1 + d + 10) % 10) + 1;
                UserSettings.Current.BlurIntensity = WorldRules.BlurIntensity;
                UserSettings.Save();
            };
            Rows.Add(blur);

            var motion = new FrameRow(Strings.Label_MotionBlur, FrameRowKind.Toggle)
            {
                Value = () => OnOff(WorldRules.MotionBlur),
                Hidden = () => !more,
            };
            motion.Adjusted += d =>
            {
                WorldRules.MotionBlur = !WorldRules.MotionBlur;
                UserSettings.Current.MotionBlur = WorldRules.MotionBlur;
                UserSettings.Save();
            };
            Rows.Add(motion);

            SelectedIndex = selected;
        }

        protected override void RebuildForLanguage() => BuildRows();

        static string OnOff(bool on) => on ? Strings.Value_On : Strings.Value_Off;

        static FrameRow Volume(string label, Func<int> get, Action<int> set)
        {
            var row = new FrameRow(label, FrameRowKind.Slider)
            {
                Value = () => get() + "%",
                Fraction = () => get() / 100f,
            };
            row.Adjusted += d =>
            {
                int[] steps = UserSettings.Volumes;
                int index = Array.IndexOf(steps, get());
                if (index < 0)
                {
                    index = steps.Length - 1;
                }

                set(steps[Math.Max(0, Math.Min(steps.Length - 1, index + d))]);
            };
            return row;
        }

        CultureInfo CurrentLanguage
        {
            get
            {
                string code = CultureInfo.CurrentUICulture.Name;
                int i = languages.FindIndex(c => c.Name == code);
                return languages[Math.Max(0, i)];
            }
        }

        void StepLanguage(int direction)
        {
            int current = Math.Max(0, languages.FindIndex(c => c.Name == CurrentLanguage.Name));
            int index = ((current + direction) % languages.Count + languages.Count) % languages.Count;
            UserSettings.SetLanguage(languages[index]);

            // the screens behind this one (the main menu) read their words again, and the fonts change with the language
            ScreenManager.RefreshScreensAfterLanguageChange();
        }
    }
}
