using System;
using System.IO;
using System.Text;
using AutoDisplayPower.Services;
using AutoDisplayPower.Utils;

namespace AutoDisplayPower;

/// <summary>
/// 只读自检模式：
///   --check               打印检测到的显示器、推断状态与电源策略
///   --check --selftest-power  额外以当前值原样回写，验证写权限
///   --topology            打印 QueryDisplayConfig 全部显示目标（调试开合检测）
/// </summary>
public static class CheckMode
{
    /// <summary>调试：打印显示拓扑，验证内屏“物理可用”能否区分开/合盖。</summary>
    public static void DumpTopology()
    {
        var sb = new StringBuilder();
        sb.AppendLine("== DisplayTopology (QDC_ALL_PATHS) ==");
        sb.AppendLine(DisplayTopology.Diagnostic());
        var targets = DisplayTopology.EnumerateTargets(onlyActivePaths: false);
        sb.AppendLine($"目标数量: {targets.Count}");
        foreach (var t in targets)
            sb.AppendLine($"  {t}");

        var activeTargets = DisplayTopology.EnumerateTargets(onlyActivePaths: true);
        sb.AppendLine($"仅活动目标数量: {activeTargets.Count}");
        foreach (var t in activeTargets)
            sb.AppendLine($"  ACTIVE=>{t}");

        var lid = DisplayTopology.DetectLidState(targets);
        sb.AppendLine($"[推断合盖] {lid}");

        sb.AppendLine();
        sb.AppendLine("== EnumDisplayDevices 所有监视器(含未激活) ==");
        foreach (var (probe, active) in Win32Display.EnumerateAllMonitors())
        {
            string model = EdidHelper.ResolveModelName(probe.Id);
            sb.AppendLine($"  [{(active ? "ACTIVE" : "inact")}] {model}  <{probe.Id}>");
        }
        Console.WriteLine(sb.ToString());

        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoDisplayPower");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "topology-report.txt"), sb.ToString(), new UTF8Encoding(false));
        }
        catch { }
    }

    public static void Run(bool selfTestPower)
    {
        var sb = new StringBuilder();
        sb.AppendLine("== AutoDisplayPower 自检报告 ==");
        sb.AppendLine($"[时间] {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        var snap = DisplayDetector.Scan();
        sb.AppendLine($"[显示器扫描] {(snap.Ok ? "成功" : "失败")}");
        if (!snap.Ok)
        {
            sb.AppendLine("  错误：" + snap.Error);
        }
        else if (snap.Monitors.Count == 0)
        {
            sb.AppendLine("  未枚举到任何活动显示器（EnumDisplayDevices / QueryDisplayConfig 均为空）");
        }

        foreach (var m in snap.Monitors)
        {
            sb.AppendLine($"  - [{m.KindText}] {m.ModelName}");
            sb.AppendLine($"      InstanceName: {m.InstanceName}");
        }

        sb.AppendLine($"[屏幕状态] {StateRules.Describe(snap.State)}");
        sb.AppendLine($"[推断盖子] {StateRules.LidText(snap.Lid)}");
        if (!string.IsNullOrEmpty(snap.LidDetail)) sb.AppendLine($"[盖子判据] {snap.LidDetail}");
        var (lid, policyText) = StateRules.ExpectedPolicy(snap.State);
        sb.AppendLine($"[期望策略] {policyText}  (LIDACTION = {(lid.HasValue ? lid.Value.ToString() : "不变")})");

        var (ac, dc, okQ) = PowerManager.QueryLidAction();
        if (okQ)
            sb.AppendLine($"[当前实际] LIDACTION AC = 0x{ac:X}，DC = 0x{dc:X}");
        else
            sb.AppendLine("[当前实际] 无法读取 LIDACTION（本机电源方案未提供合盖项，或当前会话无修改权限）；" +
                          "程序仍会按状态管理策略，界面显示“期望策略（已下发/写入失败）”。");

        if (selfTestPower)
        {
            sb.AppendLine("[写回自测] 以当前值原样回写 AC/DC（不改变实际行为）……");
            if (okQ && ac >= 0 && dc >= 0)
            {
                if (PowerManager.ApplyLidAction(ac, dc, out string? werr))
                    sb.AppendLine("  通过：当前用户无需提权即可修改本机电源方案合盖设置。");
                else
                    sb.AppendLine($"  失败：{werr} —— 若是权限错误，本机可能需要计划任务辅助执行。");
            }
            else
            {
                sb.AppendLine("  跳过：无法读取当前值，无法执行无副作用回写。");
            }
        }

        string report = sb.ToString();
        Console.WriteLine(report);

        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutoDisplayPower");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "check-report.txt");
            File.WriteAllText(file, report, new UTF8Encoding(false));
            Console.WriteLine("报告文件：" + file);
        }
        catch
        {
            // 忽略写文件失败
        }
    }
}
