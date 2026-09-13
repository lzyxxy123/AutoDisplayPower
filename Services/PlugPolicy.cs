using System;
using AutoDisplayPower.Utils;

namespace AutoDisplayPower.Services;

/// <summary>插上外接屏时的自动切换策略。</summary>
public enum PlugBehavior
{
    /// <summary>始终切换为“仅外接”。</summary>
    FixedExternal,

    /// <summary>始终切换为“扩展”。</summary>
    FixedExtend,

    /// <summary>记住上次（外接屏在场时）的选择。</summary>
    RememberLast,
}

/// <summary>“插上外接屏时”的行为设置（HKCU 注册表，持久化）。</summary>
public static class PlugPolicy
{
    private const string BehaviorValue = "PlugBehavior";
    private const string LastModeValue = "LastPlugMode";

    /// <summary>插上外接屏时的行为。</summary>
    public static PlugBehavior Behavior
    {
        get => AppSettings.ReadString(BehaviorValue, nameof(PlugBehavior.FixedExternal)) switch
        {
            nameof(PlugBehavior.FixedExtend) => PlugBehavior.FixedExtend,
            nameof(PlugBehavior.RememberLast) => PlugBehavior.RememberLast,
            _ => PlugBehavior.FixedExternal,
        };
        set => AppSettings.WriteString(BehaviorValue, value.ToString());
    }

    /// <summary>上次（外接屏在场时）选择的模式；只可能是 External 或 Extend。</summary>
    public static DisplaySwitcher.Mode LastMode
    {
        get => AppSettings.ReadString(LastModeValue, nameof(DisplaySwitcher.Mode.External))
                   .Equals(nameof(DisplaySwitcher.Mode.Extend), StringComparison.OrdinalIgnoreCase)
               ? DisplaySwitcher.Mode.Extend
               : DisplaySwitcher.Mode.External;
        set => AppSettings.WriteString(LastModeValue, value.ToString());
    }

    /// <summary>解析“插上外接屏时”应切换到的模式。</summary>
    public static DisplaySwitcher.Mode ResolvePlugMode() => Behavior switch
    {
        PlugBehavior.FixedExtend => DisplaySwitcher.Mode.Extend,
        PlugBehavior.RememberLast => LastMode,
        _ => DisplaySwitcher.Mode.External,
    };

    /// <summary>记录一次用户选择（仅“仅外接/扩展”会被记录）。</summary>
    public static void RememberChoice(DisplaySwitcher.Mode mode)
    {
        if (mode is DisplaySwitcher.Mode.External or DisplaySwitcher.Mode.Extend)
            LastMode = mode;
    }

    /// <summary>供菜单显示的行为描述。</summary>
    public static string DescribeBehavior() => Behavior switch
    {
        PlugBehavior.FixedExtend => "始终「扩展」",
        PlugBehavior.RememberLast => "记住上次选择",
        _ => "始终「仅外接」",
    };
}
