using System;
using System.Linq;
using System.Windows.Forms;
using AutoDisplayPower.Services;
using AutoDisplayPower.Utils;
using Microsoft.Win32;

namespace AutoDisplayPower;

/// <summary>
/// 托盘常驻主控制器（ApplicationContext 承载隐藏主循环）：
/// 轮询 + 系统显示变化事件 → 显示器状态机判定 → 自动切换显示模式 / 对齐合盖电源策略。
/// </summary>
public sealed class MainForm : ApplicationContext
{
    private const string StartupTaskCreatedFlag = "StartupTaskCreated";
    private const int PollIntervalNormalMs = 2000; // 轮询兜底，满足 <3 秒响应要求
    private const int PollIntervalFastMs = 300;    // 收到显示变化事件后的快速重检

    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _miScreens;
    private readonly ToolStripMenuItem _miLid;
    private readonly ToolStripMenuItem _miPolicy;
    private readonly ToolStripMenuItem _miExternal;
    private readonly ToolStripMenuItem _miInternal;
    private readonly ToolStripMenuItem _miExtend;
    private readonly ToolStripMenuItem _miStartup;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly object _gate = new();

    private MonitorScanResult? _prev;               // 上一次成功扫描结果（用于判定插拔）
    private MonitorScanResult _current = new();     // 最近一次展示用结果
    private int _appliedLid = -1;                   // 最近一次成功下发的 LIDACTION；-1=尚未下发
    private DateTime _lastApplyFailUtc = DateTime.MinValue;
    private bool _eventPending;
    private bool _syncingStartup;
    private bool _suppressBalloons = true;          // 启动首次对齐时不弹气球，避免开机骚扰
    private string _lastCore = string.Empty;

