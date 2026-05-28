using System.Text.RegularExpressions;
using MacOSHelper.Models;

namespace MacOSHelper.Core;

public static class MacModelDatabase
{
    private static readonly Dictionary<string, MacModel> _db = new(StringComparer.OrdinalIgnoreCase)
    {
        ["macbookpro10,1"] = new("MacBookPro10,1", "MacBook Pro (Retina, 15\", Mid 2012)",   10, 13, "macOS High Sierra"),
        ["macbookpro10,2"] = new("MacBookPro10,2", "MacBook Pro (Retina, 13\", Late 2012)",   10, 13, "macOS High Sierra"),
        ["macbookpro11,1"] = new("MacBookPro11,1", "MacBook Pro (Retina, 13\", Late 2013)",   12, 0,  "macOS Monterey"),
        ["macbookpro11,2"] = new("MacBookPro11,2", "MacBook Pro (Retina, 15\", Late 2013)",   12, 0,  "macOS Monterey"),
        ["macbookpro11,3"] = new("MacBookPro11,3", "MacBook Pro (Retina, 15\", Late 2013)",   12, 0,  "macOS Monterey"),
        ["macbookpro11,4"] = new("MacBookPro11,4", "MacBook Pro (Retina, 15\", Mid 2014)",    12, 0,  "macOS Monterey"),
        ["macbookpro11,5"] = new("MacBookPro11,5", "MacBook Pro (Retina, 15\", Mid 2014)",    12, 0,  "macOS Monterey"),
        ["macbookpro12,1"] = new("MacBookPro12,1", "MacBook Pro (Retina, 13\", Early 2015)",  12, 0,  "macOS Monterey"),
        ["macbookpro13,1"] = new("MacBookPro13,1", "MacBook Pro (13\", Late 2016)",            13, 0,  "macOS Ventura"),
        ["macbookpro13,2"] = new("MacBookPro13,2", "MacBook Pro (13\", Late 2016 TB)",         13, 0,  "macOS Ventura"),
        ["macbookpro13,3"] = new("MacBookPro13,3", "MacBook Pro (15\", Late 2016)",            13, 0,  "macOS Ventura"),
        ["macbookpro14,1"] = new("MacBookPro14,1", "MacBook Pro (13\", 2017)",                 14, 0,  "macOS Sonoma"),
        ["macbookpro14,2"] = new("MacBookPro14,2", "MacBook Pro (13\", 2017 TB)",              14, 0,  "macOS Sonoma"),
        ["macbookpro14,3"] = new("MacBookPro14,3", "MacBook Pro (15\", 2017)",                 14, 0,  "macOS Sonoma"),
        ["macbookpro15,1"] = new("MacBookPro15,1", "MacBook Pro (15\", 2018)",                 15, 0,  "macOS Sequoia"),
        ["macbookpro15,2"] = new("MacBookPro15,2", "MacBook Pro (13\", 2018/2019)",            15, 0,  "macOS Sequoia"),
        ["macbookpro15,3"] = new("MacBookPro15,3", "MacBook Pro (15\", 2019 Vega)",            15, 0,  "macOS Sequoia"),
        ["macbookpro15,4"] = new("MacBookPro15,4", "MacBook Pro (13\", 2019)",                 15, 0,  "macOS Sequoia"),
        ["macbookpro16,1"] = new("MacBookPro16,1", "MacBook Pro (16\", 2019)",                 15, 0,  "macOS Sequoia"),
        ["macbookpro16,2"] = new("MacBookPro16,2", "MacBook Pro (13\", 2020 Intel)",           15, 0,  "macOS Sequoia"),
        ["macbookpro16,3"] = new("MacBookPro16,3", "MacBook Pro (13\", 2020 Intel)",           15, 0,  "macOS Sequoia"),
        ["macbookpro16,4"] = new("MacBookPro16,4", "MacBook Pro (16\", 2019)",                 15, 0,  "macOS Sequoia"),
        ["macbookpro17,1"] = new("MacBookPro17,1", "MacBook Pro (13\", M1, 2020)",             15, 0,  "macOS Sequoia"),
        ["macbookpro18,1"] = new("MacBookPro18,1", "MacBook Pro (16\", M1 Pro, 2021)",         15, 0,  "macOS Sequoia"),
        ["macbookpro18,2"] = new("MacBookPro18,2", "MacBook Pro (16\", M1 Max, 2021)",         15, 0,  "macOS Sequoia"),
        ["macbookpro18,3"] = new("MacBookPro18,3", "MacBook Pro (14\", M1 Pro, 2021)",         15, 0,  "macOS Sequoia"),
        ["macbookpro18,4"] = new("MacBookPro18,4", "MacBook Pro (14\", M1 Max, 2021)",         15, 0,  "macOS Sequoia"),

        ["macbook8,1"]  = new("MacBook8,1",  "MacBook (Retina 12\", Early 2015)", 12, 0, "macOS Monterey"),
        ["macbook9,1"]  = new("MacBook9,1",  "MacBook (Retina 12\", Early 2016)", 12, 0, "macOS Monterey"),
        ["macbook10,1"] = new("MacBook10,1", "MacBook (Retina 12\", 2017)",       12, 0, "macOS Monterey"),

        ["macbookpro19,1"] = new("MacBookPro19,1", "MacBook Pro (16\", M2 Pro/Max, 2023)", 26, 0, "macOS Tahoe"),
        ["macbookpro19,2"] = new("MacBookPro19,2", "MacBook Pro (14\", M2 Pro/Max, 2023)", 26, 0, "macOS Tahoe"),
        ["macbookpro20,1"] = new("MacBookPro20,1", "MacBook Pro (14\", M2, 2023)",          26, 0, "macOS Tahoe"),

        ["macbookair14,2"] = new("MacBookAir14,2", "MacBook Air (13\", M2, 2022)", 26, 0, "macOS Tahoe"),
        ["macbookair15,1"] = new("MacBookAir15,1", "MacBook Air (15\", M2, 2023)", 26, 0, "macOS Tahoe"),
        ["macbookair15,2"] = new("MacBookAir15,2", "MacBook Air (13\", M3, 2024)", 26, 0, "macOS Tahoe"),

        ["macbookair5,1"] = new("MacBookAir5,1", "MacBook Air (11\", Mid 2012)",    10, 15, "macOS Catalina"),
        ["macbookair5,2"] = new("MacBookAir5,2", "MacBook Air (13\", Mid 2012)",    10, 15, "macOS Catalina"),
        ["macbookair6,1"] = new("MacBookAir6,1", "MacBook Air (11\", Mid 2013)",    10, 13, "macOS High Sierra"),
        ["macbookair6,2"] = new("MacBookAir6,2", "MacBook Air (13\", Mid 2013)",    10, 13, "macOS High Sierra"),
        ["macbookair7,1"] = new("MacBookAir7,1", "MacBook Air (11\", Early 2015)",  12, 0,  "macOS Monterey"),
        ["macbookair7,2"] = new("MacBookAir7,2", "MacBook Air (13\", 2015/2017)",   12, 0,  "macOS Monterey"),
        ["macbookair8,1"] = new("MacBookAir8,1", "MacBook Air (13\", Late 2018)",   15, 0,  "macOS Sequoia"),
        ["macbookair8,2"] = new("MacBookAir8,2", "MacBook Air (13\", 2019)",        15, 0,  "macOS Sequoia"),
        ["macbookair9,1"] = new("MacBookAir9,1", "MacBook Air (13\", 2020 Intel)",  15, 0,  "macOS Sequoia"),
        ["macbookair10,1"]= new("MacBookAir10,1","MacBook Air (M1, 2020)",          15, 0,  "macOS Sequoia"),

        ["imacpro1,1"] = new("iMacPro1,1", "iMac Pro (2017)", 13, 0, "macOS Ventura"),

        ["imac13,1"] = new("iMac13,1", "iMac (21.5\", Late 2012)",   10, 15, "macOS Catalina"),
        ["imac13,2"] = new("iMac13,2", "iMac (27\", Late 2012)",      10, 15, "macOS Catalina"),
        ["imac14,1"] = new("iMac14,1", "iMac (21.5\", Late 2013)",   10, 13, "macOS High Sierra"),
        ["imac14,2"] = new("iMac14,2", "iMac (27\", Late 2013)",      10, 13, "macOS High Sierra"),
        ["imac14,3"] = new("iMac14,3", "iMac (21.5\", Late 2013)",   10, 13, "macOS High Sierra"),
        ["imac14,4"] = new("iMac14,4", "iMac (21.5\", Mid 2014)",    10, 13, "macOS High Sierra"),
        ["imac15,1"] = new("iMac15,1", "iMac (Retina 5K, Late 2014/2015)", 12, 0, "macOS Monterey"),
        ["imac16,1"] = new("iMac16,1", "iMac (21.5\", Late 2015)",   12, 0,  "macOS Monterey"),
        ["imac16,2"] = new("iMac16,2", "iMac (Retina 4K, Late 2015)",12, 0,  "macOS Monterey"),
        ["imac17,1"] = new("iMac17,1", "iMac (Retina 5K, Late 2015)",12, 0,  "macOS Monterey"),
        ["imac18,1"] = new("iMac18,1", "iMac (21.5\", 2017)",        13, 0,  "macOS Ventura"),
        ["imac18,2"] = new("iMac18,2", "iMac (Retina 4K, 2017)",     13, 0,  "macOS Ventura"),
        ["imac18,3"] = new("iMac18,3", "iMac (Retina 5K, 2017)",     13, 0,  "macOS Ventura"),
        ["imac19,1"] = new("iMac19,1", "iMac (Retina 5K, 2019)",     15, 0,  "macOS Sequoia"),
        ["imac19,2"] = new("iMac19,2", "iMac (Retina 4K, 2019)",     15, 0,  "macOS Sequoia"),
        ["imac20,1"] = new("iMac20,1", "iMac (Retina 5K, 2020)",     15, 0,  "macOS Sequoia"),
        ["imac20,2"] = new("iMac20,2", "iMac (Retina 5K, 2020)",     15, 0,  "macOS Sequoia"),
        ["imac21,1"] = new("iMac21,1", "iMac (24\", M1, 2021)",      15, 0,  "macOS Sequoia"),

        ["macmini6,1"] = new("Macmini6,1", "Mac mini (Late 2012)",   10, 15, "macOS Catalina"),
        ["macmini6,2"] = new("Macmini6,2", "Mac mini (Late 2012)",   10, 15, "macOS Catalina"),
        ["macmini7,1"] = new("Macmini7,1", "Mac mini (Late 2014)",   12, 0,  "macOS Monterey"),
        ["macmini8,1"] = new("Macmini8,1", "Mac mini (Late 2018)",   15, 0,  "macOS Sequoia"),
        ["macmini9,1"] = new("Macmini9,1", "Mac mini (M1, 2020)",    15, 0,  "macOS Sequoia"),

        ["macpro5,1"] = new("MacPro5,1", "Mac Pro (Mid 2010 / Mid 2012)", 10, 14, "macOS Mojave"),
        ["macpro6,1"] = new("MacPro6,1", "Mac Pro (Late 2013)",            12, 0,  "macOS Monterey"),
        ["macpro7,1"] = new("MacPro7,1", "Mac Pro (2019)",                 15, 0,  "macOS Sequoia"),
    };

