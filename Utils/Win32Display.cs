using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AutoDisplayPower.Utils;

/// <summary>一次显示器探测结果（可能来自多条枚举路径，去重由调用方完成）。</summary>
public sealed class DisplayProbe
{
    public string Id { get; init; } = "";        // 设备实例/路径标识（通常含型号，如 DISPLAY\KG2575PLUS\...）
    public string Friendly { get; init; } = "";  // EDID 友好名称（可能为空）
}

/// <summary>
/// 纯 Win32 活动显示器枚举（无需第三方包、无需提权）：
/// 1) EnumDisplayDevices —— 每个活动监视器的设备实例 ID（内含型号）；
/// 2) QueryDisplayConfig(ONLY_ACTIVE_PATHS) —— 活动显示目标的设备路径与 EDID 友好名。
/// 两者互为冗余，覆盖不同机型差异。
/// </summary>
public static class Win32Display
{
    private const uint QDC_ONLY_ACTIVE_PATHS = 2;
    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
    private const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1;
    private const uint DISPLAY_DEVICE_ACTIVE = 0x1;
    private const int ErrorInsufficientBuffer = -2147024774; // 0x8007007A
    private const int ModeInfoSize = 64; // DISPLAYCONFIG_MODE_INFO 实际 ≤64 字节；此处仅占位缓冲，不解析

    public static IReadOnlyList<DisplayProbe> EnumerateActiveMonitors()
    {
        var list = new List<DisplayProbe>();

        try { EnumerateViaDisplayDevices(list); } catch { /* 该途径失败不影响整体 */ }
        try { EnumerateViaQueryDisplayConfig(list); } catch { /* 同上 */ }

        return list;
    }

    private static void EnumerateViaDisplayDevices(List<DisplayProbe> list)
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
                if ((dm.StateFlags & DISPLAY_DEVICE_ACTIVE) == 0) continue;
                if (string.IsNullOrEmpty(dm.DeviceID)) continue;

                list.Add(new DisplayProbe
                {
                    Id = dm.DeviceID.Trim(),
                    Friendly = (dm.DeviceString ?? string.Empty).Trim(),
                });
            }
        }
    }

    private static void EnumerateViaQueryDisplayConfig(List<DisplayProbe> list)
    {
        if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount) != 0)
            return;

        var paths = new DISPLAYCONFIG_PATH_INFO[Math.Max(1, (int)pathCount)];
        IntPtr modes = Marshal.AllocHGlobal((int)Math.Max(1, modeCount) * ModeInfoSize);
        try
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                uint pn = (uint)paths.Length;
                uint mn = modeCount;
                int status = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pn, paths, ref mn, modes);
                if (status == 0)
                {
                    pathCount = pn;
                    break;
                }

                if (status == ErrorInsufficientBuffer)
                {
                    if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out pathCount, out modeCount) != 0) return;
                    paths = new DISPLAYCONFIG_PATH_INFO[Math.Max(1, (int)pathCount)];
                    Marshal.FreeHGlobal(modes);
                    modes = Marshal.AllocHGlobal((int)Math.Max(1, modeCount) * ModeInfoSize);
                    continue;
                }

                return;
            }

            for (int i = 0; i < pathCount; i++)
            {
                var t = paths[i].targetInfo;
                var name = new DISPLAYCONFIG_TARGET_DEVICE_NAME
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                        size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                        adapterId = t.adapterId,
                        id = t.id,
                    },
                    monitorFriendlyDeviceName = string.Empty,
                    monitorDevicePath = string.Empty,
                };

                if (DisplayConfigGetDeviceInfo(ref name) != 0) continue;

                string devicePath = (name.monitorDevicePath ?? string.Empty).Trim();
                string friendly = (name.monitorFriendlyDeviceName ?? string.Empty).Trim();
                if (devicePath.Length == 0 && friendly.Length == 0) continue;

                list.Add(new DisplayProbe { Id = devicePath, Friendly = friendly });
            }
        }
        finally
        {
            Marshal.FreeHGlobal(modes);
        }
    }

    // ---------------- P/Invoke ----------------

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

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
    private struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public uint type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_TARGET_DEVICE_NAME_FLAGS
    {
        public uint value;
    }

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [In, Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        IntPtr modeInfoArray);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME deviceName);
}
