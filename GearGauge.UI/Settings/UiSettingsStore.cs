using System.Text.Json;
using System.IO;

namespace GearGauge.UI.Settings;

public sealed class UiSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public UiSettingsStore()
    {
        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GearGauge");
        Directory.CreateDirectory(settingsDirectory);
        _settingsPath = Path.Combine(settingsDirectory, "ui-settings.json");
    }

    public UiSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new UiSettings().Normalize();
            }

            var content = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<UiSettings>(content, SerializerOptions)?.Normalize()
                   ?? new UiSettings().Normalize();
        }
        catch
        {
            return new UiSettings().Normalize();
        }
    }

    public void Save(UiSettings settings)
    {
        var normalized = settings.Normalize();
        var content = JsonSerializer.Serialize(normalized, SerializerOptions);
        File.WriteAllText(_settingsPath, content);
    }
}
