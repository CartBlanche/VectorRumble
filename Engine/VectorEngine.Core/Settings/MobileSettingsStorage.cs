using System;

namespace VectorEngine.Settings;

/// <summary>Settings stored under the app's personal folder (Android, iOS), in a folder named after the game.</summary>
public class MobileSettingsStorage : BaseSettingsStorage
{
    public MobileSettingsStorage(string appName) : base(appName, Environment.SpecialFolder.Personal)
    {
    }
}
