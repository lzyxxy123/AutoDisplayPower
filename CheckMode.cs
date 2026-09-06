using System;
using System.IO;
using System.Text;
using AutoDisplayPower.Services;
using AutoDisplayPower.Utils;

namespace AutoDisplayPower;

/// <summary>
/// 只读自检模式：AutoDisplayPower.exe --check [--selftest-power]
/// 打印检测到的显示器、推断状态与电源策略，不修改任何设置。
/// 报告同时写入 %LOCALAPPDATA%\AutoDisplayPower\check-report.txt。
/// </summary>
public static class CheckMode
{
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
        sb.AppendLine($"[推断盖子] {StateRules.InferLidText(snap.State)}");
        var (lid, policyText) = StateRules.ExpectedPolicy(snap.State);
        sb.AppendLine($"[期望策略] {policyText}  (LIDACTION = {(lid.HasValue ? lid.Value.ToString() : "不变")})");

        var (ac, dc, okQ) = PowerManager.QueryLidAction();
        sb.AppendLine($"[当前实际] LIDACTION AC = {(ac >= 0 ? $"0x{ac:X}" : "未知")}，" +
                      $"DC = {(dc >= 0 ? $"0x{dc:X}" : "未知")}（查询{(okQ ? "成功" : "失败")}）");

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
