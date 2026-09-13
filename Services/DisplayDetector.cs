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

    /// <summary>合盖状态：由 WMI WmiMonitorID（已连接显示器）判断内屏是否存在（开盖/关盖）。</summary>
    public LidState Lid { get; set; } = LidState.Unknown;

    /// <summary>合盖判断的细节说明（用于自检/日志）。</summary>
    public string LidDetail { get; set; } = "";

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

/// <summary>显示器归类配置（可由用户在托盘菜单里配置，默认兼容当前机型）。</summary>
public sealed class MonitorConfig
{
    /// <summary>笔记本内屏型号（EDID 名称）。</summary>
    public string InternalModel { get; set; } = "ATNA40HQ01-0";

    /// <summary>已知外接屏型号列表（仅当 AnyExternal=false 时生效）。</summary>
    public List<string> ExternalModels { get; set; } = new() { "KG2575 PLUS", "KG257S PLUS" };

    /// <summary>true=任意非内屏显示器都算外接屏（换外接屏免配置，推荐）。</summary>
    public bool AnyExternal { get; set; } = true;

    /// <summary>从注册表读取配置（未设置用默认值）。</summary>
    public static MonitorConfig Load()
    {
        var cfg = new MonitorConfig
        {
            InternalModel = AppSettings.ReadString("InternalModel", "ATNA40HQ01-0"),
            AnyExternal = AppSettings.ReadBool("AnyExternal", true),
        };

        string ext = AppSettings.ReadString("ExternalModels", "KG2575 PLUS,KG257S PLUS");
        cfg.ExternalModels = ext
            .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return cfg;
    }

    /// <summary>保存到注册表。</summary>
    public void Save()
    {
        AppSettings.WriteString("InternalModel", InternalModel);
        AppSettings.WriteString("ExternalModels", string.Join(",", ExternalModels));
        AppSettings.WriteBool("AnyExternal", AnyExternal);
    }
}

/// <summary>
/// 显示器型号扫描器：枚举当前【活动】显示器（EnumDisplayDevices），
/// 读取其 EDID 名称描述符（0xFC）获得真实型号，按【用户配置的型号】归类；
/// 未匹配的型号为“未知”，不影响判定。
/// </summary>
public static class DisplayDetector
{
    public static MonitorScanResult Scan()
    {
        var result = new MonitorScanResult { Ok = true };
        try
        {
            var config = MonitorConfig.Load();
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

                bool isInternal = MatchesModel(probeText, config.InternalModel);
                // 外接屏：配置了“任意外接屏”→ 任意非内屏显示器；否则仅匹配已知外接型号
                bool isExternal = !isInternal &&
                    (config.AnyExternal || config.ExternalModels.Any(m => MatchesModel(probeText, m)));

                if (isInternal && !internalAdded)
                {
                    internalAdded = true;
                    result.Monitors.Add(new MonitorInfo
                    {
                        InstanceName = id.Length > 0 ? id : probe.Friendly,
                        ModelName = edidName.Length > 0 ? $"笔记本内屏 ({edidName})" : $"笔记本内屏 ({config.InternalModel})",
                        Kind = MonitorKind.Internal,
                    });
                }

                if (isExternal && !externalAdded)
                {
                    externalAdded = true;
                    result.Monitors.Add(new MonitorInfo
                    {
                        InstanceName = id.Length > 0 ? id : probe.Friendly,
                        ModelName = edidName.Length > 0 ? $"外接屏 ({edidName})" : "外接屏",
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

            // 合盖状态：WMI WmiMonitorID 列出的是“已连接”显示器（含未激活的内屏）。
            // 实测：开盖时内屏在列表中（即使当前为“仅外接”模式）；关盖后内屏从列表消失。
            // → 零切换、零闪烁、零黑屏即可判断开/合盖。
            var (lid, lidDetail) = DetectLidByWmi(config);
            result.Lid = lid;
            result.LidDetail = lidDetail;
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>判定文本是否包含某个型号（忽略大小写与空格差异）。</summary>
    private static bool MatchesModel(string probe, string model)
    {
        if (string.IsNullOrWhiteSpace(probe) || string.IsNullOrWhiteSpace(model)) return false;
        string m = model.Trim();
        if (probe.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        string compact = Regex.Replace(probe, @"\s+", string.Empty);
        string compactModel = Regex.Replace(m, @"\s+", string.Empty);
        return compactModel.Length > 0 && compact.IndexOf(compactModel, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// 用 WMI root\wmi\WmiMonitorID（“已连接”显示器列表，含未激活的内屏）判断合盖状态：
    /// 内屏在列表中 → 开盖；查询成功但内屏不在列表 → 关盖；查询失败 → 未知。
    /// 该方式无需切换显示，不闪屏、不黑屏。
    /// </summary>
    public static (LidState Lid, string Detail) DetectLidByWmi(MonitorConfig config)
    {
        var q = WmiQuery.QueryProperty(@"root\wmi", "SELECT InstanceName FROM WmiMonitorID", "InstanceName");
        if (!q.Ok) return (LidState.Unknown, "WMI 查询失败：" + (q.Error ?? "未知错误"));

        var names = new List<string>();
        bool internalConnected = false;
        int resolved = 0;

        foreach (string instance in q.Values)
        {
            string edidName = EdidHelper.ResolveModelName(instance);
            if (edidName.Length > 0) resolved++;
            names.Add(edidName.Length > 0 ? edidName : instance);

            if (MatchesModel(edidName, config.InternalModel) || MatchesModel(instance, config.InternalModel))
                internalConnected = true;
        }

        // 有实例但一个型号名都解析不出来 → 无法可靠判断，不误报
        if (q.Values.Count > 0 && resolved == 0)
            return (LidState.Unknown, $"WMI 已连接 {q.Values.Count} 台，但型号名解析失败（内屏={config.InternalModel}）");

        string detail = names.Count == 0
            ? "WMI 未列出任何已连接显示器"
            : "WMI 已连接：" + string.Join("、", names);

        return (internalConnected ? LidState.Open : LidState.Closed, detail);
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
