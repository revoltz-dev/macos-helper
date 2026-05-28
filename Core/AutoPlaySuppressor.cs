using Microsoft.Win32;

namespace MacOSHelper.Core;

internal sealed class AutoPlaySuppressor : IDisposable
{
    private const string KeyPath   = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
    private const string ValueName = "NoDriveTypeAutoRun";

    private object? _oldValue;
    private bool    _hadOld;
    private bool    _disposed;

    public AutoPlaySuppressor()
    {
        try
        {
            using (var ro = Registry.CurrentUser.OpenSubKey(KeyPath))
            {
                _oldValue = ro?.GetValue(ValueName);
                _hadOld   = _oldValue != null;
            }
            using var w = Registry.CurrentUser.CreateSubKey(KeyPath);
            w.SetValue(ValueName, 0xFF, RegistryValueKind.DWord);
        }
        catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            using var w = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
            if (w == null) return;
            if (_hadOld && _oldValue is int v)
                w.SetValue(ValueName, v, RegistryValueKind.DWord);
            else
                w.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch { }
    }
}
