using System.Drawing;
using AutoDisplayPower.Services;

namespace AutoDisplayPower.Utils;

/// <summary>
/// 界面配色（参照 Microsoft Fluent Design 的浅色语义色，低饱和、优雅）。
/// </summary>
internal static class UiTheme
{
    // ---- 基础文字 / 背景 ----
    public static readonly Color TextPrimary = Color.FromArgb(0x1B, 0x1B, 0x1B);   // Fluent Text primary
    public static readonly Color TextSecondary = Color.FromArgb(0x61, 0x61, 0x61); // Fluent Text secondary
    public static readonly Color MenuBack = Color.FromArgb(0xFB, 0xFB, 0xFB);      // 菜单背景
    public static readonly Color MenuBorder = Color.FromArgb(0xE1, 0xE1, 0xE1);    // 菜单边框/分隔线
    public static readonly Color Hover = Color.FromArgb(0xEE, 0xF4, 0xFB);         // 悬停高亮（淡蓝）
    public static readonly Color HoverBorder = Color.FromArgb(0xCF, 0xE3, 0xF7);

    // ---- Fluent 语义色 ----
    public static readonly Color Accent = Color.FromArgb(0x00, 0x78, 0xD4); // 蓝（communication blue）
    public static readonly Color Teal = Color.FromArgb(0x03, 0x83, 0x87);   // 青
    public static readonly Color Purple = Color.FromArgb(0x87, 0x64, 0xB8); // 紫
    public static readonly Color Green = Color.FromArgb(0x0F, 0x7B, 0x0F);  // 绿
    public static readonly Color Amber = Color.FromArgb(0xC7, 0x77, 0x00);  // 琥珀
    public static readonly Color Red = Color.FromArgb(0xC4, 0x2B, 0x1C);    // 红（错误）
    public static readonly Color Gray = Color.FromArgb(0x8A, 0x88, 0x86);   // 灰（未知）

    /// <summary>屏幕状态 → 颜色（仅外接=蓝 / 仅笔记本=青 / 扩展=紫 / 未知=灰）。</summary>
    public static Color ScreenStateColor(ScreenState state) => state switch
    {
        ScreenState.ExternalOnly => Accent,
        ScreenState.InternalOnly => Teal,
        ScreenState.Extended => Purple,
        _ => Gray,
    };

    /// <summary>盖子状态 → 颜色（打开=绿 / 闭合=琥珀 / 未知=灰）。</summary>
    public static Color LidColor(LidState lid) => lid switch
    {
        LidState.Open => Green,
        LidState.Closed => Amber,
        _ => Gray,
    };

    /// <summary>合盖策略值 → 颜色（不操作=蓝 / 睡眠=紫 / 未知=灰）。</summary>
    public static Color PolicyColor(int? value) => value switch
    {
        0 => Accent,
        1 => Purple,
        2 => Purple,
        3 => Amber,
        _ => Gray,
    };

    /// <summary>把颜色调亮（用于图标渐变的高光端）。</summary>
    public static Color Lighten(Color c, double amount)
        => Color.FromArgb(c.A,
            (int)System.Math.Min(255, c.R + (255 - c.R) * amount),
            (int)System.Math.Min(255, c.G + (255 - c.G) * amount),
            (int)System.Math.Min(255, c.B + (255 - c.B) * amount));

    /// <summary>把颜色调暗。</summary>
    public static Color Darken(Color c, double amount)
        => Color.FromArgb(c.A,
            (int)(c.R * (1 - amount)),
            (int)(c.G * (1 - amount)),
            (int)(c.B * (1 - amount)));
}
