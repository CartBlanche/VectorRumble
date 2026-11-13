using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace VectorEngine.Audio;

/// <summary>
/// The one-shot sound clips that sit loose in Content (the cutscene sounds, the drone, the chimes): each is read and decoded once and the
/// decoded <see cref="SoundEffect"/> kept, so playing it again costs nothing. <c>SoundEffect.FromStream</c> on every play re-read the file and
/// decoded it, a stall of tens of milliseconds for a clip of a megabyte, and the effect was never disposed. A clip that cannot be loaded (no file, no
/// audio device) is remembered as missing, so the failure is logged once. The cache stops growing at <see cref="BudgetBytes"/> of files; clips
/// past that are loaded as before (not kept). Effects are shared: never dispose one, and make an instance with <c>CreateInstance()</c> to play it with
/// its own volume or loop.
/// </summary>
public static class ClipCache
{
    private const long BudgetBytes = 64L * 1024 * 1024;

    private static readonly Dictionary<string, SoundEffect> cache = new(StringComparer.Ordinal);
    private static readonly HashSet<string> missing = new(StringComparer.Ordinal);
    private static long bytes;

    /// <summary>The clip at <c>Content/&lt;path&gt;</c> (for example <c>Sounds/Cutscene/ending_bond.wav</c>), or null if it cannot be loaded.</summary>
    public static SoundEffect Get(string path)
    {
        if (cache.TryGetValue(path, out var effect))
            return effect;
        if (missing.Contains(path))
            return null;
        try
        {
            using Stream stream = TitleContainer.OpenStream(Path.Combine("Content", path));
            long length = stream.Length;
            effect = SoundEffect.FromStream(stream);
            if (bytes + length <= BudgetBytes)
            {
                cache[path] = effect;
                bytes += length;
            }
            return effect;
        }
        catch (Exception ex)
        {
            missing.Add(path);
            System.Diagnostics.Debug.WriteLine($"Clip {path}: {ex.Message}");
            return null;
        }
    }
}
