using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace VectorEngine.Input;

/// <summary>
/// The logical actions the engine's own screens (menus, message boxes) ask for. What triggers each one lives in
/// <see cref="ActionBindings"/>, so screens never name a key or a button. A game adds its own actions as
/// <see cref="ActionId"/>s (for example <c>new ActionId("Send")</c>) and defines their default keys with
/// <see cref="ActionBindings.Define"/>.
/// </summary>
public enum GameAction
{
    Select,
    Cancel,
    Pause,
    Up,
    Down,
    Next,
    Previous,
    /// <summary>The pad's click: with the pad pointer on, it presses and releases at the pad cursor like a mouse button.</summary>
    Click,
}

/// <summary>
/// The name of an action. The engine's <see cref="GameAction"/>s convert to one implicitly, and a game makes its own
/// with a name. The name is what the settings file and Inputs.xml keep, so renaming one changes the saved format.
/// </summary>
public readonly record struct ActionId(string Name)
{
    public static implicit operator ActionId(GameAction action) => new(action.ToString());

    public override string ToString() => Name;
}

/// <summary>Which way the mouse wheel has to turn to trigger an action.</summary>
public enum WheelDirection
{
    None,
    Up,
    Down,
}

/// <summary>
/// Everything bound to one action: any of its keys, gamepad buttons or a wheel turn triggers it.
/// </summary>
public class ActionBinding
{
    public readonly List<Keys> Keys = new();
    public readonly List<Buttons> Buttons = new();
    public WheelDirection Wheel = WheelDirection.None;
}

/// <summary>What a player rebound one action to. A list left null keeps the action's default keys or buttons.</summary>
public class SavedBinding
{
    public List<string> Keys { get; set; }
    public List<string> Buttons { get; set; }
}

/// <summary>
/// The action table: built in with the defaults, then replaced action by action from an Inputs.xml in the game's
/// content when that file lists them (a rebinding screen writes the same structure). A missing file or action keeps
/// the built-in bindings.
/// </summary>
public class ActionBindings
{
    private const string BindingsFile = "UI/Inputs.xml";

    private readonly Dictionary<ActionId, ActionBinding> table = new();

    // The bindings before the player's changes (built in, then Inputs.xml), for Reset and for saving only what changed
    private Dictionary<ActionId, ActionBinding> defaults = new();

    public ActionBindings()
    {
        Define(GameAction.Select, new[] { Keys.Space, Keys.Enter }, new[] { Buttons.A, Buttons.Start });
        Define(GameAction.Cancel, new[] { Keys.Escape }, new[] { Buttons.B, Buttons.Back });
        Define(GameAction.Pause, new[] { Keys.Escape }, new[] { Buttons.Back, Buttons.Start });
        Define(GameAction.Up, new[] { Keys.Up }, new[] { Buttons.DPadUp, Buttons.LeftThumbstickUp }, WheelDirection.Up);
        Define(GameAction.Down, new[] { Keys.Down }, new[] { Buttons.DPadDown, Buttons.LeftThumbstickDown }, WheelDirection.Down);
        Define(GameAction.Next, new[] { Keys.Right }, new[] { Buttons.DPadRight, Buttons.LeftThumbstickRight });
        Define(GameAction.Previous, new[] { Keys.Left }, new[] { Buttons.DPadLeft, Buttons.LeftThumbstickLeft });
        Define(GameAction.Click, new[] { Keys.Space }, new[] { Buttons.A });
    }

    /// <summary>
    /// Adds an action with its built-in keys and buttons (or replaces the built-in ones). These are also what
    /// <see cref="ResetAll"/> goes back to.
    /// </summary>
    public void Define(ActionId action, Keys[] keys, Buttons[] buttons, WheelDirection wheel = WheelDirection.None)
    {
        var binding = new ActionBinding { Wheel = wheel };
        binding.Keys.AddRange(keys);
        binding.Buttons.AddRange(buttons);
        table[action] = binding;
        defaults[action] = Copy(binding);
    }

    private Dictionary<ActionId, ActionBinding> Snapshot() => table.ToDictionary(p => p.Key, p => Copy(p.Value));

    private static ActionBinding Copy(ActionBinding binding)
    {
        var copy = new ActionBinding { Wheel = binding.Wheel };
        copy.Keys.AddRange(binding.Keys);
        copy.Buttons.AddRange(binding.Buttons);
        return copy;
    }

