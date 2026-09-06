using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AutoDisplayPower.Utils;

namespace AutoDisplayPower.Services;

/// <summary>显示器种类。</summary>
public enum MonitorKind
{
    Unknown,
    Internal,   // 笔记本内屏 ATNA40HQ01-0
    External,   // 外接屏 KG2575 PLUS
}

/// <summary>推断出的屏幕状态机状态。</summary>
public enum ScreenState
{
    Unknown,        // 无法读取 / 无已知显示器（安全模式：保持现状）
    InternalOnly,   // 仅内屏
    ExternalOnly,   // 仅外接屏
    Extended,       // 内屏 + 外接屏同时亮
}

public sealed class MonitorInfo
{
    public required string InstanceName { get; init; }
    public required string ModelName { get; init; }
    public MonitorKind Kind { get; init; } = MonitorKind.Unknown;

    public string KindText => Kind switch
    {
        MonitorKind.Internal => "内屏",
        MonitorKind.External => "外接",
        _ => "未知",
    };
}

public sealed class MonitorScanResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public List<MonitorInfo> Monitors { get; } = new();

    /// <summary>合盖状态：由 QueryDisplayConfig 判断内屏是否物理“可用”推断（区分开盖/关盖）。</summary>
    public LidState Lid { get; set; } = LidState.Unknown;

    public bool HasInternal => Monitors.Any(m => m.Kind == MonitorKind.Internal);
    public bool HasExternal => Monitors.Any(m => m.Kind == MonitorKind.External);

    /// <summary>由当前扫描结果推断的物理状态。</summary>
    public ScreenState State
    {
        get
        {
            if (!Ok) return ScreenState.Unknown;
            bool hasInternal = HasInternal;
            bool hasExternal = HasExternal;
            if (hasInternal && hasExternal) return ScreenState.Extended;
            if (hasInternal) return ScreenState.InternalOnly;
            if (hasExternal) return ScreenState.ExternalOnly;
            return ScreenState.Unknown;
        }
    }
}

/// <summary>
/// 显示器型号扫描器：枚举当前【活动】显示器（EnumDisplayDevices），
/// 读取其 EDID 名称描述符（0xFC）获得真实型号，
/// 根据已知指纹（内屏 ATNA40HQ01-0 / 外接 KG257S PLUS）归类；
/// 其他未知型号一律忽略（不影响判定）。
/// </summary>
public static class DisplayDetector
{
    private static readonly string[] InternalTokens = { "ATNA40HQ01-0", "ATNA40HQ010" };

    // 外接屏指纹：规格书写作 KG2575 PLUS，实际 EDID 名称为 KG257S PLUS（5 与 S 易混），两者都兼容
    private static readonly string[] ExternalTokens = { "KG2575 PLUS", "KG257S PLUS", "KG2575", "KG257S", "KG257" };

