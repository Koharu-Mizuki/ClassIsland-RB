using System.IO;
using System.Text.Json;
using ClassIsland.OcrAiTimetable.Models;

namespace ClassIsland.OcrAiTimetable.Services;

public class OcrAiSettingsService
{
    private readonly string _settingsPath;

    public OcrAiSettings Settings { get; }

    public OcrAiSettingsService(string pluginConfigFolder)
    {
        _settingsPath = Path.Combine(pluginConfigFolder, "settings.json");
        Directory.CreateDirectory(pluginConfigFolder);

        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                Settings = JsonSerializer.Deserialize<OcrAiSettings>(json) ?? new OcrAiSettings();
            }
            catch
            {
                Settings = new OcrAiSettings();
            }
        }
        else
        {
            Settings = new OcrAiSettings();
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }
}
