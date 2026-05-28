namespace MacOSHelper.Models;

public class MacOsProduct
{
    public string ProductId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Version { get; set; } = "";
    public string Build { get; set; } = "";
    public long TotalSizeBytes { get; set; }
    public DateTime PostDate { get; set; }
    public List<ProductPackage> Packages { get; set; } = new();
    public string DistributionUrl { get; set; } = "";
    public string MetadataUrl { get; set; } = "";

    public int MajorVersion
    {
        get
        {
            if (Version.StartsWith("10."))
            {
                var parts = Version.Split('.');
                return 10;
            }
            return int.TryParse(Version.Split('.')[0], out var major) ? major : 0;
        }
    }

    public int MinorVersion
    {
        get
        {
            if (Version.StartsWith("10."))
            {
                var parts = Version.Split('.');
                return parts.Length >= 2 && int.TryParse(parts[1], out var minor) ? minor : 0;
            }
            return 0;
        }
    }

    public string SizeDisplay
    {
        get
        {
            double gb = TotalSizeBytes / (1024.0 * 1024 * 1024);
            return gb >= 1 ? $"{gb:F2} GB" : $"{TotalSizeBytes / (1024.0 * 1024):F0} MB";
        }
    }
}

public class ProductPackage
{
    public string Url { get; set; } = "";
    public long Size { get; set; }
    public string Digest { get; set; } = ""; // SHA1 hex do catálogo (opcional)
    public string FileName => Path.GetFileName(Url.Split('?')[0]);
}
