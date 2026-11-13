using System;

namespace VectorEngine.Settings;

/// <summary>Settings stored under the user's roaming application-data folder, in a folder named after the game.</summary>
public class DesktopSettingsStorage : BaseSettingsStorage
{
    public DesktopSettingsStorage(string appName) : base(appName, Environment.SpecialFolder.ApplicationData)
    {
    }
}
