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
    private readonly ToolStripMenuItem _miStatusTitle;
    private readonly ToolStripMenuItem _miScreens;
    private readonly ToolStripMenuItem _miLid;
    private readonly ToolStripMenuItem _miPolicy;
    private readonly ToolStripMenuItem _miExternal;
    private readonly ToolStripMenuItem _miInternal;
    private readonly ToolStripMenuItem _miExtend;
    private readonly ToolStripMenuItem _miPlugMenu;
    private readonly ToolStripMenuItem _miPlugExternal;
    private readonly ToolStripMenuItem _miPlugExtend;
    private readonly ToolStripMenuItem _miPlugRemember;
    private readonly ToolStripMenuItem _miStartup;
    private readonly ToolStripMenuItem _miConfigDisplay;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly System.Windows.Forms.Timer _emphasisTimer; // 策略变化后的短暂强调
    private readonly Font _policyBoldFont;
    private readonly object _gate = new();

    private MonitorScanResult? _prev;               // 上一次成功扫描结果（用于判定插拔）
    private MonitorScanResult _current = new();     // 最近一次展示用结果
    private int _appliedLid = -1;                   // 最近一次成功下发的 LIDACTION；-1=尚未下发
    private DateTime _lastApplyFailUtc = DateTime.MinValue;
    private bool _eventPending;
    private bool _syncingStartup;
    private bool _suppressBalloons = true;          // 启动首次对齐时不弹气球，避免开机骚扰
    private string? _lastApplyError;                // 上次下发失败信息（相同错误只记一次日志）
    private DisplaySwitcher.Mode? _pendingSwitchMode; // 待汇总通知：本次成功切换到的显示模式
    private string? _pendingSwitchFailure;            // 待汇总通知：本次切换失败提示
    private int? _pendingPolicyValue;                 // 待汇总通知：本次下发的合盖策略
    private string _lastCore = string.Empty;
    private string _lastPolicyText = string.Empty;     // 上次显示的电源策略文本（用于检测变化）
    private bool _policyEmphasizing;                   // 策略刚变化，正在强调显示

    public MainForm()
    {
        _tray = new NotifyIcon
        {
            Icon = TrayIconFactory.GetStateIcon(ScreenState.Unknown),
            Text = "AutoDisplayPower 智能显示器电源管理",
            Visible = true,
        };

        // 先建菜单，按实际 DPI 生成彩色矢量图标
        _menu = new ContextMenuStrip { Renderer = new ModernMenuRenderer() };
        int iconSize = Math.Max(16, (int)Math.Round(16.0 * _menu.DeviceDpi / 96.0));
        MenuIconFactory.Configure(iconSize);
        _menu.ImageScalingSize = new Size(iconSize, iconSize);
        _policyBoldFont = new Font(_menu.Font, FontStyle.Bold);

        _miStatusTitle = new ToolStripMenuItem("状态")
        {
            Enabled = false, ForeColor = UiTheme.TextPrimary, Font = _policyBoldFont, Image = MenuIconFactory.Blank(),
        };
        _miScreens = new ToolStripMenuItem("当前屏幕：—")
        {
            Enabled = false, ForeColor = UiTheme.TextPrimary, Image = MenuIconFactory.ScreenIcon(ScreenState.Unknown),
        };
        _miLid = new ToolStripMenuItem("盖子状态：—")
        {
            Enabled = false, ForeColor = UiTheme.TextPrimary, Image = MenuIconFactory.LidIcon(LidState.Unknown),
        };
        _miPolicy = new ToolStripMenuItem("电源策略：—")
        {
            Enabled = false, ForeColor = UiTheme.TextPrimary, Image = MenuIconFactory.PolicyIcon(null, false),
        };

        // 三个模式项：图标彩色；当前模式用“整行浅色高亮”表示（Tag=Mode，不画对钩）
        _miExternal = new ToolStripMenuItem("仅外接") { Tag = MenuTag.Mode, Image = MenuIconFactory.ModeExternal() };
        _miInternal = new ToolStripMenuItem("仅笔记本") { Tag = MenuTag.Mode, Image = MenuIconFactory.ModeInternal() };
        _miExtend = new ToolStripMenuItem("扩展") { Tag = MenuTag.Mode, Image = MenuIconFactory.ModeExtend() };

        // “插上外接屏时”子菜单（三选一，选中项同样整行高亮）
        _miPlugExternal = new ToolStripMenuItem("始终「仅外接」") { Tag = MenuTag.Mode, Image = MenuIconFactory.ModeExternal() };
        _miPlugExtend = new ToolStripMenuItem("始终「扩展」") { Tag = MenuTag.Mode, Image = MenuIconFactory.ModeExtend() };
        _miPlugRemember = new ToolStripMenuItem("记住上次选择") { Tag = MenuTag.Mode, Image = MenuIconFactory.History() };
        _miPlugMenu = new ToolStripMenuItem("插上外接屏时") { Image = MenuIconFactory.PlugArrow() };
        _miPlugMenu.DropDownItems.Add(_miPlugExternal);
        _miPlugMenu.DropDownItems.Add(_miPlugExtend);
        _miPlugMenu.DropDownItems.Add(new ToolStripSeparator());
        _miPlugMenu.DropDownItems.Add(_miPlugRemember);

        // 开机自启动：右侧滑动开关
        _miStartup = new ToolStripMenuItem("开机自启动")
        {
            CheckOnClick = true, Tag = MenuTag.Switch, Image = MenuIconFactory.Power(),
        };
        _miConfigDisplay = new ToolStripMenuItem("显示器型号配置…") { Image = MenuIconFactory.Settings() };

        _menu.Items.Add(_miStatusTitle);
        _menu.Items.Add(_miScreens);
        _menu.Items.Add(_miLid);
        _menu.Items.Add(_miPolicy);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_miExternal);
        _menu.Items.Add(_miInternal);
        _menu.Items.Add(_miExtend);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_miPlugMenu);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_miStartup);
        _menu.Items.Add(_miConfigDisplay);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => Application.Exit()) { Image = MenuIconFactory.Blank() });

        // 所有项都不缩放图标，并统一用近黑文字（彩色语义交给图标）
        foreach (ToolStripItem item in AllMenuItems())
        {
            item.ImageScaling = ToolStripItemImageScaling.None;
            if (item is ToolStripMenuItem menuItem) menuItem.ForeColor = UiTheme.TextPrimary;
        }

        _tray.ContextMenuStrip = _menu;
        _tray.DoubleClick += OnTrayDoubleClick;
        _menu.Opening += (_, _) =>
        {
            RefreshStatusFromCurrent();
            RefreshPlugMenu(); // 子菜单对钩/当前记录也保持最新
            SyncMenuColors();
        };

        _miExternal.Click += (_, _) => ManualSwitch(DisplaySwitcher.Mode.External);
        _miInternal.Click += (_, _) => ManualSwitch(DisplaySwitcher.Mode.Internal);
        _miExtend.Click += (_, _) => ManualSwitch(DisplaySwitcher.Mode.Extend);
        _miStartup.CheckedChanged += (_, _) => OnStartupToggle();
        _miConfigDisplay.Click += (_, _) => OpenConfig();

        _miPlugExternal.Click += (_, _) => SetPlugBehavior(PlugBehavior.FixedExternal);
        _miPlugExtend.Click += (_, _) => SetPlugBehavior(PlugBehavior.FixedExtend);
        _miPlugRemember.Click += (_, _) => SetPlugBehavior(PlugBehavior.RememberLast);
        RefreshPlugMenu();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _emphasisTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _emphasisTimer.Tick += (_, _) => EndPolicyEmphasis();

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

            // 自动切换后重新检测校验：未达到目标模式则记为失败（不再一律报“已切换”）
            if (extAdded)
            {
                // 插上外接屏时的模式：始终「仅外接」/ 始终「扩展」/ 记住上次选择
                DisplaySwitcher.Mode plugMode = PlugPolicy.ResolvePlugMode();
                snap = ApplySwitch(plugMode, $"接入外接屏（{PlugPolicy.DescribeBehavior()}）");
            }
            else if (extRemoved)
            {
                snap = ApplySwitch(DisplaySwitcher.Mode.Internal, "拔掉外接屏");
            }

            if (!snap.Ok) snap = _prev ?? new MonitorScanResult(); // 校验期读取失败则沿用上次结果

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
            NotifyPendingChanges(snap);
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

    /// <summary>
    /// 执行显示模式切换，并在约 1 秒后【重新检测屏幕集合】校验是否真的达到目标模式。
    /// 达到 → 记为待汇总的“成功切换”；未达到 → 记为“切换失败（附当前实际屏幕）”。
    /// 返回切换校验后的扫描结果。
    /// </summary>
    private MonitorScanResult ApplySwitch(DisplaySwitcher.Mode mode, string reason)
    {
        if (!DisplaySwitcher.Switch(mode, out string? err))
        {
            Logger.Error($"{reason} → 切换执行失败：{err}");
            _pendingSwitchFailure = $"切换失败：{err}";
            return DisplayDetector.Scan();
        }

        Logger.Info($"{reason} → 已执行切换：{DisplaySwitcher.ModeText(mode)}，正在校验…");
        System.Threading.Thread.Sleep(1000); // 等待显示切换稳定后再检测，避免读到中间态

        var after = DisplayDetector.Scan();
        if (after.Ok && ModeAchieved(mode, after))
        {
            _pendingSwitchMode = mode;
            Logger.Info($"切换校验通过：{DisplaySwitcher.ModeText(mode)}");
        }
        else
        {
            _pendingSwitchFailure = BuildSwitchFailureText(mode, after);
            Logger.Warn($"切换校验失败：{_pendingSwitchFailure}");
        }

        return after;
    }

    /// <summary>校验目标显示模式是否真的达成（严格匹配：仅笔记本 / 仅外接 / 双屏扩展）。</summary>
    private static bool ModeAchieved(DisplaySwitcher.Mode mode, MonitorScanResult snap) => mode switch
    {
        DisplaySwitcher.Mode.Internal => snap.State == ScreenState.InternalOnly,
        DisplaySwitcher.Mode.External => snap.State == ScreenState.ExternalOnly,
        _ => snap.State == ScreenState.Extended, // 扩展必须双屏同亮
    };

    /// <summary>生成切换失败提示，附带当前实际检测到的屏幕。</summary>
    private static string BuildSwitchFailureText(DisplaySwitcher.Mode mode, MonitorScanResult snap)
    {
        if (!snap.Ok) return "切换失败：屏幕状态读取失败";

        string current = snap.Monitors.Count == 0
            ? "未检测到任何屏幕"
            : string.Join("、", snap.Monitors.Select(m => m.ModelName).Distinct());

        // 目标模式所需的屏幕是否在现场
        bool needsInternal = mode != DisplaySwitcher.Mode.External;   // 仅笔记本 / 扩展
        bool needsExternal = mode != DisplaySwitcher.Mode.Internal;   // 仅外接 / 扩展
        bool missingInternal = needsInternal && !snap.HasInternal;
        bool missingExternal = needsExternal && !snap.HasExternal;

        if (missingInternal || missingExternal)
        {
            string missing = (missingInternal, missingExternal) switch
            {
                (true, true) => "外接屏幕与笔记本屏幕",
                (true, false) => "笔记本屏幕",
                _ => "外接屏幕",
            };
            return $"切换失败：未检测到{missing}（当前检测到：{current}）";
        }

        // 所需屏幕都在，但模式未生效（例如另一块屏无法关闭）
        return $"切换失败：切换未生效（当前检测到：{current}）";
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
            _pendingPolicyValue = target; // 不单独弹通知，汇总到本轮检测末尾统一弹一条
        }
        else
        {
            _lastApplyFailUtc = DateTime.UtcNow;
            _appliedLid = -2;
            if (_lastApplyError != err)
            {
                _lastApplyError = err;
                Logger.Error($"合盖策略下发失败：{err}");
            }
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

        // 三个模式项：当前模式用“整行浅色高亮”表示（Checked 仅作状态标记，渲染器不画对钩）
        _miExternal.Checked = snap.State == ScreenState.ExternalOnly;
        _miInternal.Checked = snap.State == ScreenState.InternalOnly;
        _miExtend.Checked = snap.State == ScreenState.Extended;

        // 托盘图标颜色随屏幕状态变化
        _tray.Icon = TrayIconFactory.GetStateIcon(snap.State);

        string screens = snap.Monitors.Count == 0
            ? "未检测到显示器"
            : string.Join("、", snap.Monitors.Select(m => m.ModelName).Distinct());
        string lid = StateRules.LidText(snap.Lid);

        // 电源方案值只查询一次，供 策略图标 / 悬停提示 / 策略文本 复用（避免重复启动 powercfg）
        var policy = PowerManager.QueryLidAction();

        // 状态行图标随状态变色（文字统一近黑，语义交给图标）
        _miScreens.Image = MenuIconFactory.ScreenIcon(snap.State);
        _miLid.Image = MenuIconFactory.LidIcon(snap.Lid);
        _miPolicy.Image = MenuIconFactory.PolicyIcon(ResolvePolicyIconValue(snap, policy), IsPolicyWriteFailed(policy));

        // 悬停托盘图标时显示的单行摘要（含 屏幕 / 盖子 / 电源策略）
        _tray.Text = BuildTrayTooltip(snap, policy);

        // 电源策略文本变化 → 短暂强调（加粗 + 浅色高亮 + “已更新”）
        string policyText = QueryPolicyText(snap, policy);
        if (policyText != _lastPolicyText)
        {
            _lastPolicyText = policyText;
            BeginPolicyEmphasis();
        }
        _miPolicy.Text = "电源策略：" + policyText + (_policyEmphasizing ? "（已更新）" : "");

        string core = $"{snap.State}|{screens}|{lid}";
        if (core == _lastCore) return;

        _lastCore = core;
        _miScreens.Text = "当前屏幕：" + screens;
        _miLid.Text = "盖子状态：" + lid;
    }

    /// <summary>策略图标用值：优先真实方案值，读不到则用当前状态对应的期望值。</summary>
    private static int? ResolvePolicyIconValue(MonitorScanResult snap, (int Ac, int Dc, bool Ok) policy)
    {
        if (policy.Ok) return policy.Ac == policy.Dc ? policy.Ac : null;
        return StateRules.ExpectedPolicy(snap.State).LidAction;
    }

    /// <summary>是否处于“下发失败”状态（无权限/本机方案无合盖项）。</summary>
    private bool IsPolicyWriteFailed((int Ac, int Dc, bool Ok) policy)
        => !policy.Ok && _appliedLid == -2;

    /// <summary>策略变化：加粗 + 浅色高亮，3 秒后恢复。</summary>
    private void BeginPolicyEmphasis()
    {
        if (_suppressBalloons) return; // 启动首次对齐不强调

        _policyEmphasizing = true;
        _miPolicy.Tag = MenuTag.Emphasize;
        _miPolicy.Font = _policyBoldFont;
        _emphasisTimer.Stop();
        _emphasisTimer.Start();
        _menu.Invalidate();
    }

    private void EndPolicyEmphasis()
    {
        _emphasisTimer.Stop();
        _policyEmphasizing = false;
        _miPolicy.Tag = null;
        _miPolicy.Font = null; // 恢复默认字体
        _miPolicy.Text = "电源策略：" + _lastPolicyText;
        _menu.Invalidate();
    }

    /// <summary>悬停托盘提示：单行摘要（系统提示不支持多行，长度也有限）。</summary>
    private static string BuildTrayTooltip(MonitorScanResult snap, (int Ac, int Dc, bool Ok) policy)
    {
        string screen = snap.State switch
        {
            ScreenState.ExternalOnly => "仅外接",
            ScreenState.InternalOnly => "仅笔记本",
            ScreenState.Extended => "扩展",
            _ => "未知",
        };
        string lid = snap.Lid switch
        {
            LidState.Open => "盖子打开",
            LidState.Closed => "盖子闭合",
            _ => "盖子未知",
        };

        string policyText = DescribePolicyShort(snap, policy);
        string text = $"{screen} · {lid} · {policyText}";
        return text.Length > 60 ? text[..60] : text;
    }

    /// <summary>电源策略的短文本（读不到方案值时回落到当前状态对应的期望策略）。</summary>
    private static string DescribePolicyShort(MonitorScanResult snap, (int Ac, int Dc, bool Ok) policy)
    {
        if (policy.Ok && policy.Ac == policy.Dc) return StateRules.DescribeLidValue(policy.Ac);
        if (policy.Ok) return $"AC {StateRules.DescribeLidValue(policy.Ac)} / DC {StateRules.DescribeLidValue(policy.Dc)}";

        var (expected, _) = StateRules.ExpectedPolicy(snap.State);
        return expected is null ? "策略未知" : StateRules.DescribeLidValue(expected.Value);
    }

    /// <summary>切换“插上外接屏时”的行为并保存。</summary>
    private void SetPlugBehavior(PlugBehavior behavior)
    {
        PlugPolicy.Behavior = behavior;
        RefreshPlugMenu();
        Logger.Info($"插上外接屏时的行为已设为：{PlugPolicy.DescribeBehavior()}");
    }

    /// <summary>刷新“插上外接屏时”子菜单的选中项（选中项由渲染器画整行浅色高亮）。</summary>
    private void RefreshPlugMenu()
    {
        PlugBehavior behavior = PlugPolicy.Behavior;
        _miPlugExternal.Checked = behavior == PlugBehavior.FixedExternal;
        _miPlugExtend.Checked = behavior == PlugBehavior.FixedExtend;
        _miPlugRemember.Checked = behavior == PlugBehavior.RememberLast;

        // 在“记住上次选择”上显示当前记录的是哪一种
        _miPlugRemember.Text = behavior == PlugBehavior.RememberLast
            ? $"记住上次选择（当前：{(PlugPolicy.LastMode == DisplaySwitcher.Mode.Extend ? "扩展" : "仅外接")}）"
            : "记住上次选择";
    }

    /// <summary>遍历主菜单与子菜单的全部项。</summary>
    private IEnumerable<ToolStripItem> AllMenuItems()
    {
        foreach (ToolStripItem item in _menu.Items) yield return item;
        foreach (ToolStripItem item in _miPlugMenu.DropDownItems) yield return item;
    }

    /// <summary>开机自启动的开关状态由滑动开关呈现，文字保持近黑。</summary>
    private void SyncMenuColors()
    {
        _miStartup.ForeColor = UiTheme.TextPrimary;
    }

    /// <summary>
    /// 显示合盖电源策略：优先读电源方案真实值；
    /// 读不到（本机方案无合盖项 / 当前会话无权限）时，显示本程序正在管理的策略并标注写入状态。
    /// </summary>
    private string QueryPolicyText(MonitorScanResult snap, (int Ac, int Dc, bool Ok) policy)
    {
        if (policy.Ok)
        {
            return policy.Ac == policy.Dc
                ? StateRules.DescribeLidValue(policy.Ac)
                : $"AC {StateRules.DescribeLidValue(policy.Ac)} / DC {StateRules.DescribeLidValue(policy.Dc)}";
        }

        var (expected, _) = StateRules.ExpectedPolicy(snap.State);
        if (expected is null) return "未管理（屏幕状态未知）";

        string text = StateRules.DescribeLidValue(expected.Value);
        if (_appliedLid == expected.Value) return text + "（已下发）";
        if (_appliedLid == -2) return text + "（写入失败，需权限）";
        return text + "（待下发）";
    }

    // ---------------- 手动操作 ----------------

    private void OpenConfig()
    {
        try
        {
            using var dlg = new ConfigForm();
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _lastCore = string.Empty; // 配置变更后刷新状态
                Orchestrate();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("打开显示器型号配置失败", ex);
            TryBalloon("打开配置失败：" + ex.Message);
        }
    }

    /// <summary>
    /// 把本次检测中发生的“显示切换/切换失败”和“电源策略更新”汇总成【一条】气泡通知。
    /// 成功示例：已切换为仅笔记本屏幕，电源策略为合盖睡眠
    /// 失败示例：切换失败：未检测到笔记本屏幕（当前检测到：外接屏 (KG257S PLUS)）
    /// </summary>
    private void NotifyPendingChanges(MonitorScanResult snap)
    {
        if (_pendingSwitchMode is null && _pendingSwitchFailure is null && _pendingPolicyValue is null) return;

        if (_suppressBalloons)
        {
            // 启动首次对齐不打扰用户
            _pendingSwitchMode = null;
            _pendingSwitchFailure = null;
            _pendingPolicyValue = null;
            return;
        }

        string text;
        if (_pendingSwitchFailure is not null)
        {
            // 切换失败：以失败提示为主
            text = _pendingSwitchFailure;
            if (_pendingPolicyValue is { } failedPolicy)
                text += $"，电源策略为{StateRules.DescribeLidValue(failedPolicy)}";
        }
        else
        {
            // 策略部分：优先用本次实际下发的值；若只切换了屏幕而策略未变，用当前状态对应的策略
            int? policyValue = _pendingPolicyValue;
            if (policyValue is null && _pendingSwitchMode is not null)
                policyValue = StateRules.ExpectedPolicy(snap.State).LidAction;

            string? switchPart = _pendingSwitchMode is { } m ? $"已切换为{ModeShortText(m)}屏幕" : null;
            string? policyPart = policyValue is { } p ? $"电源策略为{StateRules.DescribeLidValue(p)}" : null;

            text = switchPart is not null && policyPart is not null
                ? $"{switchPart}，{policyPart}"
                : switchPart ?? policyPart!;
        }

        _pendingSwitchMode = null;
        _pendingSwitchFailure = null;
        _pendingPolicyValue = null;
        TryBalloon(text);
    }

    private static string ModeShortText(DisplaySwitcher.Mode mode) => mode switch
    {
        DisplaySwitcher.Mode.External => "仅外接",
        DisplaySwitcher.Mode.Internal => "仅笔记本",
        _ => "扩展",
    };

    private void ManualSwitch(DisplaySwitcher.Mode mode)
    {
        try
        {
            // 执行切换并校验实际结果（约1秒），再统一弹一条通知
            var after = ApplySwitch(mode, "手动切换");
            if (after.Ok)
            {
                _prev = after;
                _current = after;

                // 记录“上次选择”：仅当外接屏在场、且切换成功，且选的是「仅外接/扩展」
                if (after.HasExternal && ModeAchieved(mode, after))
                {
                    PlugPolicy.RememberChoice(mode);
                    RefreshPlugMenu();
                }
            }
            _lastCore = string.Empty;
            AlignPolicy(after);
            UpdateStatusFrom(after);
            NotifyPendingChanges(after);
        }
        catch (Exception ex)
        {
            Logger.Error("手动切换异常", ex);
            TryBalloon($"切换失败：{ex.Message}");
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
            $"推断盖子：{StateRules.LidText(snap.Lid)}\n" +
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
            SyncMenuColors();
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
        SyncMenuColors();
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
