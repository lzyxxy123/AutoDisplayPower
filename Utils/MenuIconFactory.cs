using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using AutoDisplayPower.Services;
namespace AutoDisplayPower.Utils;

/// <summary>
/// 菜单彩色图标工厂：全部用代码绘制的矢量图标（设计空间 16×16，3 倍超采样后缩放，边缘平滑）。
/// 与托盘图标同一套设计语言：扁平、圆角、语义色。无需任何外部资源。
/// </summary>
internal static class MenuIconFactory
{
    private const int Design = 16;
    private static int _size = 16;
    private static readonly Dictionary<string, Image> Cache = new();

    /// <summary>按当前 DPI 配置图标像素尺寸（默认 16）。</summary>
    public static void Configure(int size)
    {
        if (size <= 0) size = 16;
        if (size == _size && Cache.Count > 0) return;
        _size = size;
        Cache.Clear();
    }

    /// <summary>清空缓存（切换主题后调用，使图标按新配色重新绘制）。</summary>
    public static void ClearCache() => Cache.Clear();

    // ---------------- 对外图标 ----------------

    public static Image ModeExternal() => Get("mode-ext", g => DrawMonitor(g, UiTheme.Blue));
    public static Image ModeInternal() => Get("mode-int", g => DrawLaptop(g, UiTheme.Teal));
    public static Image ModeExtend() => Get("mode-2", g => DrawDualMonitor(g, UiTheme.Purple));

    public static Image ScreenIcon(ScreenState state) => state switch
    {
        ScreenState.ExternalOnly => ModeExternal(),
        ScreenState.InternalOnly => ModeInternal(),
        ScreenState.Extended => ModeExtend(),
        _ => Get("screen-unk", g => DrawMonitor(g, UiTheme.Gray)),
    };

    public static Image LidIcon(LidState lid) => lid switch
    {
        LidState.Open => Get("lid-open", g => DrawLid(g, true, UiTheme.Green)),
        LidState.Closed => Get("lid-closed", g => DrawLid(g, false, UiTheme.Amber)),
        _ => Get("lid-unk", g => DrawLid(g, true, UiTheme.Gray)),
    };

    /// <summary>0=不操作 1/2=睡眠/休眠 3=关机；null=未知；failed=true 表示写入失败。</summary>
    public static Image PolicyIcon(int? value, bool failed)
    {
        if (failed) return Get("pol-fail", g => DrawWarnTriangle(g, UiTheme.Red));
        return value switch
        {
            0 => Get("pol-none", g => DrawCircleGlyph(g, UiTheme.Blue, DrawPauseBars)),
            1 => Get("pol-sleep", g => DrawMoon(g, UiTheme.Purple)),
            2 => Get("pol-sleep", g => DrawMoon(g, UiTheme.Purple)),
            3 => Get("pol-off", g => DrawPowerSymbol(g, UiTheme.Amber)),
            _ => Get("pol-unk", g => DrawCircleGlyph(g, UiTheme.Gray, gg => DrawGlyphText(gg, "?", Brushes.White))),
        };
    }

    public static Image Power() => Get("power", g => DrawPowerSymbol(g, UiTheme.Accent));
    public static Image Settings() => Get("settings", g => DrawSliders(g, UiTheme.Gray));
    public static Image PlugArrow() => Get("plug", g => DrawPlug(g, UiTheme.Accent));
    public static Image History() => Get("hist", g => DrawClock(g, UiTheme.Accent));
    public static Image Palette() => Get("palette", g => DrawPalette(g, UiTheme.Accent));
    public static Image Blank() => Get("blank", _ => { });

    // ---------------- 行图标：[对钩列][图标] 组合图 ----------------
    // 把“选中对钩”直接合成进图片，避免依赖菜单渲染器的勾选区（更可靠、且勾与图标不会互相抢位）

    /// <summary>模式行图标（active=true 时在左侧画出对钩）。</summary>
    public static Image ModeRow(DisplaySwitcher.Mode mode, bool active)
        => Row($"row-mode-{mode}-{active}", ModeIcon(mode), active);

    public static Image ScreenRow(ScreenState state)
        => Row($"row-screen-{state}", ScreenIcon(state), false);

