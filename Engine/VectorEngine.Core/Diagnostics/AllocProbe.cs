using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace VectorEngine.Diagnostics;

/// <summary>
/// Where the allocations and the stalls are (D-099): between <see cref="Begin"/> and <see cref="End"/> it adds up the bytes this thread allocated per
/// (type, what) and remembers the slowest single call, so the screen manager can charge each screen's Update, Input and Draw. Off unless a <c>--measure</c>
/// run turns it on, and then costs two thread-counter reads and a timestamp per call. A label is a type plus a short tag, so no string is built while counting.
/// </summary>
public static class AllocProbe
{
    public static bool Enabled;

    public readonly record struct Mark(long Bytes, long Ticks);

    private static readonly Dictionary<(Type Type, string Tag), long> bytes = new();
    private static readonly Dictionary<(Type Type, string Tag), double> slowestMs = new();

    public static Mark Begin() => Enabled ? new Mark(GC.GetAllocatedBytesForCurrentThread(), Stopwatch.GetTimestamp()) : default;

    public static void End(Type type, string tag, Mark start)
    {
        if (!Enabled)
            return;
        long spent = GC.GetAllocatedBytesForCurrentThread() - start.Bytes;
        double ms = (Stopwatch.GetTimestamp() - start.Ticks) * 1000.0 / Stopwatch.Frequency;
        var key = (type, tag);
        bytes[key] = bytes.GetValueOrDefault(key) + spent;
        if (ms > slowestMs.GetValueOrDefault(key))
            slowestMs[key] = ms;
    }

    public static void Reset()
    {
        bytes.Clear();
        slowestMs.Clear();
    }

    public static string Top(int count) =>
        string.Join(", ", bytes.OrderByDescending(b => b.Value).Take(count).Where(b => b.Value > 0).Select(b => $"{b.Key.Type.Name}.{b.Key.Tag} {b.Value / 1024} KB"));

    /// <summary>The slowest single calls (milliseconds), to see which screen's Update or Draw a stall was in.</summary>
    public static string Slowest(int count) =>
        string.Join(", ", slowestMs.OrderByDescending(b => b.Value).Take(count).Select(b => $"{b.Key.Type.Name}.{b.Key.Tag} {b.Value:0.0} ms"));
}
