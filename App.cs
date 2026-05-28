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
    public string LoadingStatus   { get; set; } = "Iniciando...";
    public float  LoadingProgress { get; set; } = 0f;
}
