using System.Xml;
using MacOSHelper.Models;

namespace MacOSHelper.Helpers;

public static class PlistParser
{
    public static List<MacOsProduct> ParseSucatalog(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var products = new List<MacOsProduct>();

        var rootDict = doc.SelectSingleNode("/plist/dict") as XmlElement;
        if (rootDict == null) return products;

        var productsDict = GetDictForKey(rootDict, "Products");
        if (productsDict == null) return products;

        var children = productsDict.ChildNodes;
        for (int i = 0; i < children.Count - 1; i += 2)
        {
            if (children[i]?.Name != "key") continue;
            var productId = children[i]!.InnerText.Trim();
            if (children[i + 1] is not XmlElement productDict) continue;

            var product = ParseProductDict(productId, productDict);
            if (product != null)
                products.Add(product);
        }

        return products;
    }

    private static MacOsProduct? ParseProductDict(string productId, XmlElement dict)
    {
        var product = new MacOsProduct { ProductId = productId };

        product.MetadataUrl = GetStringForKey(dict, "ServerMetadataURL") ?? "";

        var distDict = GetDictForKey(dict, "Distributions");
        if (distDict != null)
        {
            product.DistributionUrl =
                GetStringForKey(distDict, "English") ??
                GetStringForKey(distDict, "en") ?? "";
        }

        var dateStr = GetDateForKey(dict, "PostDate");
        if (dateStr != null && DateTime.TryParse(dateStr, out var dt))
            product.PostDate = dt;

        var pkgsArray = GetArrayForKey(dict, "Packages");
        if (pkgsArray != null)
        {
            foreach (XmlElement pkgDict in pkgsArray.ChildNodes.OfType<XmlElement>().Where(e => e.Name == "dict"))
            {
                var url = GetStringForKey(pkgDict, "URL") ?? "";
                var sizeStr = GetStringForKey(pkgDict, "Size") ?? "0";
                var digest = GetStringForKey(pkgDict, "Digest") ?? "";
                long.TryParse(sizeStr, out var size);
                if (!string.IsNullOrEmpty(url))
                {
                    product.Packages.Add(new ProductPackage
                    {
                        Url    = url,
                        Size   = size,
                        Digest = digest
                    });
                    product.TotalSizeBytes += size;
                }
            }
        }

        if (product.Packages.Count == 0) return null;

        return product;
    }

    public static (string title, string version) ParseSmd(string xml)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var root = doc.SelectSingleNode("/plist/dict") as XmlElement;
            if (root == null) return ("", "");

            var title = GetStringForKey(root, "localization")
                ?? GetStringForKey(root, "CFBundleName") ?? "";
            var version = GetStringForKey(root, "CFBundleShortVersionString") ?? "";

            if (string.IsNullOrEmpty(title))
            {
                var locDict = GetDictForKey(root, "localization");
                if (locDict != null)
                {
                    var enDict = GetDictForKey(locDict, "English");
                    if (enDict != null)
                        title = GetStringForKey(enDict, "title") ?? "";
                }
            }

