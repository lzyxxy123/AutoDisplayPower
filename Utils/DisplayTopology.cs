using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AutoDisplayPower.Services;

namespace AutoDisplayPower.Utils;

/// <summary>一个显示目标（屏幕）的拓扑信息。</summary>
public sealed class DisplayTargetInfo
{
    public string AdapterId { get; set; } = "";
    public uint TargetId { get; set; }
    public bool Active { get; set; }       // 是否处于当前活动拓扑（正在显示）
    public bool Available { get; set; }    // 物理上是否可用（连接着）——内屏可区分“关盖断开”与“开盖但未激活”
    public string DevicePath { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public string EdidName { get; set; } = "";  // 由 DevicePath 经 EdidHelper 解析出的真实型号名

    public bool IsInternal => EdidName.Contains("ATNA40HQ01", StringComparison.OrdinalIgnoreCase)
                              || EdidName.Contains("SDC4203", StringComparison.OrdinalIgnoreCase);
    public bool IsExternal => EdidName.Contains("KG257", StringComparison.OrdinalIgnoreCase);

    public override string ToString()
        => $"[{(Active ? "ACTIVE" : "inact")}|{(Available ? "avail" : "unavail")}] {EdidName}  id={AdapterId}/{TargetId}";
}

/// <summary>合盖状态。</summary>
public enum LidState { Unknown, Open, Closed }

/// <summary>
/// 基于 QueryDisplayConfig 枚举全部显示路径的拓扑信息。
/// 关键：QDC_ALL_PATHS 会列出所有物理可见（可能未激活）的目标，其 targetAvailable 可区分
/// “内屏物理断开（关盖）/内屏物理连接但未激活（开盖+仅外接）”。
/// </summary>
public static class DisplayTopology
{
    private const uint QDC_ALL_PATHS = 1;
    private const uint QDC_ONLY_ACTIVE_PATHS = 2;
    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
    private const uint DISPLAYCONFIG_PATH_ACTIVE = 0x1;
    private const int ErrorInsufficientBuffer = -2147024774; // 0x8007007A
    private const int ModeInfoBufSize = 128; // DISPLAYCONFIG_MODE_INFO 实际 ≤64，放宽占位
    private const int ErrorPathNotFound = -2147024893; // 0x80070003

    /// <summary>枚举全部显示目标（去重后，每个目标附设备名与 EDID 型号名）。</summary>
    public static IReadOnlyList<DisplayTargetInfo> EnumerateTargets(bool onlyActivePaths = false)
    {
        uint flags = onlyActivePaths ? QDC_ONLY_ACTIVE_PATHS : QDC_ALL_PATHS;
        var result = new List<DisplayTargetInfo>();
        try
        {
            if (GetDisplayConfigBufferSizes(flags, out uint pathCount, out uint modeCount) != 0)
                return result;

            int pathSize = Marshal.SizeOf<DISPLAYCONFIG_PATH_INFO>();
            IntPtr paths = Marshal.AllocHGlobal((int)pathCount * pathSize);
            IntPtr modes = Marshal.AllocHGlobal((int)Math.Max(1, modeCount) * ModeInfoBufSize);
            try
            {
                bool ok = false;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    uint pn = pathCount;
                    uint mn = modeCount;
                    int status = QueryDisplayConfig(flags, ref pn, paths, ref mn, modes);
                    if (status == 0) { pathCount = pn; ok = true; break; }
                    if (status == ErrorInsufficientBuffer)
                    {
                        if (GetDisplayConfigBufferSizes(flags, out pathCount, out modeCount) != 0) return result;
                        Marshal.FreeHGlobal(paths);
                        Marshal.FreeHGlobal(modes);
                        paths = Marshal.AllocHGlobal((int)pathCount * pathSize);
                        modes = Marshal.AllocHGlobal((int)Math.Max(1, modeCount) * ModeInfoBufSize);
                        continue;
                    }
                    return result; // 其他错误（如 ERROR_INVALID_PARAMETER）
                }

                if (!ok) return result;

                var byKey = new Dictionary<string, DisplayTargetInfo>();
                for (int i = 0; i < (int)pathCount; i++)
                {
                    var info = Marshal.PtrToStructure<DISPLAYCONFIG_PATH_INFO>(IntPtr.Add(paths, i * pathSize));
                    ref var target = ref info.targetInfo;
                    string key = $"{target.adapterId.LowPart:X8}{target.adapterId.HighPart:X8}:{target.id}";
                    bool active = (info.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0;
                    bool available = target.targetAvailable != 0;
                    string adapterId = $"{target.adapterId.LowPart:X8}{target.adapterId.HighPart:X8}";

                    if (byKey.TryGetValue(key, out var existing))
                    {
                        existing.Active |= active;
                        existing.Available |= available;
                    }
                    else
                    {
                        byKey[key] = new DisplayTargetInfo
                        {
                            AdapterId = adapterId,
                            TargetId = target.id,
                            Active = active,
                            Available = available,
                        };
                    }
                }

                // 为每个目标补充设备名与 EDID 型号
                foreach (var item in byKey.Values)
                {
                    var (devicePath, friendly) = GetTargetDeviceName(item.AdapterId, item.TargetId);
                    item.DevicePath = devicePath;
                    item.FriendlyName = friendly;
                    string modelSource = devicePath.Length > 0 ? devicePath : friendly;
                    item.EdidName = EdidHelper.ResolveModelName(modelSource);
                }

                result.AddRange(byKey.Values);
            }
            finally { Marshal.FreeHGlobal(paths); Marshal.FreeHGlobal(modes); }
        }
        catch { /* 枚举失败：返回已收集项 */ }
        return result;
    }

