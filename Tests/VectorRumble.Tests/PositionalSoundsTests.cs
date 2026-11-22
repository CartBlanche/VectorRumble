using System;
using System.IO;
using Xunit;

namespace VectorRumble.Tests
{
    public class PositionalSoundsTests
    {
        static string ContentFolder()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "Content", "Content.mgcb")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            Assert.NotNull(dir);
            return Path.Combine(dir, "Content");
        }

        [Fact]
        public void A_sound_in_the_middle_is_not_panned_and_the_edges_are_capped()
        {
            Assert.Equal(0f, PositionalSounds.PanFor(640f, 1280f), 3);
            Assert.Equal(-PositionalSounds.MaxPan, PositionalSounds.PanFor(0f, 1280f), 3);
            Assert.Equal(PositionalSounds.MaxPan, PositionalSounds.PanFor(1280f, 1280f), 3);
            Assert.True(PositionalSounds.MaxPan < 1f);
        }

        [Fact]
        public void A_sound_off_the_screen_pans_no_further_than_the_edge()
        {
            Assert.Equal(-PositionalSounds.MaxPan, PositionalSounds.PanFor(-500f, 1280f), 3);
            Assert.Equal(PositionalSounds.MaxPan, PositionalSounds.PanFor(5000f, 1280f), 3);
        }

        [Fact]
        public void Pan_grows_from_left_to_right()
        {
            float last = -1f;
            for (float x = 0; x <= 1280f; x += 64f)
            {
                float pan = PositionalSounds.PanFor(x, 1280f);
                Assert.True(pan > last);
                last = pan;
            }
        }

        [Fact]
        public void A_zero_width_play_area_does_not_pan()
        {
            Assert.Equal(0f, PositionalSounds.PanFor(100f, 0f));
        }

        [Fact]
        public void Stereo_samples_mix_down_to_their_average()
        {
            // left 1000 and right 3000 -> 2000; left -2000 and right -4000 -> -3000
            var stereo = new byte[8];
            BitConverter.GetBytes((short)1000).CopyTo(stereo, 0);
            BitConverter.GetBytes((short)3000).CopyTo(stereo, 2);
            BitConverter.GetBytes((short)-2000).CopyTo(stereo, 4);
            BitConverter.GetBytes((short)-4000).CopyTo(stereo, 6);

            byte[] mono = PositionalSounds.Downmix(stereo, 2);

            Assert.Equal(4, mono.Length);
            Assert.Equal(2000, BitConverter.ToInt16(mono, 0));
            Assert.Equal(-3000, BitConverter.ToInt16(mono, 2));
        }

        [Fact]
        public void A_mono_clip_is_left_alone()
        {
            var data = new byte[] { 1, 2, 3, 4 };
            Assert.Same(data, PositionalSounds.Downmix(data, 1));
        }

        [Fact]
        public void Every_clip_of_every_cue_exists_reads_as_mono_and_is_copied_to_the_game()
        {
            string content = ContentFolder();
            string mgcb = File.ReadAllText(Path.Combine(content, "Content.mgcb"));
            foreach (var cue in PositionalSounds.Cues)
            {
                Assert.NotEmpty(cue.Value.Layers);
                foreach (string[] layer in cue.Value.Layers)
                {
                    Assert.NotEmpty(layer);
                    foreach (string clip in layer)
                    {
                        string file = Path.Combine(content, "Audio", "Waves", clip + ".wav");
                        Assert.True(File.Exists(file), file);
                        Assert.Contains("/copy:Audio/Waves/" + clip + ".wav", mgcb);

                        using (FileStream stream = File.OpenRead(file))
                        {
                            PositionalSounds.WavData wav = PositionalSounds.ReadWav(stream);
                            Assert.True(wav.Samples.Length > 0 && wav.Samples.Length % 2 == 0);
                            Assert.InRange(wav.SampleRate, 8000, 48000);
                        }
                    }
                }
            }
        }

        [Fact]
        public void A_stereo_file_becomes_half_as_long_in_mono()
        {
            string file = Path.Combine(ContentFolder(), "Audio", "Waves", "explosion1.wav");
            int dataBytes;
            using (FileStream stream = File.OpenRead(file))
            {
                dataBytes = PositionalSounds.ReadWav(stream).Samples.Length;
            }

            Assert.InRange(dataBytes * 2, new FileInfo(file).Length - 200, new FileInfo(file).Length);
        }

        [Fact]
        public void A_cue_the_player_does_not_have_is_left_to_the_sound_bank()
        {
            using (var sounds = new PositionalSounds())
            {
                Assert.False(sounds.Play("rocket", 100f, 1280f, 1f, true));
                Assert.False(sounds.Play("menuMove", 100f, 1280f, 1f, true));
            }
        }

        [Fact]
        public void The_directional_sound_words_exist_in_every_language()
        {
            foreach (string culture in LocalizationTests.Cultures)
            {
                Assert.False(string.IsNullOrWhiteSpace(Strings.ResourceManager.GetString("Label_DirectionalSound", new System.Globalization.CultureInfo(culture))));
            }

            Assert.True(new VectorRumbleSettings().DirectionalSound);
        }
    }
}
