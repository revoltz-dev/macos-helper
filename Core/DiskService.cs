using System.Management;
using MacOSHelper.Models;

namespace MacOSHelper.Core;

public static class DiskService
{
    public static List<DiskInfo> GetRemovableDisks()
    {
        var disks = new Dictionary<int, DiskInfo>();

        try
        {
            using var diskQuery = new ManagementObjectSearcher(
                "SELECT DeviceID, Model, Size, MediaType, InterfaceType FROM Win32_DiskDrive WHERE InterfaceType = 'USB'");

            foreach (ManagementObject disk in diskQuery.Get())
            {
                var deviceId = disk["DeviceID"]?.ToString() ?? "";
                var model    = disk["Model"]?.ToString() ?? "Unknown";
                var sizeStr  = disk["Size"]?.ToString() ?? "0";
                var media    = disk["MediaType"]?.ToString() ?? "";

                long.TryParse(sizeStr, out var size);

                var idxMatch = System.Text.RegularExpressions.Regex.Match(deviceId, @"\d+$");
                if (!idxMatch.Success) continue;
                int diskIdx = int.Parse(idxMatch.Value);

                bool isRemovable = media.Contains("Removable", StringComparison.OrdinalIgnoreCase)
                                || media == "2" || size < 256L * 1024 * 1024 * 1024;

                disks[diskIdx] = new DiskInfo
                {
                    DiskIndex   = diskIdx,
                    DeviceId    = deviceId,
                    Model       = model,
                    SizeBytes   = size,
                    IsRemovable = isRemovable,
                    Partitions  = new List<PartitionInfo>()
                };
            }

            using var partMap = new ManagementObjectSearcher(
                "SELECT Antecedent, Dependent FROM Win32_DiskDriveToDiskPartition");

            var driveLetterMap = new Dictionary<string, (string letter, string name, string fs)>();

            using var logicalMap = new ManagementObjectSearcher(
                "SELECT Antecedent, Dependent FROM Win32_LogicalDiskToPartition");
            using var logicalQuery = new ManagementObjectSearcher(
                "SELECT DeviceID, VolumeName, FileSystem, Size FROM Win32_LogicalDisk");

            var logicalDisks = new Dictionary<string, (string name, string fs, long size)>();
            foreach (ManagementObject ld in logicalQuery.Get())
            {
                var dev  = ld["DeviceID"]?.ToString() ?? "";
                var name = ld["VolumeName"]?.ToString() ?? "";
                var fs   = ld["FileSystem"]?.ToString() ?? "";
                long.TryParse(ld["Size"]?.ToString() ?? "0", out var sz);
                logicalDisks[dev] = (name, fs, sz);
            }

            var partToLetter = new Dictionary<string, string>();
            foreach (ManagementObject lm in logicalMap.Get())
            {
                var ant = lm["Antecedent"]?.ToString() ?? "";
                var dep = lm["Dependent"]?.ToString() ?? "";
                var letter = System.Text.RegularExpressions.Regex.Match(dep, @"""([A-Z]:)""").Groups[1].Value;
                if (!string.IsNullOrEmpty(letter))
                    partToLetter[ant] = letter;
            }

            foreach (ManagementObject pm in partMap.Get())
            {
                var antecedent = pm["Antecedent"]?.ToString() ?? "";
                var dependent  = pm["Dependent"]?.ToString() ?? "";

                var driveMatch = System.Text.RegularExpressions.Regex.Match(
                    antecedent, @"PHYSICALDRIVE(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!driveMatch.Success) continue;
                int di = int.Parse(driveMatch.Groups[1].Value);
                if (!disks.ContainsKey(di)) continue;

                var partMatch = System.Text.RegularExpressions.Regex.Match(dependent, @"Partition #(\d+)");
                int partIdx = partMatch.Success ? int.Parse(partMatch.Groups[1].Value) : 0;

                string? driveLetter = partToLetter.TryGetValue(dependent, out var dl) ? dl : null;
                string? volName = null;
                string? fsType = null;
                long partSize = 0;

                if (driveLetter != null && logicalDisks.TryGetValue(driveLetter, out var ldInfo))
                {
                    volName  = ldInfo.name;
                    fsType   = ldInfo.fs;
                    partSize = ldInfo.size;
                }

                disks[di].Partitions.Add(new PartitionInfo
                {
                    PartitionIndex = partIdx,
                    DriveLetter    = driveLetter,
                    VolumeName     = volName,
                    FileSystem     = fsType,
                    SizeBytes      = partSize
                });

                if (driveLetter != null)
                {
                    var driveInfo = DriveInfo.GetDrives()
                        .FirstOrDefault(d => d.Name.StartsWith(driveLetter, StringComparison.OrdinalIgnoreCase));
                    if (driveInfo?.DriveType == DriveType.Removable)
                        disks[di].IsRemovable = true;
                }
            }
        }
        catch { }

        return disks.Values
            .OrderBy(d => d.DiskIndex)
            .ToList();
    }
}
