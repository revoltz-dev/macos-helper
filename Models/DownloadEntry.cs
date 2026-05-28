namespace MacOSHelper.Models;

public enum DownloadStatus { Idle, Downloading, Paused, Completed, Failed, Cancelled }

public class DownloadEntry
{
    public string ProductId { get; init; } = "";
    public string ProductTitle { get; init; } = "";
    public string DestFolder { get; init; } = "";
    public List<ProductPackage> Packages { get; init; } = new();

    public int CurrentPackageIndex { get; set; }
    public string CurrentFileName { get; set; } = "";
    public long CurrentFileBytes;
    public long CurrentFileTotal { get; set; }
    public long TotalDownloadedBytes;
    public long TotalBytes { get; set; }

    public DownloadStatus Status { get; set; } = DownloadStatus.Idle;
    public string? ErrorMessage { get; set; }
    public CancellationTokenSource Cts { get; } = new();

    public float OverallProgress => TotalBytes > 0
        ? Math.Clamp((float)TotalDownloadedBytes / TotalBytes, 0f, 1f) : 0f;

    public float CurrentFileProgress => CurrentFileTotal > 0
        ? Math.Clamp((float)CurrentFileBytes / CurrentFileTotal, 0f, 1f) : 0f;

    public double SpeedBytesPerSec { get; set; }
    public DateTime LastSpeedSample { get; set; } = DateTime.UtcNow;
    public long LastSpeedBytes { get; set; }
}
