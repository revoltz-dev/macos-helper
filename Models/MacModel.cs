namespace MacOSHelper.Models;

public record MacModel(
    string Identifier,
    string MarketingName,
    int MaxMajor,
    int MaxMinor,
    string MaxMacOSName
)
{
    public string MaxVersionString => MaxMajor == 10 ? $"{MaxMajor}.{MaxMinor}" : $"{MaxMajor}";
    public string DisplayMax => $"{MaxMacOSName} ({MaxVersionString})";
}
