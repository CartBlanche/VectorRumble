namespace VectorEngine.Audio;

/// <summary>
/// The player's two volume settings (Settings screen), as 0 to 1. ScreenManager sets them every frame from the saved settings; each
/// sound source multiplies its own level by the one it belongs to, so a change is heard at once. Music is the sneaking music and the
/// main menu's cue; Sfx is everything else: the level's ambience, cutscene clips, the drone, the call ring and the voices.
/// </summary>
public static class AudioLevels
{
    public static float Music { get; set; } = 1f;
    public static float Sfx { get; set; } = 1f;
}
