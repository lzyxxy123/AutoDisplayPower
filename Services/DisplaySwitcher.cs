using System;
using System.Diagnostics;
using AutoDisplayPower.Utils;

namespace AutoDisplayPower.Services;

/// <summary>显示模式切换器：调用系统 DisplaySwitch.exe。</summary>
public static class DisplaySwitcher
{
    public enum Mode
    {
        External,   // /external 仅外接
        Internal,   // /internal 仅笔记本
        Extend,     // /extend   扩展
    }

    public static string ModeText(Mode mode) => mode switch
    {
        Mode.External => "仅外接显示",
        Mode.Internal => "仅笔记本显示",
        _ => "扩展显示",
    };

    public static bool Switch(Mode mode, out string? error)
    {
        string arg = mode switch
        {
            Mode.External => "/external",
            Mode.Internal => "/internal",
            _ => "/extend",
        };

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "DisplaySwitch.exe"),
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add(arg);

            using (var p = Process.Start(psi))
            {
                p?.WaitForExit(15000);
            }

            Logger.Info($"已切换显示模式：{arg}");
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Logger.Error($"切换显示模式失败（{arg}）", ex);
            return false;
        }
    }
}
