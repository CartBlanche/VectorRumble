using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using VectorEngine.Input;
using Xunit;

namespace VectorEngine.Tests;

public class InputStateTests
{
    private static InputState Pressing(params Keys[] keys)
    {
        var input = new InputState();
        input.CurrentKeyboardStates[0] = new KeyboardState(keys);
        return input;
    }

    [Fact]
    public void A_newly_pressed_bound_key_triggers_its_action()
    {
        var input = Pressing(Keys.Enter);
        Assert.True(input.MenuSelect);
        Assert.False(input.MenuCancel);
    }

    [Fact]
    public void A_key_that_was_already_down_is_not_a_new_press()
    {
        var input = Pressing(Keys.Enter);
        input.LastKeyboardStates[0] = new KeyboardState(Keys.Enter);
        Assert.False(input.MenuSelect);
        Assert.True(input.IsHeld(GameAction.Select));
    }

    [Fact]
    public void Rebinding_changes_what_triggers_an_action()
    {
        var input = Pressing(Keys.Q);
        Assert.False(input.MenuUp);
        input.Bindings.RebindKey(GameAction.Up, Keys.Q);
        Assert.True(input.MenuUp);
    }

    [Fact]
    public void A_game_defined_action_is_read_like_a_core_one()
    {
        var send = new ActionId("Send");
        var input = Pressing(Keys.F5);
        input.Bindings.Define(send, new[] { Keys.F5 }, new Buttons[0]);
        Assert.True(input.IsPressed(send));
    }

    [Fact]
    public void The_pointer_reads_in_the_games_units_through_the_transformation()
    {
        var input = new InputState();
        input.UpdateInputTransformation(Matrix.CreateTranslation(-100, -50, 0) * Matrix.CreateScale(0.5f));
        Assert.Equal(new Vector2(50, 25), input.TransformCursorLocation(new Vector2(200, 100)));
    }
}
