namespace VectorEngine.Screens;

/// <summary>
/// Implemented by a screen that keeps translated text it read when it was built (a radial menu's segment names, a title) and so
/// must read it again when the player changes the language in Settings. The screen manager calls <see cref="OnLanguageChanged"/>
/// on every screen on the stack that implements this, right after the new culture is set. A screen that looks its text up each
/// time it draws (the Settings and Levels screens) needs nothing, and a screen opened after the change reads the new language itself.
/// </summary>
public interface ILanguageAware
{
    void OnLanguageChanged();
}
