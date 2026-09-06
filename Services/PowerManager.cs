using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using AutoDisplayPower.Utils;

namespace AutoDisplayPower.Services;

/// <summary>
/// 电源计划合盖操作(LIDACTION)管理器：
/// 直接以当前用户身份修改“当前活动电源方案”的 AC/DC 合盖设置（无需提权），失败时静默重试一次。
/// </summary>
public static class PowerManager
{
    private const string SchemeCurrent = "SCHEME_CURRENT";
    private const string SubGroupButtons = "SUB_BUTTONS";
    private const string LidActionSetting = "LIDACTION";

    // LIDACTION 设置项 GUID（跨语言/跨方案恒定），用于完整方案输出中的兜底定位
    private const string LidActionSettingGuid = "{5ca83367-6e45-459f-a27b-476b1d01c936}";

    /// <summary>合盖操作取值：0=不操作、1=睡眠(S3)。</summary>
    public const int LidDoNothing = 0;
    public const int LidSleep = 1;

    /// <summary>
    /// 同时设置 AC(接通电源) 与 DC(使用电池) 两项合盖操作，然后 setactive 立即生效。
    /// 任一步失败则整体静默重试一次。
    /// </summary>
    public static bool ApplyLidAction(int acValue, int dcValue, out string? error)
    {
        error = null;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            var steps = new[]
            {
                Run("-setacvalueindex", SchemeCurrent, SubGroupButtons, LidActionSetting, acValue.ToString()),
                Run("-setdcvalueindex", SchemeCurrent, SubGroupButtons, LidActionSetting, dcValue.ToString()),
                Run("-setactive", SchemeCurrent),
            };

            string? firstError = null;
            bool allOk = true;
            foreach (var step in steps)
            {
                if (step.ExitCode != 0)
                {
                    allOk = false;
                    firstError ??= $"powercfg 退出码 {step.ExitCode}：{step.ErrorTail}";
                    break;
                }
            }

            if (allOk)
            {
                Logger.Info($"电源策略已更新：AC LIDACTION={acValue}, DC LIDACTION={dcValue}");
                return true;
            }

            error = firstError;
            Logger.Warn($"powercfg 执行失败（第 {attempt + 1}/2 次）：{firstError}");
            System.Threading.Thread.Sleep(300);
        }

        return false;
    }

    /// <summary>查询当前方案的 AC/DC 合盖值。解析失败返回 ok=false。</summary>
    public static (int Ac, int Dc, bool Ok) QueryLidAction()
    {
        try
        {
            var r = Run("-query", SchemeCurrent, SubGroupButtons, LidActionSetting);
            if (r.ExitCode == 0)
            {
                var direct = ParseAcDc(r.Output ?? string.Empty);
                if (direct is not null) return (direct.Value.Ac, direct.Value.Dc, true);
            }

            // 兜底：个别环境/机型对子组与设置别名解析异常，改从完整方案输出中按 LIDACTION GUID 定位
            var full = Run("-query", SchemeCurrent);
            if (full.ExitCode == 0)
            {
                string text = full.Output ?? string.Empty;
                int idx = text.IndexOf(LidActionSettingGuid, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var fallback = ParseAcDc(text.Substring(idx));
                    if (fallback is not null) return (fallback.Value.Ac, fallback.Value.Dc, true);
                }
            }

            return (-1, -1, false);
        }
        catch (Exception ex)
        {
            Logger.Warn($"查询 LIDACTION 失败：{ex.Message}");
            return (-1, -1, false);
        }
    }

    /// <summary>
    /// 从 powercfg 输出中解析 AC/DC 当前索引：输出依次为“当前交流电源设置索引”与“当前直流电源设置索引”两行，
    /// 取值形如 0x00000001，故取前两个十六进制数即可（语言无关）。
    /// </summary>
    private static (int Ac, int Dc)? ParseAcDc(string text)
    {
        var matches = Regex.Matches(text, @"0x([0-9a-fA-F]+)");
        var values = new List<int>();
        foreach (Match m in matches)
        {
            try { values.Add(Convert.ToInt32(m.Groups[1].Value, 16)); } catch { /* 忽略非法值 */ }
        }

        if (values.Count >= 2) return (values[0], values[1]);
        return null;
    }

    private static (int ExitCode, string Output, string ErrorTail) Run(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "powercfg.exe"),
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
