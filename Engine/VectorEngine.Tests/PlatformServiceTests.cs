using System.Collections.Generic;
using System.Globalization;
using VectorEngine.Localization;
using VectorEngine.Rendering;
using VectorEngine.Settings;
using Xunit;

namespace VectorEngine.Tests;

public class PlatformServiceTests
{
    private sealed class Memory : ISettingsStorage
    {
        public string SettingsFileName { get; set; } = "memory";
        public object Saved;
        public void SaveSettings<T>(T settings) where T : new() => Saved = settings;
        public T LoadSettings<T>() where T : new() => Saved is T t ? t : new T();
    }

    private sealed class Prefs { public int Volume = 50; }

    [Fact]
    public void SettingsManager_loads_defaults_then_round_trips_through_storage()
    {
        var storage = new Memory();
        var manager = new SettingsManager<Prefs>(storage);
        Assert.Equal(50, manager.Settings.Volume);
        manager.Settings.Volume = 80;
        manager.Save();
        manager.Load();
        Assert.Equal(80, manager.Settings.Volume);
    }

    [Fact]
    public void Settings_storage_needs_an_app_name() =>
        Assert.ThrowsAny<System.ArgumentException>(() => new DesktopSettingsStorage(" "));

    [Fact]
    public void Settings_storage_keeps_the_app_name() =>
        Assert.Equal("Rumble", new DesktopSettingsStorage("Rumble").AppName);

    [Fact]
    public void Resolve_prefers_the_saved_language_and_falls_back_to_invariant()
    {
        var supported = new List<CultureInfo> { new("de-DE"), new("fr-FR"), CultureInfo.InvariantCulture };
        Assert.Equal("fr-FR", LocalizationManager.Resolve("fr-FR", supported).Name);
        Assert.Equal(CultureInfo.InvariantCulture, LocalizationManager.Resolve("xx-YY", supported));
    }

    [Fact]
    public void Display_defaults_to_720p_and_can_be_reconfigured()
    {
        try
        {
            Assert.Equal(1280, Display.BaseWidth);
            Assert.Equal(720, Display.BaseHeight);
            Display.Configure(1920, 1080);
            Display.Update(1920, 1080);
            Assert.Equal(1920, Display.BaseWidth);
            Assert.Equal(1f, Display.Scale, 3);
        }
        finally { Display.Configure(Display.DefaultWidth, Display.DefaultHeight); }
    }
}