            return (title, version);
        }
        catch { return ("", ""); }
    }

    public static (string build, string version, string title) ParseDist(string distXml)
    {
        try
        {
            string build = "", version = "", title = "";

            foreach (var line in distXml.Split('\n'))
            {
                var t = line.Trim();

                if (string.IsNullOrEmpty(build))
                {
                    if (t.Contains("BUILD") && t.Contains("="))
                    {
                        var b = ExtractQuoted(t);
                        if (!string.IsNullOrEmpty(b) && System.Text.RegularExpressions.Regex.IsMatch(b, @"^\d+[A-Z]\d+[a-z]?$"))
                            build = b;
                    }
                }

                if (string.IsNullOrEmpty(version))
                {
                    if (t.Contains("VERSION") && t.Contains("="))
                    {
                        var v = ExtractQuoted(t);
                        if (!string.IsNullOrEmpty(v) && System.Text.RegularExpressions.Regex.IsMatch(v, @"^\d+\.\d+"))
                            version = v;
                    }
                }
            }

            if (string.IsNullOrEmpty(build))
            {
                var m = System.Text.RegularExpressions.Regex.Match(distXml,
                    @"macOSProductBuildVersion[^<]*<[^>]+>([^<]+)<",
                    System.Text.RegularExpressions.RegexOptions.Singleline);
                if (m.Success) build = m.Groups[1].Value.Trim();
            }
            if (string.IsNullOrEmpty(version))
            {
                var m = System.Text.RegularExpressions.Regex.Match(distXml,
                    @"macOSProductVersion[^<]*<[^>]+>([^<]+)<",
                    System.Text.RegularExpressions.RegexOptions.Singleline);
                if (m.Success) version = m.Groups[1].Value.Trim();
            }

            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(distXml);

                var titleNode = doc.SelectSingleNode("//title");
                if (titleNode != null)
                {
                    var raw = titleNode.InnerText.Trim();
                    if (!string.IsNullOrEmpty(raw) && raw != "SU_TITLE")
                        title = raw;
                }

                if (string.IsNullOrEmpty(title))
                {
                    var suNode = doc.SelectSingleNode("//SU_TITLE");
                    if (suNode != null && !string.IsNullOrEmpty(suNode.InnerText))
                        title = suNode.InnerText.Trim();
                }

                if (string.IsNullOrEmpty(build))
                {
                    var nodes = doc.GetElementsByTagName("key");
                    foreach (XmlNode n in nodes)
                    {
                        if (n.InnerText?.Trim() == "BUILD" && n.NextSibling != null)
                            build = n.NextSibling.InnerText.Trim();
                        else if (n.InnerText?.Trim() == "macOSProductBuildVersion" && n.NextSibling != null)
                            build = n.NextSibling.InnerText.Trim();
                    }
                }
                if (string.IsNullOrEmpty(version))
                {
                    var nodes = doc.GetElementsByTagName("key");
                    foreach (XmlNode n in nodes)
                    {
                        if ((n.InnerText?.Trim() == "VERSION" || n.InnerText?.Trim() == "macOSProductVersion")
                            && n.NextSibling != null)
                            version = n.NextSibling.InnerText.Trim();
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(version) && !string.IsNullOrEmpty(build))
                version = BuildToVersion(build);

            return (build, version, title);
        }
        catch { return ("", "", ""); }
    }

    private static string BuildToVersion(string build)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(build, @"^\d+[A-Z]")) return "";
        var numStr = System.Text.RegularExpressions.Regex.Match(build, @"^\d+").Value;
        if (!int.TryParse(numStr, out int num)) return "";
        return num switch
        {
            8  => "10.4",
            9  => "10.5",
            10 => "10.6",
            11 => "10.7",
            12 => "10.8",
            13 => "10.9",
            14 => "10.10",
            15 => "10.11",
            16 => "10.12",
            17 => "10.13",
            18 => "10.14",
            19 => "10.15",
            20 => "11",
            21 => "12",
            22 => "13",
            23 => "14",
            24 => "15",
            25 => "26",
            _  => ""
        };
    }

    private static XmlElement? GetDictForKey(XmlElement parent, string key)
    {
        var children = parent.ChildNodes;
        for (int i = 0; i < children.Count - 1; i++)
        {
            if (children[i]?.Name == "key" && children[i]!.InnerText.Trim() == key)
                return children[i + 1] as XmlElement;
        }
        return null;
    }

    private static XmlElement? GetArrayForKey(XmlElement parent, string key)
    {
        var children = parent.ChildNodes;
        for (int i = 0; i < children.Count - 1; i++)
        {
            if (children[i]?.Name == "key" && children[i]!.InnerText.Trim() == key)
                return children[i + 1] as XmlElement;
        }
        return null;
    }

    private static string? GetStringForKey(XmlElement parent, string key)
    {
        var children = parent.ChildNodes;
        for (int i = 0; i < children.Count - 1; i++)
        {
            if (children[i]?.Name == "key" && children[i]!.InnerText.Trim() == key)
            {
                var next = children[i + 1];
                if (next?.Name is "string" or "integer" or "date")
                    return next.InnerText.Trim();
            }
        }
        return null;
    }

    private static string? GetDateForKey(XmlElement parent, string key)
        => GetStringForKey(parent, key);

    private static string ExtractQuoted(string s)
    {
        var idx = s.IndexOf('"');
        if (idx < 0) idx = s.IndexOf('\'');
        if (idx < 0) return "";
        char q = s[idx];
        var end = s.IndexOf(q, idx + 1);
        return end > idx ? s.Substring(idx + 1, end - idx - 1) : "";
    }
}
