using System;
using System.Diagnostics;
using AutoDisplayPower.Utils;

namespace AutoDisplayPower.Services;

/// <summary>
/// 开机自启动管理：使用“任务计划程序”而非注册表 Run 键。
/// 以当前登录用户身份创建 At Logon 触发器任务，全程无 UAC 弹窗。
/// </summary>
public static class StartupManager
{
    public const string TaskName = "AutoDisplayPower";

    public static bool Enable(string exePath)
    {
        // 不加 /RU，任务以当前登录用户身份运行；/F 覆盖同名旧任务（如 EXE 路径变化时自动重建）
        var r = Run("/Create", "/F", "/TN", TaskName, "/TR", exePath, "/SC", "ONLOGON");
        if (r.ExitCode == 0)
        {
            Logger.Info($"已创建开机自启动计划任务：{TaskName} -> {exePath}");
            return true;
        }

        Logger.Warn($"创建开机自启动任务失败（退出码 {r.ExitCode}）：{r.ErrorTail}");
        return false;
    }

    public static bool Disable()
    {
        var r = Run("/Delete", "/TN", TaskName, "/F");
        if (r.ExitCode == 0)
        {
            Logger.Info($"已删除开机自启动计划任务：{TaskName}");
            return true;
        }

        Logger.Warn($"删除开机自启动任务失败（退出码 {r.ExitCode}）：{r.ErrorTail}");
        return false;
    }

    public static bool IsEnabled() => Run("/Query", "/TN", TaskName).ExitCode == 0;

    /// <summary>任务当前指向的 EXE 是否与本程序路径一致（用于路径迁移后自动重建）。</summary>
    public static bool PointsTo(string exePath)
    {
        var r = Run("/Query", "/TN", TaskName, "/XML");
        return r.ExitCode == 0
               && (r.Output ?? string.Empty).IndexOf(exePath.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static (int ExitCode, string Output, string ErrorTail) Run(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "schtasks.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        string tail = (string.IsNullOrWhiteSpace(stderr) ? stdout : stderr).Trim();
        if (tail.Length > 0)
        {
            string[] lines = tail.Split('\n');
            tail = lines[lines.Length - 1].Trim();
        }

        return (p.ExitCode, stdout, tail);
    }
}
