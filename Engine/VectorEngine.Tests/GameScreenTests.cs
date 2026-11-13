using System;
using Microsoft.Xna.Framework;
using VectorEngine.Screens;
using Xunit;

namespace VectorEngine.Tests;

public class GameScreenTests
{
    private sealed class Fade : GameScreen
    {
        public Fade(double onSeconds, double offSeconds)
        {
            TransitionOnTime = TimeSpan.FromSeconds(onSeconds);
            TransitionOffTime = TimeSpan.FromSeconds(offSeconds);
        }
    }

    private static GameTime Frame(double seconds) => new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));

    [Fact]
    public void A_new_screen_starts_fully_transitioned_off()
    {
        var screen = new Fade(1, 1);
        Assert.Equal(0f, screen.TransitionAlpha);
        Assert.Equal(0, screen.TransitionAlphaByte);
    }

    [Fact]
    public void It_transitions_on_over_its_time_then_becomes_active()
    {
        var screen = new Fade(1, 1);
        screen.Update(Frame(0.5), false, false);
        Assert.Equal(ScreenState.TransitionOn, screen.ScreenState);
        Assert.Equal(0.5f, screen.TransitionAlpha, 3);
        Assert.Equal(127, screen.TransitionAlphaByte);

        screen.Update(Frame(0.6), false, false);
        Assert.Equal(ScreenState.Active, screen.ScreenState);
        Assert.Equal(1f, screen.TransitionAlpha, 3);
        Assert.Equal(255, screen.TransitionAlphaByte);
    }

    [Fact]
    public void A_screen_with_no_transition_time_is_active_at_once()
    {
        var screen = new Fade(0, 0);
        screen.Update(Frame(0.016), false, false);
        Assert.Equal(ScreenState.Active, screen.ScreenState);
    }

    [Fact]
    public void A_covered_screen_transitions_off_and_hides()
    {
        var screen = new Fade(0, 1);
        screen.Update(Frame(0.016), false, false);
        screen.Update(Frame(0.5), false, true);
        Assert.Equal(ScreenState.TransitionOff, screen.ScreenState);
        screen.Update(Frame(0.6), false, true);
        Assert.Equal(ScreenState.Hidden, screen.ScreenState);
    }
}