    public static Image LidRow(LidState lid)
        => Row($"row-lid-{lid}", LidIcon(lid), false);

    public static Image PolicyRow(int? value, bool failed)
        => Row($"row-pol-{value}-{failed}", PolicyIcon(value, failed), false);

    /// <summary>无图标的行（占位，保证缩进一致）。</summary>
    public static Image EmptyRow() => Row("row-empty", Blank(), false);

    /// <summary>子菜单行（自带图标，无对钩列）。</summary>
    public static Image SubRow(string key, Image icon) => Row($"row-sub-{key}", icon, false);

    /// <summary>主题色块（用于“界面主题”子菜单）。</summary>
    public static Image Swatch(string key, Color color)
    {
        string cacheKey = $"swatch-{key}@{_size}";
        if (Cache.TryGetValue(cacheKey, out Image? cached)) return cached;

        var bmp = new Bitmap(_size, _size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float pad = _size * 0.16f;
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, pad, pad, _size - pad * 2, _size - pad * 2);
        }

        Cache[cacheKey] = bmp;
        return bmp;
    }

    public static Image ModeIcon(DisplaySwitcher.Mode mode) => mode switch
    {
        DisplaySwitcher.Mode.External => ModeExternal(),
        DisplaySwitcher.Mode.Internal => ModeInternal(),
        _ => ModeExtend(),
    };

    /// <summary>组合出 [对钩列][图标] 的行图标（check=true 时左侧画对钩）。</summary>
    public static Image Row(string key, Image icon, bool check)
    {
        string cacheKey = key + "@" + _size;
        if (Cache.TryGetValue(cacheKey, out Image? cached)) return cached;

        int h = _size;
        int w = _size * 2; // 左列对钩 + 右列图标
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (check) DrawCheck(g, new RectangleF(1f, h / 2f - h * 0.22f, h * 0.46f, h * 0.44f));
            g.DrawImage(icon, new RectangleF(_size, 0, _size, _size));
        }

        Cache[cacheKey] = bmp;
        return bmp;
    }

    /// <summary>矢量对钩（颜色用主题强调色）。</summary>
    private static void DrawCheck(Graphics g, RectangleF r)
    {
        using var pen = new Pen(UiTheme.Accent, Math.Max(1.6f, _size * 0.14f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        g.DrawLines(pen, new[]
        {
            new PointF(r.Left, r.Top + r.Height * 0.5f),
            new PointF(r.Left + r.Width * 0.38f, r.Bottom),
            new PointF(r.Right, r.Top),
        });
    }

    // ---------------- 绘制实现（16×16 设计空间）----------------

    private static void DrawMonitor(Graphics g, Color color)
    {
        using (var screen = Rounded(new RectangleF(1.2f, 2.0f, 13.6f, 9.2f), 1.6f))
        using (var brush = Gradient(new RectangleF(1.2f, 2.0f, 13.6f, 9.2f), color))
        {
            g.FillPath(brush, screen);
        }

        using var dark = new SolidBrush(UiTheme.Darken(color, 0.18));
        g.FillRectangle(dark, 7.0f, 11.2f, 2.0f, 1.8f);
        using var stand = Rounded(new RectangleF(4.4f, 12.9f, 7.2f, 1.6f), 0.8f);
        g.FillPath(dark, stand);
    }

    private static void DrawLaptop(Graphics g, Color color)
    {
        using (var screen = Rounded(new RectangleF(2.6f, 2.0f, 10.8f, 8.0f), 1.4f))
        using (var brush = Gradient(new RectangleF(2.6f, 2.0f, 10.8f, 8.0f), color))
        {
            g.FillPath(brush, screen);
        }

        using var dark = new SolidBrush(UiTheme.Darken(color, 0.18));
        using var bas = Rounded(new RectangleF(0.9f, 11.0f, 14.2f, 2.6f), 1.1f);
        g.FillPath(dark, bas);
    }

    private static void DrawDualMonitor(Graphics g, Color color)
    {
        // 后面一台（略浅，但保持可辨识）
        using (var back = Rounded(new RectangleF(0.6f, 1.2f, 8.8f, 6.4f), 1.2f))
        using (var brush = new SolidBrush(UiTheme.Lighten(color, 0.34)))
        {
            g.FillPath(brush, back);
        }

        // 后面一台的支架
        using (var backDark = new SolidBrush(UiTheme.Lighten(color, 0.2)))
        {
            g.FillRectangle(backDark, 4.2f, 7.6f, 1.6f, 1.4f);
        }

        // 前面一台（主色）
        using (var front = Rounded(new RectangleF(5.4f, 5.8f, 9.8f, 7.0f), 1.3f))
        using (var brush = Gradient(new RectangleF(5.4f, 5.8f, 9.8f, 7.0f), color))
        {
            g.FillPath(brush, front);
        }

        using var dark = new SolidBrush(UiTheme.Darken(color, 0.22));
        g.FillRectangle(dark, 9.5f, 12.8f, 1.8f, 1.4f);
        using var bas = Rounded(new RectangleF(7.8f, 14.0f, 5.2f, 1.3f), 0.6f);
        g.FillPath(dark, bas);
    }

    private static void DrawLid(Graphics g, bool open, Color color)
    {
        if (open)
        {
            // 开盖：屏幕在上、通栏底座在下，中间留空隙（明显区别于“显示器+支架”）
            using (var screen = Rounded(new RectangleF(3.4f, 1.4f, 9.2f, 7.6f), 1.3f))
            using (var brush = Gradient(new RectangleF(3.4f, 1.4f, 9.2f, 7.6f), color))
            {
                g.FillPath(brush, screen);
            }

            using var dark = new SolidBrush(UiTheme.Darken(color, 0.18));
            using var bas = Rounded(new RectangleF(1.2f, 10.6f, 13.6f, 2.6f), 1.1f);
            g.FillPath(dark, bas);
        }
        else
        {
            // 闭合：上下两片贴合（无空隙）
            using (var top = Rounded(new RectangleF(3.4f, 5.4f, 9.2f, 2.4f), 1.0f))
            using (var brush = Gradient(new RectangleF(3.4f, 5.4f, 9.2f, 2.4f), color))
            {
                g.FillPath(brush, top);
            }

            using var dark = new SolidBrush(UiTheme.Darken(color, 0.18));
            using var bas = Rounded(new RectangleF(1.2f, 8.2f, 13.6f, 2.8f), 1.2f);
            g.FillPath(dark, bas);
        }
    }

    private static void DrawMoon(Graphics g, Color color)
    {
        using var path = new GraphicsPath(FillMode.Alternate);
        path.AddEllipse(2.4f, 2.4f, 11.2f, 11.2f);
        path.AddEllipse(6.2f, 0.6f, 11.2f, 11.2f);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    private static void DrawWarnTriangle(Graphics g, Color color)
    {
        using (var brush = new SolidBrush(color))
        {
            g.FillPolygon(brush, new[]
            {
                new PointF(8f, 1.4f),
                new PointF(15.1f, 14.2f),
                new PointF(0.9f, 14.2f),
            });
        }

        using var white = new SolidBrush(Color.White);
        g.FillRectangle(white, 7.25f, 6.0f, 1.5f, 4.4f);
        g.FillRectangle(white, 7.25f, 11.4f, 1.5f, 1.6f);
    }

    private static void DrawCircleGlyph(Graphics g, Color color, Action<Graphics> glyph)
    {
        using (var brush = new SolidBrush(color))
        {
            g.FillEllipse(brush, 1.4f, 1.4f, 13.2f, 13.2f);
        }

        glyph(g);
    }

    private static void DrawPauseBars(Graphics g)
    {
        using var white = new SolidBrush(Color.White);
        using var b1 = Rounded(new RectangleF(5.4f, 4.6f, 2.0f, 6.8f), 0.8f);
        using var b2 = Rounded(new RectangleF(8.6f, 4.6f, 2.0f, 6.8f), 0.8f);
        g.FillPath(white, b1);
        g.FillPath(white, b2);
    }

    private static void DrawGlyphText(Graphics g, string text, Brush brush)
    {
        using var font = new Font("Segoe UI", 10.5f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        g.DrawString(text, font, brush, new RectangleF(1.4f, 1.8f, 13.2f, 13.2f), sf);
    }

    private static void DrawPowerSymbol(Graphics g, Color color)
    {
        using var pen = new Pen(color, 1.9f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(pen, 2.6f, 3.4f, 10.8f, 10.8f, -62f, 304f);
        g.DrawLine(pen, 8f, 1.6f, 8f, 7.6f);
    }

    private static void DrawSliders(Graphics g, Color color)
    {
        using var pen = new Pen(color, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var knob = new SolidBrush(color);
        float[] knobX = { 5.6f, 10.4f, 7.4f };
        for (int i = 0; i < 3; i++)
        {
            float y = 3.6f + i * 4.4f;
            g.DrawLine(pen, 1.6f, y, 14.4f, y);
            g.FillEllipse(knob, knobX[i] - 1.7f, y - 1.7f, 3.4f, 3.4f);
        }
    }

    private static void DrawPlug(Graphics g, Color color)
    {
        // 显示器（右侧）
        using (var screen = Rounded(new RectangleF(6.8f, 2.6f, 8.4f, 8.0f), 1.2f))
        using (var brush = new SolidBrush(color))
        {
            g.FillPath(brush, screen);
        }

        using (var dark = new SolidBrush(UiTheme.Darken(color, 0.2)))
        {
            g.FillRectangle(dark, 9.9f, 10.6f, 2.2f, 1.5f);
            using var bas = Rounded(new RectangleF(7.6f, 12.1f, 6.8f, 1.4f), 0.7f);
            g.FillPath(dark, bas);
        }

        // 箭头（从左侧插入显示器）
        using var pen = new Pen(color, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLine(pen, 1.2f, 6.6f, 6.2f, 6.6f);
        g.DrawLine(pen, 3.7f, 4.2f, 6.2f, 6.6f);
        g.DrawLine(pen, 3.7f, 9.0f, 6.2f, 6.6f);
    }

    private static void DrawClock(Graphics g, Color color)
    {
        using var pen = new Pen(color, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawEllipse(pen, 1.8f, 1.8f, 12.4f, 12.4f);
        g.DrawLine(pen, 8f, 4.6f, 8f, 8.4f);
        g.DrawLine(pen, 8f, 8.4f, 11.0f, 10.0f);
    }

    /// <summary>调色板：圆环 + 三色点（表示可切换主题）。</summary>
    private static void DrawPalette(Graphics g, Color color)
    {
        using (var pen = new Pen(color, 1.5f))
        {
            g.DrawEllipse(pen, 1.4f, 1.4f, 13.2f, 13.2f);
        }

        using (var b = new SolidBrush(UiTheme.Blue)) g.FillEllipse(b, 3.8f, 3.8f, 3.2f, 3.2f);
        using (var b = new SolidBrush(UiTheme.Green)) g.FillEllipse(b, 9.0f, 3.8f, 3.2f, 3.2f);
        using (var b = new SolidBrush(UiTheme.Purple)) g.FillEllipse(b, 6.4f, 9.0f, 3.2f, 3.2f);
    }

    // ---------------- 通用工具 ----------------

    private static LinearGradientBrush Gradient(RectangleF r, Color color)
    {
        var area = new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2);
        return new LinearGradientBrush(area, UiTheme.Lighten(color, 0.42), color, 60f);
    }

    private static GraphicsPath Rounded(RectangleF r, float radius)
    {
        float d = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180f, 90f);
        path.AddArc(r.Right - d, r.Top, d, d, 270f, 90f);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    private static Image Get(string key, Action<Graphics> draw)
    {
        string cacheKey = key + "@" + _size;
        if (Cache.TryGetValue(cacheKey, out Image? cached)) return cached;

        Image image = Render(_size, draw);
        Cache[cacheKey] = image;
        return image;
    }

    private static Bitmap Render(int size, Action<Graphics> draw)
    {
        const int ss = 3; // 超采样倍数
        float scale = size / (float)Design;

        using var big = new Bitmap(size * ss, size * ss, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(big))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.ScaleTransform(scale * ss, scale * ss);
            draw(g);
        }

        var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g2 = Graphics.FromImage(result))
        {
            g2.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g2.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g2.DrawImage(big, new Rectangle(0, 0, size, size));
        }

        return result;
    }
}
