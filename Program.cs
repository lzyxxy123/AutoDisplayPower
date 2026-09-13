using System;
using System.Threading;
using System.Windows.Forms;
using AutoDisplayPower.Utils;

namespace AutoDisplayPower;

internal static class Program
{
    /// <summary>
    /// 应用程序主入口。
    /// --check            只读自检（打印检测到的显示器/状态/策略，不修改任何设置）
    /// --selftest-power   配合 --check 使用：以当前 AC/DC 值原样回写，验证当前用户是否有权修改电源方案
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        bool checkMode = args.Length > 0 && (args[0] == "--check" || args[0] == "-c");
        bool selfTestPower = Array.IndexOf(args, "--selftest-power") >= 0;
        bool topologyMode = Array.IndexOf(args, "--topology") >= 0;
        bool iconMode = Array.IndexOf(args, "--icons") >= 0;
        bool mockupMode = Array.IndexOf(args, "--mockup") >= 0;

        if (mockupMode)
        {
            CheckMode.DumpMockups();
            return;
        }

        if (iconMode)
        {
            CheckMode.DumpIcons();
            return;
        }

        if (topologyMode)
        {
            CheckMode.DumpTopology();
            return;
        }

        if (checkMode)
        {
            CheckMode.Run(selfTestPower);
            return;
        }

        // 单实例：已运行时直接退出，避免出现多个托盘图标
        using (var mutex = new Mutex(true, @"Local\AutoDisplayPower.SingleInstance", out bool createdNew))
        {
            if (!createdNew)
            {
                Logger.Info("检测到已运行的实例，本次启动直接退出。");
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var app = new MainForm();
            Application.Run(app);

            Logger.Info("程序退出。");
        }
    }
}
