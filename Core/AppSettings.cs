using System.Text.Json;

namespace MacOSHelper.Core;

public sealed class AppSettings
{
    public string Language { get; set; } = "en";

    private static string Path =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MacOSHelper",
            "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var json = File.ReadAllText(Path);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(Path)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path, json);
        }
        catch { }
    }
}
