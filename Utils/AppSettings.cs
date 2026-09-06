using Microsoft.Win32;

namespace AutoDisplayPower.Utils;

/// <summary>HKCU\Software\AutoDisplayPower 下的简单设置读写。</summary>
public static class AppSettings
{
    private const string RootKeyPath = @"Software\AutoDisplayPower";

    public static bool ReadBool(string name, bool defaultValue)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RootKeyPath);
            if (key?.GetValue(name) is int v) return v != 0;
            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    public static void WriteBool(string name, bool value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RootKeyPath);
            key?.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
        }
        catch
        {
            // 忽略
        }
    }
}
