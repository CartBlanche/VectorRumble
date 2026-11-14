using Xunit;

namespace VectorRumble.Tests
{
    public class MenuIdleTests
    {
        static void Run(MenuIdle idle, float seconds, bool activity = false, bool allowed = true)
        {
            // in frames of a sixtieth, as the game runs
            for (float t = 0; t < seconds; t += 1f / 60f)
            {
                idle.Update(1f / 60f, activity, allowed);
            }
        }

        [Fact]
        public void The_menus_freeze_after_ten_seconds_without_input_and_not_before()
        {
            var idle = new MenuIdle();

            Run(idle, 9.5f);
            Assert.False(idle.Paused);
            Assert.False(MenuIdle.Frozen);

            Run(idle, 1f);
            Assert.True(idle.Paused);
            Assert.True(MenuIdle.Frozen);
        }

        [Fact]
        public void Any_input_wakes_them_at_once_and_starts_the_count_again()
        {
            var idle = new MenuIdle();
            Run(idle, 12f);
            Assert.True(idle.Paused);

            idle.Update(1f / 60f, activity: true, allowed: true);
            Assert.False(idle.Paused);
            Assert.False(MenuIdle.Frozen);

            Run(idle, 9f);
            Assert.False(idle.Paused);
        }

        [Fact]
        public void The_dim_comes_in_gently_and_goes_quickly()
        {
            var idle = new MenuIdle();
            Run(idle, 10.2f);
            Assert.InRange(idle.Dim, 0.01f, 0.99f);

            Run(idle, 1f);
            Assert.Equal(1f, idle.Dim);

            Run(idle, 0.3f, activity: true);
            Assert.Equal(0f, idle.Dim);
        }

        [Fact]
        public void A_screen_that_must_keep_going_never_freezes()
        {
            var idle = new MenuIdle();
            Run(idle, 60f, allowed: false);
            Assert.False(idle.Paused);
            Assert.Equal(0f, idle.Dim);
        }

        [Fact]
        public void Releasing_it_lets_the_menus_run_again()
        {
            var idle = new MenuIdle();
            Run(idle, 12f);
            idle.Release();
            Assert.False(MenuIdle.Frozen);
            Assert.False(idle.Paused);
            Assert.Equal(0f, idle.Dim);
        }

        [Fact]
        public void The_setting_is_on_by_default_and_is_saved()
        {
            Assert.True(new VectorRumbleSettings().PauseIdleMenus);
            var back = System.Text.Json.JsonSerializer.Deserialize<VectorRumbleSettings>(
                System.Text.Json.JsonSerializer.Serialize(new VectorRumbleSettings { PauseIdleMenus = false }));
            Assert.False(back.PauseIdleMenus);
            Assert.True(System.Text.Json.JsonSerializer.Deserialize<VectorRumbleSettings>("{\"MusicPercent\":50}").PauseIdleMenus);
        }
    }
}
