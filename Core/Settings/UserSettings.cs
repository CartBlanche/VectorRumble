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

        /// <summary>Sound effects come from the side of the screen they happen on (stereo pan).</summary>
        public bool DirectionalSound { get; set; } = true;

        public bool FullScreen { get; set; }

        /// <summary>The neon glow on the lines of the game.</summary>
        public bool NeonEffect { get; set; } = true;

        /// <summary>The bursts of sparks behind the menus. On by default; some players find them too busy.</summary>
        public bool MenuFireworks { get; set; } = true;

        /// <summary>The length of the trail behind moving things, 1 to 10.</summary>
        public int BlurIntensity { get; set; } = 5;

        public bool MotionBlur { get; set; } = true;

        /// <summary>Sky behind the arena: the single pixel stars.</summary>
        public bool SkyStars { get; set; } = true;

        /// <summary>Sky behind the arena: constellations as faint line art.</summary>
        public bool SkyConstellations { get; set; } = true;

        /// <summary>Sky behind the arena: names of the bright stars, constellations and planets.</summary>
        public bool SkyNames { get; set; }

        /// <summary>Sky behind the arena: planets.</summary>
        public bool SkyPlanets { get; set; } = true;

        /// <summary>Sky behind the arena: the nebula as faint dots.</summary>
        public bool SkyNebula { get; set; } = true;

        /// <summary>Sky behind the arena: the soft glow of the nebula.</summary>
        public bool SkyGlow { get; set; } = true;

        /// <summary>Sky behind the arena: the comet that crosses now and then.</summary>
        public bool SkyComet { get; set; } = true;

        /// <summary>The parts of the sky to draw.</summary>
        internal SkyLayers SkyLayers =>
            (SkyStars ? SkyLayers.Stars : 0) | (SkyConstellations ? SkyLayers.Constellations : 0) |
            (SkyNames ? SkyLayers.Names : 0) | (SkyPlanets ? SkyLayers.Planets : 0) |
            (SkyNebula ? SkyLayers.Nebula : 0) | (SkyGlow ? SkyLayers.Glow : 0) | (SkyComet ? SkyLayers.Comet : 0);

        /// <summary>The menus freeze, with a dim "Paused", after ten seconds without input.</summary>
        public bool PauseIdleMenus { get; set; } = true;

        /// <summary>How well the computer's pilots fly in a game on this machine alone.</summary>
        public AIDifficulty AIDifficulty { get; set; } = AIDifficulty.Medium;

        /// <summary>The ship the player flew last, by name (a community ship by its file id): offered first next time.</summary>
        public string LastShip { get; set; }

        /// <summary>The colour the player flew last, as its place in the palette.</summary>
        public int LastColour { get; set; }
    }

    /// <summary>
    /// The player's settings: loaded when the game starts, applied, and saved each time one changes. A damaged or
    /// missing file is not an error: the game starts with the defaults.
    /// </summary>
    static class UserSettings
    {
        /// <summary>The languages the player can choose, as culture names: English (empty) and the ones with words and a font.</summary>
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
#if DEVSHOT
            // a scripted run (see DevShots) leaves the player's settings file as it was
            if (DevShots.DoNotSave)
            {
                return;
            }
#endif
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
