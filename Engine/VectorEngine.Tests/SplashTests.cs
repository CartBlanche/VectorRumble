using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using VectorEngine.Splash;
using Xunit;

namespace VectorEngine.Tests
{
    public class SplashTests
    {
        static List<SplashLine> Frame(SplashAnimation a, float t)
        {
            var lines = new List<SplashLine>();
            a.Build(t, lines);
            return lines;
        }

        static bool Near(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < 0.05f;

        [Fact]
        public void The_wordmark_has_twelve_letters_inside_the_canvas_and_centred()
        {
            var words = VectorEngineLogo.Wordmark;
            Assert.Equal(VectorEngineLogo.LetterCount, words.Select(s => s.Letter).Distinct().Count());

            var xs = words.SelectMany(s => new[] { s.A.X, s.B.X }).ToArray();
            var ys = words.SelectMany(s => new[] { s.A.Y, s.B.Y }).ToArray();
            Assert.InRange(xs.Min(), 0f, VectorEngineLogo.Width);
            Assert.InRange(xs.Max(), 0f, VectorEngineLogo.Width);
            Assert.InRange(ys.Min(), 0f, VectorEngineLogo.Height);
            Assert.InRange(ys.Max(), 0f, VectorEngineLogo.Height);
            Assert.Equal(VectorEngineLogo.Width / 2f, (xs.Min() + xs.Max()) / 2f, 1);
        }

        [Fact]
        public void The_mark_is_one_V_arm_pair_and_three_prongs_above_the_words()
        {
            var mark = VectorEngineLogo.Mark;
            Assert.Equal(5, mark.Count); // left arm, right arm (the spine), three prongs

            // the three prongs are level
            Assert.Equal(3, mark.Count(s => Math.Abs(s.A.Y - s.B.Y) < 0.001f));

            float markBottom = mark.Max(s => Math.Max(s.A.Y, s.B.Y));
            float wordsTop = VectorEngineLogo.Wordmark.Min(s => Math.Min(s.A.Y, s.B.Y));
            Assert.True(markBottom < wordsTop);
        }

        [Fact]
        public void When_it_is_over_every_line_sits_on_a_letter_or_mark_segment_at_full_strength()
        {
            var opts = new SplashOptions();
            var lines = Frame(new SplashAnimation(opts), SplashAnimation.Seconds);
            var targets = VectorEngineLogo.Wordmark.Concat(VectorEngineLogo.Mark).ToList();

            Assert.Equal(targets.Count, lines.Count);
            foreach (var line in lines)
            {
                // a line looks the same flipped, so match its ends either way round
                Assert.Contains(targets, s => (Near(s.A, line.A) && Near(s.B, line.B)) || (Near(s.A, line.B) && Near(s.B, line.A)));
                Assert.Equal(1f, line.Alpha, 3);
            }
            Assert.Equal(VectorEngineLogo.Mark.Count, lines.Count(l => (int)l.Color.X == opts.MarkColor.R && (int)l.Color.Z == opts.MarkColor.B));
        }

        [Fact]
        public void At_the_start_the_lines_are_loose_and_mostly_not_yet_visible()
        {
            var lines = Frame(new SplashAnimation(), 0f);
            var targets = VectorEngineLogo.Wordmark.Concat(VectorEngineLogo.Mark).ToList();

            Assert.All(lines, l => Assert.True(l.Alpha < 0.5f));
            int onTarget = lines.Count(l => targets.Any(s => Near(s.A, l.A) && Near(s.B, l.B)));
            Assert.True(onTarget < lines.Count / 4);
        }

        [Fact]
        public void The_same_seed_gives_the_same_animation_and_another_seed_a_different_one()
        {
            var a = Frame(new SplashAnimation(new SplashOptions { Seed = 11 }), 1.2f);
            var b = Frame(new SplashAnimation(new SplashOptions { Seed = 11 }), 1.2f);
            var c = Frame(new SplashAnimation(new SplashOptions { Seed = 12 }), 1.2f);

            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
        }

        [Fact]
        public void Every_frame_has_finite_numbers()
        {
            var animation = new SplashAnimation();
            for (float t = 0f; t <= SplashAnimation.Seconds + 0.5f; t += 0.05f)
                foreach (var l in Frame(animation, t))
                {
                    Assert.True(float.IsFinite(l.A.X) && float.IsFinite(l.A.Y) && float.IsFinite(l.B.X) && float.IsFinite(l.B.Y));
                    Assert.InRange(l.Alpha, 0f, 1f);
                    Assert.True(l.Width > 0f);
                }
        }

        [Fact]
        public void Skipping_jumps_to_the_fade_out()
        {
            var splash = new SplashScreen();
            Assert.Equal(0f, splash.Elapsed);
            splash.Skip();
            Assert.True(splash.Elapsed >= SplashAnimation.Seconds);
        }
    }
}
