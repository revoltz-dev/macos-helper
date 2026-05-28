using MacOSHelper.Core;
using MacOSHelper.Models;

namespace MacOSHelper;

public sealed class App
{
    public MacModel? DetectedModel { get; set; }
    public bool      ShowDetect    { get; set; }
    public bool      ShowCatalog   { get; set; }

    public string DownloadsDir { get; } =
        Path.Combine(AppContext.BaseDirectory, "Downloads");

    public bool   IsLoading       { get; set; } = true;
    public string LoadingStatus   { get; set; } = T.Initializing;
    public float  LoadingProgress { get; set; } = 0f;

    public AppSettings Settings { get; }

    public App()
    {
        Settings = AppSettings.Load();
        T.Current = Settings.Language == "en" ? Lang.En : Lang.Pt;
        LoadingStatus = T.Initializing;
    }

    public void SetLanguage(Lang lang)
    {
        if (T.Current == lang) return;
        T.Current = lang;
        Settings.Language = lang == Lang.En ? "en" : "pt";
        Settings.Save();
    }
}
