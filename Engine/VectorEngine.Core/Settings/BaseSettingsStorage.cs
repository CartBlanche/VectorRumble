using System;
using System.IO;
using System.Text.Json;

namespace VectorEngine.Settings;

public abstract class BaseSettingsStorage : ISettingsStorage
{
    /// <param name="appName">The game's own folder name inside the platform's settings folder, so games do not share files.</param>
    /// <param name="specialFolder">Which platform folder holds the settings.</param>
    protected BaseSettingsStorage(string appName, Environment.SpecialFolder specialFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        AppName = appName;
        SpecialFolderPath = specialFolder;
        SettingsFileName = "settings.json"; // Default settings file name
    }

    /// <summary>The game's folder name inside <see cref="SpecialFolderPath"/>.</summary>
    public string AppName { get; }

    protected Environment.SpecialFolder SpecialFolderPath { get; }

    private string settingsFileName;
    public string SettingsFileName
    {
        get => settingsFileName;

        set {
            if (settingsFileName != value)
            {
                settingsFileName = value;
            }
        }
    }
    protected string SettingsFilePath => Path.Combine(Environment.GetFolderPath(SpecialFolderPath), AppName, SettingsFileName);

    public virtual void SaveSettings<T>(T settings) where T : new()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(settings, options);

        // Ensure that the directory exists
        string directoryPath = Path.GetDirectoryName(SettingsFilePath);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.WriteAllText(SettingsFilePath, jsonString);
    }

    public virtual T LoadSettings<T>() where T : new()
    {
        if (!File.Exists(SettingsFilePath))
            return new T();

        string jsonString = File.ReadAllText(SettingsFilePath);
        return JsonSerializer.Deserialize<T>(jsonString) ?? new T();
    }
}