    public MainForm()
    {
        _tray = new NotifyIcon
        {
            Icon = TrayIconFactory.Create(),
            Text = "AutoDisplayPower 智能显示器电源管理",
            Visible = true,
        };

        _miScreens = new ToolStripMenuItem("  当前屏幕：—") { Enabled = false };
        _miLid = new ToolStripMenuItem("  推断盖子状态：—") { Enabled = false };
        _miPolicy = new ToolStripMenuItem("  电源策略：—") { Enabled = false };
        _miExternal = new ToolStripMenuItem("🖥️ 仅外接");
        _miInternal = new ToolStripMenuItem("💻 仅笔记本");
        _miExtend = new ToolStripMenuItem("🔄 扩展");
        _miStartup = new ToolStripMenuItem("开机自启动") { CheckOnClick = true };

        _menu = new ContextMenuStrip();
        _menu.Items.Add(new ToolStripMenuItem("📊 状态（只读）") { Enabled = false });
        _menu.Items.Add(_miScreens);
        _menu.Items.Add(_miLid);
        _menu.Items.Add(_miPolicy);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_miExternal);
        _menu.Items.Add(_miInternal);
        _menu.Items.Add(_miExtend);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_miStartup);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => Application.Exit()));

        _tray.ContextMenuStrip = _menu;
        _tray.DoubleClick += OnTrayDoubleClick;
        _menu.Opening += (_, _) => RefreshStatusFromCurrent();

        _miExternal.Click += (_, _) => ManualSwitch(DisplaySwitcher.Mode.External);
        _miInternal.Click += (_, _) => ManualSwitch(DisplaySwitcher.Mode.Internal);
        _miExtend.Click += (_, _) => ManualSwitch(DisplaySwitcher.Mode.Extend);
        _miStartup.CheckedChanged += (_, _) => OnStartupToggle();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _pollTimer = new System.Windows.Forms.Timer { Interval = PollIntervalNormalMs };
        _pollTimer.Tick += OnTimerTick;
        _pollTimer.Start();

        EnsureStartupTask();

        // 启动对齐：应用当前状态对应的电源策略并刷新状态（不自动切换显示模式）
        Orchestrate();
    }

    // ---------------- 主循环 ----------------

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_eventPending)
        {
            _eventPending = false;
            _pollTimer.Interval = PollIntervalNormalMs;
        }

        Orchestrate();
    }

    /// <summary>系统显示配置变化事件（可能在非 UI 线程触发，只做轻量标记，由 UI 定时器执行检测）。</summary>
    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        _eventPending = true;
        _pollTimer.Interval = PollIntervalFastMs;
    }

    private void Orchestrate()
    {
        if (!Monitor.TryEnter(_gate)) return;
        try
        {
            var snap = DisplayDetector.Scan();
            if (!snap.Ok)
            {
                Logger.Warn($"显示器扫描失败：{snap.Error}（保持现状，稍后自动重试）");
                return; // 不覆盖 _prev，避免把“读取失败”误判为显示器拔出
            }

            bool extAdded = _prev is { HasExternal: false } && snap.HasExternal;
            bool extRemoved = _prev is { HasExternal: true } && !snap.HasExternal;

            if (extAdded) DoAutoSwitch(DisplaySwitcher.Mode.External);
            else if (extRemoved) DoAutoSwitch(DisplaySwitcher.Mode.Internal);

            if (extAdded || extRemoved)
            {
                System.Threading.Thread.Sleep(1000); // 等待显示切换稳定后再扫描，避免读到中间态
                snap = DisplayDetector.Scan();
                if (!snap.Ok) snap = _prev ?? new MonitorScanResult();
            }

            var prevState = _prev?.State;
            _prev = snap;
            _current = snap;

            if (snap.State == ScreenState.Unknown && prevState != ScreenState.Unknown)
            {
                Logger.Warn(snap.Monitors.Count == 0
                    ? "未检测到任何已知显示器（空场景），保持当前电源策略不变。"
                    : $"存在无法识别的显示器场景（共 {snap.Monitors.Count} 台），保持当前电源策略不变。");
            }

            AlignPolicy(snap);
            UpdateStatusFrom(snap);
        }
        catch (Exception ex)
        {
            Logger.Error("主循环异常", ex);
        }
        finally
        {
            Monitor.Exit(_gate);
            _suppressBalloons = false;
        }
    }

    private void DoAutoSwitch(DisplaySwitcher.Mode mode)
    {
        if (DisplaySwitcher.Switch(mode, out string? err))
        {
            string action = mode == DisplaySwitcher.Mode.External ? "接入外接屏" : "拔掉外接屏";
            Logger.Info($"{action} → 自动切换：{DisplaySwitcher.ModeText(mode)}");
            if (!_suppressBalloons)
                TryBalloon($"{action}，已自动切换：{DisplaySwitcher.ModeText(mode)}");
        }
        else
        {
            Logger.Error($"自动切换显示模式失败：{err}");
        }
    }

    /// <summary>把当前状态对应的合盖策略应用到 AC/DC（只修改，不强制切换显示）。</summary>
    private void AlignPolicy(MonitorScanResult snap)
    {
        (int? lid, _) = StateRules.ExpectedPolicy(snap.State);
        if (lid is null) return; // 未知状态：保持现状（日志已在 Orchestrate 中处理）

        int target = lid.Value;

        // 首次下发前先读一次当前实际值，若已一致则避免无谓写入
        if (_appliedLid < 0)
        {
            var (ac, dc, ok) = PowerManager.QueryLidAction();
            _appliedLid = ok && ac == dc ? ac : -2; // AC/DC 不一致或读取失败 → 强制按目标统一写入
        }

        if (_appliedLid == target) return;

        // 失败冷却 30 秒，避免每次都触发一次失败的 powercfg 写入
        if (DateTime.UtcNow - _lastApplyFailUtc < TimeSpan.FromSeconds(30)) return;

        if (PowerManager.ApplyLidAction(target, target, out string? err))
        {
            _appliedLid = target;
            Logger.Info($"状态 {snap.State} → 已下发合盖策略：{StateRules.DescribeLidValue(target)}");
            if (!_suppressBalloons)
                TryBalloon($"显示器状态已变化，合盖电源策略已更新为：{StateRules.DescribeLidValue(target)}");
        }
        else
        {
            _lastApplyFailUtc = DateTime.UtcNow;
            _appliedLid = -2;
            Logger.Error($"合盖策略下发失败：{err}");
        }
    }

    // ---------------- 状态展示 ----------------

    private void RefreshStatusFromCurrent()
    {
        _lastCore = string.Empty;
        UpdateStatusFrom(_current);
    }

    private void UpdateStatusFrom(MonitorScanResult snap)
    {
        if (!snap.Ok) return;

        string screens = snap.Monitors.Count == 0
            ? "未检测到显示器"
            : string.Join("、", snap.Monitors.Select(m => m.ModelName).Distinct());
        string lid = StateRules.InferLidText(snap.State);
        string core = $"{snap.State}|{screens}|{lid}";
        if (core == _lastCore) return;

        _lastCore = core;
        _miScreens.Text = "  当前屏幕：" + screens;
        _miLid.Text = "  推断盖子状态：" + lid;
        _miPolicy.Text = "  电源策略：" + QueryPolicyText();
    }

    private string QueryPolicyText()
    {
        var (ac, dc, ok) = PowerManager.QueryLidAction();
        if (!ok) return "查询失败";
        return ac == dc
            ? StateRules.DescribeLidValue(ac)
            : $"AC {StateRules.DescribeLidValue(ac)} / DC {StateRules.DescribeLidValue(dc)}";
    }

    // ---------------- 手动操作 ----------------

    private void ManualSwitch(DisplaySwitcher.Mode mode)
    {
        if (DisplaySwitcher.Switch(mode, out string? err))
        {
            Logger.Info($"手动切换：{DisplaySwitcher.ModeText(mode)}");
            TryBalloon($"已切换：{DisplaySwitcher.ModeText(mode)}");
            _lastCore = string.Empty; // 稍后事件/轮询会刷新状态与策略
        }
        else
        {
            Logger.Error($"手动切换失败：{err}");
            TryBalloon($"切换失败：{err}");
        }
    }

    private void OnTrayDoubleClick(object? sender, EventArgs e)
    {
        var snap = DisplayDetector.Scan();
        if (!snap.Ok)
        {
            TryBalloon("显示器状态读取失败");
            return;
        }

        TryBalloon(
            $"屏幕状态：{StateRules.Describe(snap.State)}\n" +
            $"推断盖子：{StateRules.InferLidText(snap.State)}\n" +
            $"期望策略：{StateRules.ExpectedPolicy(snap.State).PolicyText}");
    }

    // ---------------- 开机自启动 ----------------

    private void EnsureStartupTask()
    {
        try
        {
            bool firstRun = !AppSettings.ReadBool(StartupTaskCreatedFlag, false);
            if (firstRun)
            {
                // 规格书 3.4：首次运行默认强制开启
                if (!StartupManager.Enable(Application.ExecutablePath))
                    Logger.Warn("首次运行创建开机自启动任务失败，可在托盘菜单中手动开启。");
                AppSettings.WriteBool(StartupTaskCreatedFlag, true);
            }
            else if (StartupManager.IsEnabled() && !StartupManager.PointsTo(Application.ExecutablePath))
            {
                StartupManager.Enable(Application.ExecutablePath); // EXE 被移动/改名后自动重建
            }

            _syncingStartup = true;
            _miStartup.Checked = StartupManager.IsEnabled();
            _syncingStartup = false;
        }
        catch (Exception ex)
        {
            _syncingStartup = false;
            Logger.Error("初始化开机自启动任务失败", ex);
        }
    }

    private void OnStartupToggle()
    {
        if (_syncingStartup) return;
        try
        {
            bool ok = _miStartup.Checked
                ? StartupManager.Enable(Application.ExecutablePath)
                : StartupManager.Disable();
            if (!ok) RevertStartupCheck();
        }
        catch (Exception ex)
        {
            Logger.Error("处理开机自启动勾选失败", ex);
            RevertStartupCheck();
        }
    }

    private void RevertStartupCheck()
    {
        _syncingStartup = true;
        _miStartup.Checked = StartupManager.IsEnabled();
        _syncingStartup = false;
        TryBalloon("开机自启动任务操作失败（任务计划程序权限受限？），已恢复原状态。");
    }

    // ---------------- 辅助 ----------------

    private void TryBalloon(string text)
    {
        try
        {
            _tray.BalloonTipTitle = "AutoDisplayPower";
            _tray.BalloonTipText = text;
            _tray.BalloonTipIcon = ToolTipIcon.Info;
            _tray.ShowBalloonTip(2000);
        }
        catch
        {
            // 个别精简系统不支持气球提示，忽略
        }
    }

    protected override void ExitThreadCore()
    {
        _pollTimer.Stop();
        _pollTimer.Dispose();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }

        _menu.Dispose();
        base.ExitThreadCore();
    }
}