    public static MacModel? Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var s = Regex.Replace(input.Trim(), @"\s+", " ");
        var collapsed = Regex.Replace(s, @"\s", "").Replace('.', ',');

        var aliases = new (string Pattern, string Canon)[]
        {
            (@"MacBookPro",   "MacBookPro"),
            (@"MacbookPro",   "MacBookPro"),
            (@"MacBook Pro",  "MacBookPro"),
            (@"MacBookAir",   "MacBookAir"),
            (@"MacbookAir",   "MacBookAir"),
            (@"MacBook Air",  "MacBookAir"),
            (@"MacBook",      "MacBook"),
            (@"Macbook",      "MacBook"),
            (@"iMacPro",      "iMacPro"),
            (@"iMac",         "iMac"),
            (@"MacPro",       "MacPro"),
            (@"Macmini",      "Macmini"),
            (@"MacMini",      "Macmini"),
            (@"Mac mini",     "Macmini"),
            (@"Mac Mini",     "Macmini"),
        };

        var idRegex = new Regex(@"(\d+)[,.](\d+)$|(\d+)[,.](\d+)", RegexOptions.IgnoreCase);

        foreach (var (pattern, canon) in aliases)
        {
            var p = Regex.Replace(pattern, @"\s", "");
            var idx = collapsed.IndexOf(p, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;

            var after = collapsed.Substring(idx + p.Length);
            var nm = Regex.Match(after, @"^(\d+)[,.](\d+)");
            if (!nm.Success) continue;

            var canonId = $"{canon}{nm.Groups[1].Value},{nm.Groups[2].Value}";

            if (_db.TryGetValue(canonId, out var exact)) return exact;

            int major = int.Parse(nm.Groups[1].Value);
            var fallback = _db
                .Where(kv => kv.Key.StartsWith(canon, StringComparison.OrdinalIgnoreCase))
                .Where(kv =>
                {
                    var m2 = Regex.Match(kv.Key, @"(\d+),");
                    return m2.Success && int.Parse(m2.Groups[1].Value) == major;
                })
                .Select(kv => kv.Value)
                .FirstOrDefault();

            if (fallback != null) return fallback;
        }

        return null;
    }

    public static IEnumerable<MacModel> All => _db.Values;
}