    public static MonitorScanResult Scan()
    {
        var result = new MonitorScanResult { Ok = true };
        try
        {
            var probes = Win32Display.EnumerateActiveMonitors();
            bool internalAdded = false;
            bool externalAdded = false;
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var probe in probes)
            {
                string id = probe.Id.Trim();

                // 读取 EDID 0xFC 名称描述符，得到真实型号（如 ATNA40HQ01-0 / KG257S PLUS）
                string edidName = EdidHelper.ResolveModelName(id);

                // 合并设备路径、友好名、EDID 名称三个信息源后再匹配，兼容不同机型差异
                string probeText = $"{id} | {probe.Friendly} | {edidName}";
                if (string.IsNullOrWhiteSpace(probeText)) continue;

                bool isInternal = MatchesAny(probeText, InternalTokens);
                bool isExternal = MatchesAny(probeText, ExternalTokens);

                if (isInternal && !internalAdded)
                {
                    internalAdded = true;
                    result.Monitors.Add(new MonitorInfo
                    {
                        InstanceName = id.Length > 0 ? id : probe.Friendly,
                        ModelName = edidName.Length > 0 ? $"笔记本内屏 ({edidName})" : "笔记本内屏 (ATNA40HQ01-0)",
                        Kind = MonitorKind.Internal,
                    });
                }

                if (isExternal && !externalAdded)
                {
                    externalAdded = true;
                    result.Monitors.Add(new MonitorInfo
                    {
                        InstanceName = id.Length > 0 ? id : probe.Friendly,
                        ModelName = edidName.Length > 0 ? $"外接屏 ({edidName})" : "外接屏 (KG2575 PLUS)",
                        Kind = MonitorKind.External,
                    });
                }

                if (!isInternal && !isExternal)
                {
                    string key = id.Length > 0 ? id : probe.Friendly;
                    if (key.Length > 0 && seenIds.Add(key))
                    {
                        result.Monitors.Add(new MonitorInfo
                        {
                            InstanceName = id,
                            ModelName = ShortLabel(edidName, probe.Friendly, id),
                            Kind = MonitorKind.Unknown,
                        });
                    }
                }
            }

            // 合盖状态：用 QueryDisplayConfig 判断内屏是否物理“可用”（区分“开盖但未激活”与“关盖断开”）
            try
            {
                var lidTargets = DisplayTopology.EnumerateTargets(onlyActivePaths: false);
                result.Lid = DisplayTopology.DetectLidState(lidTargets);
            }
            catch (Exception ex)
            {
                Logger.Warn($"读取合盖状态失败：{ex.Message}");
            }
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private static bool MatchesAny(string probe, string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(probe)) return false;
        string compact = Regex.Replace(probe, @"\s+", string.Empty);
        foreach (string token in tokens)
        {
            if (probe.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            string compactToken = Regex.Replace(token, @"\s+", string.Empty);
            if (compact.IndexOf(compactToken, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }

    /// <summary>从未知显示器生成一个可读简称：优先 EDID 名，其次设备友好名，最后取设备实例型号段。</summary>
    private static string ShortLabel(string edidName, string friendly, string id)
    {
        if (!string.IsNullOrWhiteSpace(edidName)) return edidName;

        string f = (friendly ?? string.Empty).Trim();
        if (f.Length > 0 && !f.Contains("即插即用", StringComparison.OrdinalIgnoreCase)
                          && !f.Contains("PnP", StringComparison.OrdinalIgnoreCase)
                          && !f.Contains("Generic", StringComparison.OrdinalIgnoreCase))
        {
            return f;
        }

        var m = Regex.Match(id ?? string.Empty, @"(?:DISPLAY|MONITOR)[\\#]([^\\#]+)", RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value;
        return string.IsNullOrWhiteSpace(id) ? "未知显示器" : id;
    }
}

/// <summary>
/// 状态机规则（对应需求规格书 3.1）：
/// 仅外接 → 合盖不操作(0)；仅内屏 → 合盖睡眠(1)；双屏 → 合盖不操作(0)；其他 → 保持现状。
/// </summary>
public static class StateRules
{
    public static string Describe(ScreenState s) => s switch
    {
        ScreenState.InternalOnly => "仅笔记本内屏",
        ScreenState.ExternalOnly => "仅外接屏",
        ScreenState.Extended => "双屏扩展",
        _ => "未知 / 安全模式",
    };

    public static string InferLidText(ScreenState s) => s switch
    {
        ScreenState.InternalOnly => "打开（未接外接）",
        ScreenState.ExternalOnly => "闭合（内屏已断开）",
        ScreenState.Extended => "打开（双屏扩展）",
        _ => "未知",
    };

    /// <summary>期望合盖操作：0=不操作、1=睡眠(S3)、null=保持现状不动。</summary>
    public static (int? LidAction, string PolicyText) ExpectedPolicy(ScreenState s) => s switch
    {
        ScreenState.InternalOnly => (1, "合盖 → 睡眠(S3)"),
        ScreenState.ExternalOnly => (0, "合盖 → 不操作"),
        ScreenState.Extended => (0, "合盖 → 不操作"),
        _ => (null, "保持现状（未知状态）"),
    };

    public static string DescribeLidValue(int value) => value switch
    {
        0 => "合盖不操作",
        1 => "合盖睡眠",
        2 => "合盖休眠",
        3 => "合盖关机",
        _ => $"未知({value})",
    };

    /// <summary>合盖状态的可读文本（来自 QueryDisplayConfig 判断）。</summary>
    public static string LidText(LidState lid) => lid switch
    {
        LidState.Open => "打开",
        LidState.Closed => "闭合",
        _ => "未知",
    };
}
