using System.Globalization;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace VectorRumble.Tests
{
    public class UserSettingsTests
    {
        [Fact]
        public void Every_listed_language_has_its_words()
        {
            // English is the neutral file; each other code needs its own resource file, or the picker offers a language that is not there
            foreach (CultureInfo culture in UserSettings.Languages.Where(c => c.Name.Length > 0))
            {
                var set = Strings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
                Assert.True(set != null, culture.Name + " has no Strings resource file");
            }
        }

        [Fact]
        public void Languages_are_English_first_and_have_readable_names()
        {
            var languages = UserSettings.Languages;
            Assert.Equal(string.Empty, languages[0].Name);
            Assert.All(languages, c => Assert.Matches("^[A-Za-z ()]+$", UserSettings.LanguageName(c)));
        }

        [Fact]
        public void Settings_survive_being_saved_and_read_back()
        {
            var settings = new VectorRumbleSettings { LanguageCode = "de", MusicPercent = 25, EffectsPercent = 50, FullScreen = true, NeonEffect = false, BlurIntensity = 7, MotionBlur = false };
            var back = JsonSerializer.Deserialize<VectorRumbleSettings>(JsonSerializer.Serialize(settings));
            Assert.Equal("de", back.LanguageCode);
            Assert.Equal(25, back.MusicPercent);
            Assert.Equal(50, back.EffectsPercent);
            Assert.True(back.FullScreen);
            Assert.False(back.NeonEffect);
            Assert.Equal(7, back.BlurIntensity);
            Assert.False(back.MotionBlur);
        }

        [Fact]
        public void Menu_fireworks_are_on_by_default_and_the_choice_survives_saving()
        {
            Assert.True(new VectorRumbleSettings().MenuFireworks);

            var off = new VectorRumbleSettings { MenuFireworks = false };
            var back = JsonSerializer.Deserialize<VectorRumbleSettings>(JsonSerializer.Serialize(off));
            Assert.False(back.MenuFireworks);

            // a settings file from before the option existed leaves them on
            var old = JsonSerializer.Deserialize<VectorRumbleSettings>("{\"MusicPercent\":40}");
            Assert.True(old.MenuFireworks);
        }

        [Fact]
        public void New_settings_default_to_the_devices_language_and_full_volume()
        {
            var settings = new VectorRumbleSettings();
            Assert.Null(settings.LanguageCode);
            Assert.Equal(100, settings.MusicPercent);
            Assert.Equal(100, settings.EffectsPercent);
        }
    }
}
