using System;

namespace VectorEngine;

/// <summary>What kind of device the game is running on.</summary>
public static class Platform
{
    /// <summary>A phone or tablet (Android, iOS): touch input, full screen, the app's personal folder.</summary>
    public static readonly bool IsMobile = OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

    /// <summary>A computer (macOS, Linux, Windows): keyboard and mouse.</summary>
    public static readonly bool IsDesktop = OperatingSystem.IsMacOS() || OperatingSystem.IsLinux() || OperatingSystem.IsWindows();
}
