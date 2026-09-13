using System;
using System.Drawing;
using AutoDisplayPower.Services;

namespace AutoDisplayPower.Utils;

/// <summary>
/// 界面配色入口：转发到"当前主题"（见 <see cref="ThemeCatalog"/>）。
/// 主题可在托盘菜单切换并持久化，切换后需清空图标缓存并重建渲染器。
/// </summary>
internal static class UiTheme
{
    private const string ThemeSettingKey = "UiTheme";

    /// <summary>当前主题。</summary>
    public static Theme Current { get; private set; } =
        ThemeCatalog.Get(AppSettings.ReadString(ThemeSettingKey, ThemeCatalog.DefaultKey));

    /// <summary>主题切换后触发（用于清缓存、重建渲染器、刷新界面）。</summary>
    public static event Action? ThemeChanged;

    public static string CurrentKey => Current.Key;

    /// <summary>切换主题并持久化。</summary>
    public static void SetTheme(string key)
    {
        Theme theme = ThemeCatalog.Get(key);
        if (theme.Key == Current.Key) return;

        Current = theme;
        AppSettings.WriteString(ThemeSettingKey, theme.Key);
        ThemeChanged?.Invoke();
    }

    /// <summary>仅用于预览/样张：临时切换当前主题（不写入注册表、不触发事件）。</summary>
    public static void PreviewTheme(string key) => Current = ThemeCatalog.Get(key);

    // ---- 转发到当前主题（保持既有调用点不变）----
    public static Color TextPrimary => Current.TextPrimary;
    public static Color TextSecondary => Current.TextSecondary;
    public static Color MenuBack => Current.PanelBack;
    public static Color MenuBorder => Current.Border;
    public static Color Hover => Current.Hover;
    public static Color HoverBorder => Current.HoverBorder;
    public static Color Pill => Current.PillFill;
    public static Color PillStrong => Current.PillStrong;
    public static Color SwitchOff => Current.SwitchOff;
    public static Color Accent => Current.Accent;
    public static Color Blue => Current.Blue;
    public static Color Teal => Current.Teal;
    public static Color Purple => Current.Purple;
    public static Color Green => Current.Green;
    public static Color Amber => Current.Amber;
    public static Color Red => Current.Red;
    public static Color Gray => Current.Gray;

    /// <summary>屏幕状态 → 颜色（仅外接=蓝 / 仅笔记本=青 / 扩展=紫 / 未知=灰）。</summary>
    public static Color ScreenStateColor(ScreenState state) => state switch
    {
        ScreenState.ExternalOnly => Current.Blue,
        ScreenState.InternalOnly => Current.Teal,
        ScreenState.Extended => Current.Purple,
        _ => Current.Gray,
    };

    /// <summary>盖子状态 → 颜色（打开=绿 / 闭合=琥珀 / 未知=灰）。</summary>
    public static Color LidColor(LidState lid) => lid switch
    {
        LidState.Open => Current.Green,
        LidState.Closed => Current.Amber,
        _ => Current.Gray,
    };

    /// <summary>合盖策略值 → 颜色（不操作=蓝 / 睡眠=紫 / 未知=灰）。</summary>
    public static Color PolicyColor(int? value) => value switch
    {
        0 => Current.Blue,
        1 => Current.Purple,
        2 => Current.Purple,
        3 => Current.Amber,
        _ => Current.Gray,
    };

    /// <summary>把颜色调亮（用于图标渐变的高光端）。</summary>
    public static Color Lighten(Color c, double amount)
        => Color.FromArgb(c.A,
            (int)Math.Min(255, c.R + (255 - c.R) * amount),
            (int)Math.Min(255, c.G + (255 - c.G) * amount),
            (int)Math.Min(255, c.B + (255 - c.B) * amount));

    /// <summary>把颜色调暗。</summary>
    public static Color Darken(Color c, double amount)
        => Color.FromArgb(c.A,
            (int)(c.R * (1 - amount)),
            (int)(c.G * (1 - amount)),
            (int)(c.B * (1 - amount)));
}
