using System;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AutoDisplayPower.Utils;

/// <summary>
/// 从注册表读取监视器 EDID 并解析其型号名称（0xFC 描述符），
/// 这是 Windows 里最可靠的“真实型号名”来源（如 ATNA40HQ01-0 / KG257S PLUS）。
/// </summary>
public static class EdidHelper
{
    private const string EnumRoot = @"SYSTEM\CurrentControlSet\Enum";

    /// <summary>
    /// 根据设备实例 ID（如 MONITOR\LHC91B7\{...}\0002）提取型号段，
    /// 再到 DISPLAY\ / MONITOR\ 枚举下读取 EDID，解析名称。
    /// 失败返回空字符串。
    /// </summary>
    public static string ResolveModelName(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return string.Empty;

        var m = Regex.Match(deviceId, @"(?:DISPLAY|MONITOR)[\\#]([^\\#]+)", RegexOptions.IgnoreCase);
        if (!m.Success) return string.Empty;

        string modelToken = m.Groups[1].Value.Trim();
        if (modelToken.Length == 0) return string.Empty;

        return ReadEdidName(modelToken);
    }

    private static string ReadEdidName(string modelToken)
    {
        foreach (string cls in new[] { "DISPLAY", "MONITOR" })
        {
            string modelKey = $@"{EnumRoot}\{cls}\{modelToken}";

            string[]? subKeys;
            try
            {
                using var root = Registry.LocalMachine.OpenSubKey(modelKey);
                subKeys = root?.GetSubKeyNames();
            }
            catch
            {
                subKeys = null; // 无权限等，忽略
            }

            if (subKeys is null || subKeys.Length == 0) continue;

            foreach (string sub in subKeys)
            {
                try
                {
                    using var dp = Registry.LocalMachine.OpenSubKey($@"{modelKey}\{sub}\Device Parameters");
                    if (dp?.GetValue("EDID") is byte[] edid && edid.Length > 0)
                    {
                        string name = ParseNameDescriptor(edid);
                        if (name.Length > 0) return name;
                    }
                }
                catch
                {
                    // 继续尝试下一个实例
                }
            }
        }

        return string.Empty;
    }

    /// <summary>扫描整个 EDID（含扩展块）中的 0xFC 名称描述符。</summary>
    private static string ParseNameDescriptor(byte[] edid)
    {
        for (int i = 0; i + 18 <= edid.Length; i++)
        {
            // 显示描述符块的判别：前 3 字节为 0，第 4 字节为标签（0xFC=名称）
            if (edid[i] != 0 || edid[i + 1] != 0 || edid[i + 2] != 0 || edid[i + 3] != 0xFC) continue;

            int start = i + 5;
            int length = Math.Min(13, edid.Length - start);
            if (length <= 0) continue;

            string name = Encoding.ASCII.GetString(edid, start, length)
                .Trim('\n', '\r', '\0', ' ');
            if (name.Length > 0) return name;
        }

        return string.Empty;
    }
}
