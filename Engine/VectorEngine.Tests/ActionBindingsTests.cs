using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using VectorEngine.Input;
using Xunit;

namespace VectorEngine.Tests;

public class ActionBindingsTests
{
    private static readonly ActionId Send = new("Send");

    private static ActionBindings WithSend()
    {
        var b = new ActionBindings();
        b.Define(Send, new[] { Keys.Enter }, new[] { Buttons.Y });
        return b;
    }

    [Fact]
    public void Core_actions_have_their_built_in_keys_and_wheel()
    {
        var b = new ActionBindings();
        Assert.Contains(Keys.Space, b[GameAction.Select].Keys);
        Assert.Contains(Buttons.Back, b[GameAction.Cancel].Buttons);
        Assert.Equal(WheelDirection.Up, b[GameAction.Up].Wheel);
    }

    [Fact]
    public void A_game_can_add_its_own_action()
    {
        var b = WithSend();
        Assert.Equal(new[] { Keys.Enter }, b[Send].Keys);
        Assert.Empty(b[new ActionId("Nope")].Keys);
    }

    [Fact]
    public void Export_keeps_only_changes_under_the_action_name()
    {
        var b = WithSend();
        Assert.Empty(b.Export());
        b.RebindKey(Send, Keys.F5);
        var saved = b.Export();
        Assert.Equal(new[] { "Send" }, saved.Keys);
        Assert.Equal(new List<string> { "F5" }, saved["Send"].Keys);
        Assert.Null(saved["Send"].Buttons);
    }

    [Fact]
    public void Apply_restores_saved_changes_and_skips_unknown_actions()
    {
        var b = WithSend();
        b.Apply(new Dictionary<string, SavedBinding>
        {
            ["Send"] = new SavedBinding { Keys = new List<string> { "F5", "NotAKey" } },
            ["Gone"] = new SavedBinding { Keys = new List<string> { "A" } },
        });
        Assert.Equal(new[] { Keys.F5 }, b[Send].Keys);
        Assert.Empty(b[new ActionId("Gone")].Keys);
    }

    [Fact]
    public void ResetAll_goes_back_to_the_defaults_including_game_actions()
    {
        var b = WithSend();
        b.RebindKey(Send, Keys.F5);
        b.RebindKey(GameAction.Select, Keys.Q);
        b.ResetAll();
        Assert.Equal(new[] { Keys.Enter }, b[Send].Keys);
        Assert.Contains(Keys.Space, b[GameAction.Select].Keys);
    }

    [Fact]
    public void Game_actions_convert_to_ids_by_name() =>
        Assert.Equal("Select", ((ActionId)GameAction.Select).Name);
}
