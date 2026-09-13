using System.Collections.Generic;
using System.Drawing;

namespace AutoDisplayPower.Utils;

/// <summary>一套界面主题（配色 + 强调方式）。</summary>
internal sealed class Theme
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public bool Dark { get; init; }

    // ---- 菜单外观 ----
    public Color PanelBack { get; init; }
    public Color Border { get; init; }
    public Color TextPrimary { get; init; }
    public Color TextSecondary { get; init; }
    public Color Accent { get; init; }
    public Color Hover { get; init; }
    public Color HoverBorder { get; init; }
    public Color PillFill { get; init; }
    public Color PillStrong { get; init; }
    public Color SwitchOff { get; init; }

    /// <summary>当前模式用“左侧色条”而非整行填充（极简风格）。</summary>
    public bool UseLeftBar { get; init; }

    // ---- 语义色（图标用；深色主题下会更亮以保持对比）----
    public Color Blue { get; init; }
    public Color Teal { get; init; }
    public Color Purple { get; init; }
    public Color Green { get; init; }
    public Color Amber { get; init; }
    public Color Red { get; init; }
    public Color Gray { get; init; }
}

/// <summary>内置主题目录。</summary>
internal static class ThemeCatalog
{
    public const string DefaultKey = "C";

    private static readonly List<Theme> Items = new()
    {
        new Theme
        {
            Key = "A",
            Name = "A 浅色 · 蓝色强调",
            Description = "Fluent 浅色：近黑文字 + 彩色图标，当前模式为整行淡蓝高亮",
            PanelBack = Color.FromArgb(0xFB, 0xFB, 0xFB),
            Border = Color.FromArgb(0xE1, 0xE1, 0xE1),
            TextPrimary = Color.FromArgb(0x1B, 0x1B, 0x1B),
            TextSecondary = Color.FromArgb(0x61, 0x61, 0x61),
            Accent = Color.FromArgb(0x00, 0x78, 0xD4),
            Hover = Color.FromArgb(0xEE, 0xF4, 0xFB),
            HoverBorder = Color.FromArgb(0xCF, 0xE3, 0xF7),
            PillFill = Color.FromArgb(0xE9, 0xF2, 0xFC),
            PillStrong = Color.FromArgb(0xDC, 0xEC, 0xFA),
            SwitchOff = Color.FromArgb(0xC8, 0xC6, 0xC4),
            Blue = Color.FromArgb(0x00, 0x78, 0xD4),
            Teal = Color.FromArgb(0x03, 0x83, 0x87),
            Purple = Color.FromArgb(0x87, 0x64, 0xB8),
            Green = Color.FromArgb(0x0F, 0x7B, 0x0F),
            Amber = Color.FromArgb(0xC7, 0x77, 0x00),
            Red = Color.FromArgb(0xC4, 0x2B, 0x1C),
            Gray = Color.FromArgb(0x8A, 0x88, 0x86),
        },
        new Theme
        {
            Key = "B",
            Name = "B 纯白 · 橙色强调",
            Description = "纯白底 + 橙色强调色（高亮/开关），图标保留各自语义色",
            PanelBack = Color.White,
            Border = Color.FromArgb(0xE6, 0xE6, 0xE6),
            TextPrimary = Color.FromArgb(0x20, 0x20, 0x20),
            TextSecondary = Color.FromArgb(0x77, 0x77, 0x77),
            Accent = Color.FromArgb(0xE8, 0x77, 0x22),
            Hover = Color.FromArgb(0xFD, 0xF3, 0xE9),
            HoverBorder = Color.FromArgb(0xF6, 0xD9, 0xBC),
            PillFill = Color.FromArgb(0xFD, 0xF0, 0xE2),
            PillStrong = Color.FromArgb(0xFB, 0xE4, 0xCD),
            SwitchOff = Color.FromArgb(0xCF, 0xCF, 0xCF),
            Blue = Color.FromArgb(0x0A, 0x6E, 0xC2),
            Teal = Color.FromArgb(0x03, 0x83, 0x87),
            Purple = Color.FromArgb(0x87, 0x64, 0xB8),
            Green = Color.FromArgb(0x0F, 0x7B, 0x0F),
            Amber = Color.FromArgb(0xC7, 0x77, 0x00),
            Red = Color.FromArgb(0xC4, 0x2B, 0x1C),
            Gray = Color.FromArgb(0x8A, 0x88, 0x86),
        },
        new Theme
        {
            Key = "C",
            Name = "C 深色主题",
            Description = "深灰底 + 亮字 + 蓝色强调；夜间/暗色桌面更协调",
            Dark = true,
            PanelBack = Color.FromArgb(0x2B, 0x2B, 0x2B),
            Border = Color.FromArgb(0x45, 0x45, 0x45),
            TextPrimary = Color.FromArgb(0xF3, 0xF3, 0xF3),
            TextSecondary = Color.FromArgb(0xB4, 0xB4, 0xB4),
            Accent = Color.FromArgb(0x60, 0xA5, 0xFA),
            Hover = Color.FromArgb(0x3A, 0x3A, 0x3A),
            HoverBorder = Color.FromArgb(0x4E, 0x4E, 0x4E),
            PillFill = Color.FromArgb(0x3A, 0x47, 0x5A),
            PillStrong = Color.FromArgb(0x41, 0x52, 0x6B),
            SwitchOff = Color.FromArgb(0x6A, 0x6A, 0x6A),
            Blue = Color.FromArgb(0x60, 0xA5, 0xFA),
            Teal = Color.FromArgb(0x2D, 0xD4, 0xBF),
            Purple = Color.FromArgb(0xA7, 0x8B, 0xFA),
            Green = Color.FromArgb(0x4A, 0xDE, 0x80),
            Amber = Color.FromArgb(0xFB, 0xBF, 0x24),
            Red = Color.FromArgb(0xF8, 0x71, 0x71),
            Gray = Color.FromArgb(0x9C, 0xA3, 0xAF),
        },
        new Theme
        {
            Key = "D",
            Name = "D 极简 · 左侧色条",
            Description = "纯白、不用色块；当前模式只用左侧细色条 + 加粗",
            PanelBack = Color.White,
            Border = Color.FromArgb(0xEA, 0xEA, 0xEA),
            TextPrimary = Color.FromArgb(0x1B, 0x1B, 0x1B),
            TextSecondary = Color.FromArgb(0x70, 0x70, 0x70),
            Accent = Color.FromArgb(0x00, 0x78, 0xD4),
            Hover = Color.FromArgb(0xF4, 0xF4, 0xF4),
            HoverBorder = Color.FromArgb(0xDC, 0xDC, 0xDC),
            PillFill = Color.Transparent,
            PillStrong = Color.Transparent,
            SwitchOff = Color.FromArgb(0xCE, 0xCE, 0xCE),
            UseLeftBar = true,
            Blue = Color.FromArgb(0x00, 0x78, 0xD4),
            Teal = Color.FromArgb(0x03, 0x83, 0x87),
            Purple = Color.FromArgb(0x87, 0x64, 0xB8),
            Green = Color.FromArgb(0x0F, 0x7B, 0x0F),
            Amber = Color.FromArgb(0xC7, 0x77, 0x00),
            Red = Color.FromArgb(0xC4, 0x2B, 0x1C),
            Gray = Color.FromArgb(0x8A, 0x88, 0x86),
        },
    };

    public static IReadOnlyList<Theme> All => Items;

    public static Theme Get(string? key)
    {
        foreach (Theme t in Items)
        {
            if (string.Equals(t.Key, key, System.StringComparison.OrdinalIgnoreCase)) return t;
        }

        return Items.Find(t => t.Key == DefaultKey) ?? Items[0];
    }
}