    /// <summary>Replace an action's keyboard keys with one key (the player's choice in Settings, Controls).</summary>
    public void RebindKey(ActionId action, Keys key)
    {
        var binding = Edit(action);
        binding.Keys.Clear();
        binding.Keys.Add(key);
    }

    /// <summary>Replace an action's gamepad buttons with one button.</summary>
    public void RebindButton(ActionId action, Buttons button)
    {
        var binding = Edit(action);
        binding.Buttons.Clear();
        binding.Buttons.Add(button);
    }

    private ActionBinding Edit(ActionId action)
    {
        if (!table.TryGetValue(action, out var binding))
            table[action] = binding = new ActionBinding();
        return binding;
    }

    /// <summary>Put every action back to its default keys and buttons.</summary>
    public void ResetAll()
    {
        table.Clear();
        foreach (var pair in defaults)
            table[pair.Key] = Copy(pair.Value);
    }

    /// <summary>The player's changes only, in the form the settings file keeps.</summary>
    public Dictionary<string, SavedBinding> Export()
    {
        var saved = new Dictionary<string, SavedBinding>();
        foreach (var pair in table)
        {
            defaults.TryGetValue(pair.Key, out var original);
            var entry = new SavedBinding();
            bool changed = false;
            if (original == null || !pair.Value.Keys.SequenceEqual(original.Keys))
            {
                entry.Keys = pair.Value.Keys.Select(k => k.ToString()).ToList();
                changed = true;
            }
            if (original == null || !pair.Value.Buttons.SequenceEqual(original.Buttons))
            {
                entry.Buttons = pair.Value.Buttons.Select(b => b.ToString()).ToList();
                changed = true;
            }
            if (changed)
                saved[pair.Key.Name] = entry;
        }
        return saved;
    }

    /// <summary>Apply the player's saved changes on top of the defaults. Names that no longer parse, or actions this game no longer has, are skipped.</summary>
    public void Apply(Dictionary<string, SavedBinding> saved)
    {
        if (saved == null)
            return;

        foreach (var pair in saved)
        {
            var action = new ActionId(pair.Key);
            if (!defaults.ContainsKey(action) || pair.Value == null)
                continue;

            var binding = Edit(action);
            if (pair.Value.Keys != null)
            {
                binding.Keys.Clear();
                foreach (var name in pair.Value.Keys)
                    if (Enum.TryParse(name, out Keys key))
                        binding.Keys.Add(key);
            }
            if (pair.Value.Buttons != null)
            {
                binding.Buttons.Clear();
                foreach (var name in pair.Value.Buttons)
                    if (Enum.TryParse(name, out Buttons button))
                        binding.Buttons.Add(button);
            }
        }
    }

    /// <summary>The binding for an action (empty if nothing is bound).</summary>
    public ActionBinding this[ActionId action] =>
        table.TryGetValue(action, out var binding) ? binding : new ActionBinding();

    /// <summary>
    /// Replace the built-in bindings with those in Content/UI/Inputs.xml, one action at a time. Safe to call without the file.
    /// </summary>
    public void Load(string contentRoot)
    {
        try
        {
            using Stream stream = TitleContainer.OpenStream(Path.Combine(contentRoot, BindingsFile));
            foreach (var element in XDocument.Load(stream).Root.Elements("Action"))
            {
                var action = new ActionId(element.Attribute("Name")?.Value ?? string.Empty);
                if (!defaults.ContainsKey(action))
                {
                    Debug.WriteLine($"Inputs: unknown action '{action.Name}' ignored");
                    continue;
                }

                var binding = new ActionBinding();
                foreach (var name in Split(element.Attribute("Keys")?.Value))
                {
                    if (Enum.TryParse(name, out Keys key))
                        binding.Keys.Add(key);
                    else
                        Debug.WriteLine($"Inputs: unknown key '{name}' for {action} ignored");
                }
                foreach (var name in Split(element.Attribute("Buttons")?.Value))
                {
                    if (Enum.TryParse(name, out Buttons button))
                        binding.Buttons.Add(button);
                    else
                        Debug.WriteLine($"Inputs: unknown button '{name}' for {action} ignored");
                }
                if (Enum.TryParse(element.Attribute("Wheel")?.Value, out WheelDirection wheel))
                    binding.Wheel = wheel;

                table[action] = binding;
            }
            defaults = Snapshot();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load input bindings from '{BindingsFile}': {ex.Message}");
        }
    }

    private static IEnumerable<string> Split(string list) =>
        (list ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