    /// <summary>调试：尝试多组 flag 组合，报告 QueryDisplayConfig 返回码（零初始化缓冲区）。</summary>
    public static string Diagnostic()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("PathInfoSize=" + Marshal.SizeOf<DISPLAYCONFIG_PATH_INFO>()
            + " TargetInfoSize=" + Marshal.SizeOf<DISPLAYCONFIG_PATH_TARGET_INFO>()
            + " SourceInfoSize=" + Marshal.SizeOf<DISPLAYCONFIG_PATH_SOURCE_INFO>());

        int pathSize = Marshal.SizeOf<DISPLAYCONFIG_PATH_INFO>();
        foreach (uint flag in new[] { QDC_ALL_PATHS, QDC_ONLY_ACTIVE_PATHS, QDC_ALL_PATHS | 4u, QDC_ONLY_ACTIVE_PATHS | 4u })
        {
            int err = GetDisplayConfigBufferSizes(flag, out uint pa, out uint ma);
            if (err != 0) { sb.AppendLine($"flag={flag} Sizes err={err}"); continue; }
            var paths = new byte[(int)Math.Max(1, pa) * pathSize];
            var modeArr = new byte[(int)Math.Max(1, ma) * ModeInfoBufSize];
            IntPtr hPaths = Marshal.AllocHGlobal(paths.Length);
            IntPtr hModes = Marshal.AllocHGlobal(modeArr.Length);
            try
            {
                Marshal.Copy(paths, 0, hPaths, paths.Length);
                Marshal.Copy(modeArr, 0, hModes, modeArr.Length);
                uint pn = pa;
                uint mn = ma;
                int st = QueryDisplayConfig(flag, ref pn, hPaths, ref mn, hModes);
                int active = 0, avail = 0, distinct = 0;
                var seen = new HashSet<string>();
                for (int i = 0; i < Math.Min(pn, (uint)2000); i++)
                {
                    var info = Marshal.PtrToStructure<DISPLAYCONFIG_PATH_INFO>(IntPtr.Add(hPaths, i * pathSize));
                    if ((info.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0) active++;
                    if (info.targetInfo.targetAvailable != 0) avail++;
                    string k = $"{info.targetInfo.adapterId.LowPart:X}:{info.targetInfo.adapterId.HighPart:X}:{info.targetInfo.id}";
                    if (seen.Add(k)) distinct++;
                }
                sb.AppendLine($"flag={flag} Sizes paths={pa} modes={ma} -> Query status={st} activePaths={active} availTargets={avail} distinctTargets={distinct}");
            }
            finally { Marshal.FreeHGlobal(hPaths); Marshal.FreeHGlobal(hModes); }
        }
        return sb.ToString();
    }

    /// <summary>由内屏“物理可用”推断合盖状态（QDC_ALL_PATHS 可区分“连接但未激活”与“已断开”）。</summary>
    public static LidState DetectLidState(IReadOnlyList<DisplayTargetInfo> targets)
    {
        if (targets is null || targets.Count == 0) return LidState.Unknown;

        foreach (var t in targets)
        {
            // 找到内屏目标：按 targetAvailable 区分“开盖(物理可用)”与“关盖(物理断开)”
            if (t.IsInternal)
                return t.Available ? LidState.Open : LidState.Closed;
        }

        // 未找到内屏目标：无法确认是否断开，保守返回 Unknown，避免误报“闭合”
        return LidState.Unknown;
    }

    private static (string DevicePath, string Friendly) GetTargetDeviceName(string adapterId, uint targetId)
    {
        try
        {
            uint low = Convert.ToUInt32(adapterId.Substring(0, 8), 16);
            uint high = Convert.ToUInt32(adapterId.Substring(8, 8), 16);
            var name = new DISPLAYCONFIG_TARGET_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                    adapterId = new LUID { LowPart = low, HighPart = unchecked((int)high) },
                    id = targetId,
                },
                monitorFriendlyDeviceName = string.Empty,
                monitorDevicePath = string.Empty,
            };
            if (DisplayConfigGetDeviceInfo(ref name) != 0) return ("", "");
            return ((name.monitorDevicePath ?? string.Empty).Trim(), (name.monitorFriendlyDeviceName ?? string.Empty).Trim());
        }
        catch { return ("", ""); }
    }

    // ---------------- P/Invoke ----------------

    [StructLayout(LayoutKind.Sequential)] private struct LUID { public uint LowPart; public int HighPart; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_SOURCE_INFO { public LUID adapterId; public uint id; public uint modeInfoIdx; public uint statusFlags; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public uint refreshRateNumerator;
        public uint refreshRateDenominator;
        public uint scanLineOrdering;
        public uint targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_INFO { public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo; public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo; public uint flags; }

    [StructLayout(LayoutKind.Sequential)] private struct DISPLAYCONFIG_DEVICE_INFO_HEADER { public uint type; public uint size; public LUID adapterId; public uint id; }
    [StructLayout(LayoutKind.Sequential)] private struct DISPLAYCONFIG_TARGET_DEVICE_NAME_FLAGS { public uint value; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public DISPLAYCONFIG_TARGET_DEVICE_NAME_FLAGS flags;
        public uint outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string monitorDevicePath;
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, IntPtr pathArray, ref uint numModeInfoArrayElements, IntPtr modeInfoArray);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME deviceName);
}
