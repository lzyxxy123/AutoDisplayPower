using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AutoDisplayPower.Utils;

/// <summary>一次活动显示器的探测结果。</summary>
public sealed class DisplayProbe
{
    public string Id { get; init; } = "";        // 设备实例 ID（如 MONITOR\LHC91B7\…），用于定位 EDID
    public string Friendly { get; init; } = "";  // 设备友好名（DeviceString，通常为“通用即插即用监视器”）
}

/// <summary>
/// 枚举当前【活动】的显示器（EnumDisplayDevices），每个返回其设备实例 ID，
/// 供上层读取 EDID 解析真实型号名。
/// </summary>
public static class Win32Display
{
    private const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1;
    private const uint DISPLAY_DEVICE_ACTIVE = 0x1;

    public static IReadOnlyList<DisplayProbe> EnumerateActiveMonitors()
    {
        var list = new List<DisplayProbe>();
        foreach (var (probe, active) in EnumerateAllMonitors())
        {
            if (active) list.Add(probe);
        }

        return list;
    }

    /// <summary>枚举所有监视器（含未激活），返回是否活动。用于区分“连接但未激活”与“完全断开”。</summary>
    public static IReadOnlyList<(DisplayProbe Probe, bool Active)> EnumerateAllMonitors()
    {
        var list = new List<(DisplayProbe, bool)>();
        try
        {
            for (uint adapter = 0; ; adapter++)
            {
                var dd = new DISPLAY_DEVICE { cb = (uint)Marshal.SizeOf<DISPLAY_DEVICE>() };
                if (!EnumDisplayDevices(null, adapter, ref dd, 0)) break;
                if ((dd.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0) continue;

                for (uint mon = 0; ; mon++)
                {
                    var dm = new DISPLAY_DEVICE { cb = (uint)Marshal.SizeOf<DISPLAY_DEVICE>() };
                    if (!EnumDisplayDevices(dd.DeviceName, mon, ref dm, 0)) break;
                    if (string.IsNullOrEmpty(dm.DeviceID)) continue;

                    bool active = (dm.StateFlags & DISPLAY_DEVICE_ACTIVE) != 0;
                    list.Add((new DisplayProbe
                    {
                        Id = dm.DeviceID.Trim(),
                        Friendly = (dm.DeviceString ?? string.Empty).Trim(),
                    }, active));
                }
            }
        }
        catch { /* 返回已收集项 */ }

        return list;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public uint cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);
}
