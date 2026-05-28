using MacOSHelper.Helpers;
using MacOSHelper.Models;

namespace MacOSHelper.Core;

public enum CatalogSeed { PublicRelease, PublicBeta, CustomerSeed, DeveloperBeta }

public class CatalogService
{
    // URL atual (maio/2026): inclui macOS 26 Tahoe, 15 Sequoia, 14 Sonoma, 13 Ventura,
    // 12 Monterey, 11 Big Sur (10.16), 10.15 Catalina e versões anteriores até Leopard.
    // .gz é descompactado automaticamente pelo HttpClient (AutomaticDecompression = All).
    private const string CatalogBase =
        "https://swscan.apple.com/content/catalogs/others/" +
        "index-26-15-14-13-12-10.16-10.15-10.14-10.13-10.12-10.11-10.10-10.9-" +
        "mountainlion-lion-snowleopard-leopard";

    private static string CatalogUrlFor(CatalogSeed seed) => seed switch
    {
        CatalogSeed.PublicBeta    => CatalogBase + ".merged-1.sucatalog.gz",            // mesmo URL — Apple usa seedprogram pra beta
        CatalogSeed.CustomerSeed  => CatalogBase + ".merged-1-customerseed.sucatalog.gz",
        CatalogSeed.DeveloperBeta => CatalogBase + ".merged-1-seed-1.sucatalog.gz",
        _                         => CatalogBase + ".merged-1.sucatalog.gz",
    };

    private static readonly HttpClient _http = new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    })
    {
        Timeout = TimeSpan.FromSeconds(60),
        DefaultRequestHeaders = { { "User-Agent", "Mozilla/5.0 macOS" } }
    };

    private static string VersionToName(string ver)
    {
        if (ver.StartsWith("10."))
        {
            var minor = ver.Split('.').ElementAtOrDefault(1) ?? "";
            return minor switch
            {
                "9"  => "macOS Mavericks",
                "10" => "macOS Yosemite",
                "11" => "macOS El Capitan",
                "12" => "macOS Sierra",
                "13" => "macOS High Sierra",
                "14" => "macOS Mojave",
                "15" => "macOS Catalina",
                "16" => "macOS Big Sur",
                _    => "macOS 10." + minor
            };
        }
        return ver switch
        {
            var v when v.StartsWith("11") => "macOS Big Sur",
            var v when v.StartsWith("12") => "macOS Monterey",
            var v when v.StartsWith("13") => "macOS Ventura",
            var v when v.StartsWith("14") => "macOS Sonoma",
            var v when v.StartsWith("15") => "macOS Sequoia",
            var v when v.StartsWith("26") => "macOS Tahoe",
            _ => "macOS " + ver
        };
    }

    public async Task<List<MacOsProduct>> FetchProductsAsync(
        IProgress<string>? progress = null,
        CatalogSeed seed = CatalogSeed.PublicRelease,
        CancellationToken ct = default)
    {
        var url = CatalogUrlFor(seed);
        progress?.Report($"Baixando catálogo ({seed})...");
        var xml = await _http.GetStringAsync(url, ct);

        progress?.Report("Analisando catálogo...");
        var all = PlistParser.ParseSucatalog(xml);

        var candidates = all
            .Where(p => p.Packages.Any(pkg =>
                pkg.Url.Contains("InstallAssistant", StringComparison.OrdinalIgnoreCase) ||
                pkg.Url.Contains("BaseSystem", StringComparison.OrdinalIgnoreCase) ||
                pkg.Url.Contains("InstallESD", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        progress?.Report($"Encontrados {candidates.Count} instaladores. Buscando metadados...");

        var sem = new SemaphoreSlim(8);
        var tasks = candidates.Select(async product =>
        {
            await sem.WaitAsync(ct);
            try { await EnrichProductAsync(product, ct); }
            finally { sem.Release(); }
        });
        await Task.WhenAll(tasks);

        return candidates
            .Where(p => !string.IsNullOrEmpty(p.Title))
            .OrderByDescending(p => p.PostDate)
            .ToList();
    }

    private async Task EnrichProductAsync(MacOsProduct product, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(product.DistributionUrl))
        {
            try
            {
                var distXml = await _http.GetStringAsync(product.DistributionUrl, ct);
                var (build, version, title) = PlistParser.ParseDist(distXml);
                if (!string.IsNullOrEmpty(build))   product.Build   = build;
                if (!string.IsNullOrEmpty(version)) product.Version = version;
                if (!string.IsNullOrEmpty(title))   product.Title   = title;
            }
            catch { }
        }

        if ((string.IsNullOrEmpty(product.Title) || string.IsNullOrEmpty(product.Version))
            && !string.IsNullOrEmpty(product.MetadataUrl))
        {
            try
            {
                var smdXml = await _http.GetStringAsync(product.MetadataUrl, ct);
                var (title, version) = PlistParser.ParseSmd(smdXml);
                if (string.IsNullOrEmpty(product.Title)   && !string.IsNullOrEmpty(title))   product.Title   = title;
                if (string.IsNullOrEmpty(product.Version) && !string.IsNullOrEmpty(version)) product.Version = version;
            }
            catch { }
        }

        if (string.IsNullOrEmpty(product.Title) && !string.IsNullOrEmpty(product.Version))
            product.Title = VersionToName(product.Version);

        if (string.IsNullOrEmpty(product.Title))
            product.Title = "macOS (" + product.ProductId + ")";
    }
}
