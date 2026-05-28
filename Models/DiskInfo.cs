namespace MacOSHelper.Models;

public class DiskInfo
{
    public int DiskIndex { get; set; }
    public string DeviceId { get; set; } = "";
    public string Model { get; set; } = "";
    public long SizeBytes { get; set; }
    public bool IsRemovable { get; set; }
    public List<PartitionInfo> Partitions { get; set; } = new();

    public string SizeDisplay
    {
        get
        {
            double gb = SizeBytes / (1024.0 * 1024 * 1024);
            return gb >= 1 ? $"{gb:F1} GB" : $"{SizeBytes / (1024.0 * 1024):F0} MB";
        }
    }

    public string DisplayName => $"{Model} ({SizeDisplay}){(IsRemovable ? " [Removível]" : "")}";
}

public class PartitionInfo
{
    public int PartitionIndex { get; set; }
    public string? DriveLetter { get; set; }
    public string? VolumeName { get; set; }
    public string? FileSystem { get; set; }
    public long SizeBytes { get; set; }
}
