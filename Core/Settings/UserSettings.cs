using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>What the player chose in Settings, as it is saved between runs.</summary>
    public class VectorRumbleSettings
    {
        /// <summary>The language the player chose (a culture name such as "de" or "zh-CN"; empty is English), or null to follow the device's.</summary>
        public string LanguageCode { get; set; }

        /// <summary>The music volume, in percent.</summary>
        public int MusicPercent { get; set; } = 100;

        /// <summary>The sound effects' volume, in percent.</summary>
        public int EffectsPercent { get; set; } = 100;

        public bool FullScreen { get; set; }

        /// <summary>The neon glow on the lines of the game.</summary>
        public bool NeonEffect { get; set; } = true;

        /// <summary>The length of the trail behind moving things, 1 to 10.</summary>
        public int BlurIntensity { get; set; } = 5;

        public bool MotionBlur { get; set; } = true;
    }

    /// <summary>
    /// The player's settings: loaded when the game starts, applied, and saved each time one changes. A damaged or
    /// missing file is not an error: the game starts with the defaults.
    /// </summary>
    static class UserSettings
    {
        /// <summary>The languages the game has words for, as culture names: English (empty) and the satellite resource files.</summary>
        public static readonly string[] LanguageCodes = { "", "de", "es", "fr", "ja", "ko", "zh-CN" };

        /// <summary>The volumes the Settings screen steps through.</summary>
        public static readonly int[] Volumes = { 0, 25, 50, 75, 100 };

        // XACT categories of the game's sound project: the music, and everything else
        const string MusicCategory = "Music";
        const string EffectsCategory = "Default";

        static SettingsManager<VectorRumbleSettings> manager;

        public static VectorRumbleSettings Current => manager?.Settings ?? fallback;

        static readonly VectorRumbleSettings fallback = new VectorRumbleSettings();

        public static List<CultureInfo> Languages
        {
            get
            {
                var list = new List<CultureInfo>();
                foreach (string code in LanguageCodes)
                {
                    list.Add(CultureInfo.GetCultureInfo(code));
                }

                return list;
            }
        }

        /// <summary>Opens the settings file (this also loads it).</summary>
        public static void Initialize()
        {
            try
            {
                ISettingsStorage storage = VectorEngine.Platform.IsMobile
                    ? (ISettingsStorage)new MobileSettingsStorage("VectorRumble")
                    : new DesktopSettingsStorage("VectorRumble");
                manager = new SettingsManager<VectorRumbleSettings>(storage);
            }
            catch (Exception)
            {
                // an unreadable file: play with the defaults, and save over it on the first change
                manager = null;
            }
        }

        public static void Save()
        {
            try
            {
                manager?.Save();
            }
            catch (Exception)
            {
                // the settings cannot be saved (a read-only folder): they still apply for this run
            }
        }

        /// <summary>The language to use now: the player's choice if there is one, otherwise the device's.</summary>
        public static CultureInfo ResolveLanguage() => LocalizationManager.Resolve(Current.LanguageCode, Languages);

        public static void SetLanguage(CultureInfo culture)
        {
            Current.LanguageCode = culture.Name;
            LocalizationManager.SetCulture(culture.Name);
            Save();
        }

        /// <summary>Applies what the display and the language need before the game's content is loaded.</summary>
        public static void ApplyBeforeStart(GraphicsDeviceManager graphics, bool changeFullScreen)
        {
            LocalizationManager.SetCulture(ResolveLanguage().Name);

            WorldRules.NeonEffect = Current.NeonEffect;
            WorldRules.BlurIntensity = Math.Max(1, Math.Min(10, Current.BlurIntensity));
            WorldRules.MotionBlur = Current.MotionBlur;

            if (changeFullScreen && !VectorEngine.Platform.IsMobile)
            {
                graphics.IsFullScreen = Current.FullScreen;
            }
        }

        public static void ApplyAudio(AudioManager audio)
        {
            audio?.SetCategoryVolume(MusicCategory, Current.MusicPercent / 100f);
            audio?.SetCategoryVolume(EffectsCategory, Current.EffectsPercent / 100f);
        }

        public static void SetMusic(AudioManager audio, int percent)
        {
            Current.MusicPercent = percent;
            audio?.SetCategoryVolume(MusicCategory, percent / 100f);
            Save();
        }

        public static void SetEffects(AudioManager audio, int percent)
        {
            Current.EffectsPercent = percent;
            audio?.SetCategoryVolume(EffectsCategory, percent / 100f);
            Save();
        }

        /// <summary>The language's name for the Settings screen, in Latin letters that every font has.</summary>
        public static string LanguageName(CultureInfo culture)
        {
            if (culture.Name.Length == 0)
            {
                return "English (EN)";
            }

            string name = culture.Name == "zh-CN" ? "Chinese" : culture.EnglishName;
            return name + " (" + culture.TwoLetterISOLanguageName.ToUpperInvariant() + ")";
        }
    }
}
