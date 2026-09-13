using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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

    /// <summary>调试：把所有菜单图标渲染成 PNG 预览图（放大 5 倍便于查看）。</summary>
    public static void DumpIcons()
    {
        MenuIconFactory.Configure(16);

        var items = new (string Name, Image Img)[]
        {
            ("仅外接", MenuIconFactory.ModeExternal()),
            ("仅笔记本", MenuIconFactory.ModeInternal()),
            ("扩展", MenuIconFactory.ModeExtend()),
            ("盖子-打开", MenuIconFactory.LidIcon(LidState.Open)),
            ("盖子-闭合", MenuIconFactory.LidIcon(LidState.Closed)),
            ("盖子-未知", MenuIconFactory.LidIcon(LidState.Unknown)),
            ("策略-不操作", MenuIconFactory.PolicyIcon(0, false)),
            ("策略-睡眠", MenuIconFactory.PolicyIcon(1, false)),
            ("策略-关机", MenuIconFactory.PolicyIcon(3, false)),
            ("策略-写入失败", MenuIconFactory.PolicyIcon(null, true)),
            ("策略-未知", MenuIconFactory.PolicyIcon(null, false)),
            ("电源(自启动)", MenuIconFactory.Power()),
            ("设置", MenuIconFactory.Settings()),
            ("插上外接屏时", MenuIconFactory.PlugArrow()),
            ("记住上次", MenuIconFactory.History()),
        };

        const int scale = 5;
        const int cols = 5;
        int cell = 16 * scale + 16;
        int rows = (items.Length + cols - 1) / cols;

        using var bmp = new Bitmap(cols * cell, rows * (cell + 24) + 8);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(0xFB, 0xFB, 0xFB));
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            using var font = new Font("Microsoft YaHei UI", 9f);
            using var textBrush = new SolidBrush(Color.FromArgb(0x1B, 0x1B, 0x1B));

            for (int i = 0; i < items.Length; i++)
            {
                int cx = (i % cols) * cell + 8;
                int cy = (i / cols) * (cell + 24) + 8;
                g.DrawImage(items[i].Img, new Rectangle(cx, cy, 16 * scale, 16 * scale));
                g.DrawString(items[i].Name, font, textBrush, cx, cy + 16 * scale + 2);
            }
        }

        string path = Path.Combine(AppContext.BaseDirectory, "icons-preview.png");
        bmp.Save(path, ImageFormat.Png);
        Console.WriteLine("图标预览已生成：" + path);
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
            File.WriteAllText(file, report, new UTF8Encoding(true));
            Console.WriteLine("报告文件：" + file);
        }
        catch
        {
            // 忽略写文件失败
        }
    }
}
