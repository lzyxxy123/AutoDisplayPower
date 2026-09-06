using System;
using System.Diagnostics;
using AutoDisplayPower.Utils;
using Microsoft.Win32;

namespace AutoDisplayPower.Services;

/// <summary>
/// 开机自启动管理：
/// 优先使用“任务计划程序”（At Logon，当前用户，无 UAC）；
/// 若任务计划程序受系统限制无法创建（如受限会话），自动回退写入 HKCU Run 键，保证自启动可用。
/// </summary>
public static class StartupManager
{
    public const string TaskName = "AutoDisplayPower";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "AutoDisplayPower";

    public static bool Enable(string exePath)
    {
        // 1) 任务计划程序
        var r = Run("/Create", "/F", "/TN", TaskName, "/TR", exePath, "/SC", "ONLOGON");
        if (r.ExitCode == 0)
        {
            Logger.Info($"已创建开机自启动计划任务：{TaskName} -> {exePath}");
            return true;
        }

        Logger.Warn($"任务计划程序创建失败（退出码 {r.ExitCode}）：{r.ErrorTail}，回退注册表 Run 键。");

        // 2) 回退：HKCU Run 键（无需管理员、不触发 UAC）
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            key?.SetValue(RunValueName, "\"" + exePath + "\"");
            Logger.Info($"已通过 HKCU Run 键启用开机自启动（回退方案）。");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"注册表 Run 键回退失败", ex);
            return false;
        }
    }

    public static bool Disable()
    {
        var r = Run("/Delete", "/TN", TaskName, "/F");
        bool taskOk = r.ExitCode == 0;
        bool runKeyOk = true;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
        catch
        {
            runKeyOk = false;
        }

        bool ok = taskOk || runKeyOk;
        if (ok) Logger.Info("已关闭开机自启动（任务计划程序与 Run 键均已清理）。");
        return ok;
    }

    public static bool IsEnabled() => Run("/Query", "/TN", TaskName).ExitCode == 0 || RunKeyExists();

    /// <summary>任务当前指向的 EXE 是否与本程序路径一致（用于路径迁移后自动重建）。</summary>
    public static bool PointsTo(string exePath)
    {
        if (Run("/Query", "/TN", TaskName, "/XML").ExitCode == 0
            && (Run("/Query", "/TN", TaskName, "/XML").Output ?? string.Empty)
                .IndexOf(exePath.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            string? v = key?.GetValue(RunValueName) as string;
            return v is not null && v.IndexOf(exePath.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool RunKeyExists()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(RunValueName) != null;
        }
        catch
        {
            return false;
        }
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
