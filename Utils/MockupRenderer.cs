using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using AutoDisplayPower.Services;

namespace AutoDisplayPower.Utils;

/// <summary>样张风格定义。</summary>
internal sealed class MockupTheme
{
    public string Name = "";
    public string Description = "";
    public Color PanelBack = Color.White;
    public Color Border = Color.Gainsboro;
    public Color TextPrimary = Color.Black;
    public Color TextSecondary = Color.Gray;
    public Color Accent = Color.DodgerBlue;
    public Color? PillFill;          // 当前模式：整行填充色（null = 用左侧色条）
    public Color? LeftBar;           // 当前模式：左侧色条
    public Color SwitchOff = Color.Gainsboro;
}

/// <summary>菜单样张渲染器（纯绘制，用于在动手前确认风格与配色）。</summary>
internal static class MockupRenderer
{
    private const int Scale = 2;
    private const int RowHeight = 27;
    private const int SepHeight = 9;
    private const int PanelWidth = 276;
    private const int PadX = 6;
    private const int PadY = 7;
    private const int IconCol = 24;

    private enum RowKind { Header, Text, Sep, Sub, SwitchRow }

    private sealed class Row
    {
        public string Text = "";
        public Image? Icon;
        public RowKind Kind = RowKind.Text;
        public bool Active;
        public bool SwitchOn;
    }

    public static IReadOnlyList<string> RenderAll(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        MenuIconFactory.Configure(32); // 2 倍样张：直接用 32px 图标，边缘更利落

        var themes = new List<MockupTheme>
        {
            new()
            {
                Name = "方案 A　浅色 · 蓝色强调（当前实现）",
                Description = "Fluent 浅色：近黑文字 + 彩色图标；当前模式 = 整行淡蓝药丸；开关=蓝色",
                PanelBack = Color.FromArgb(0xFB, 0xFB, 0xFB),
                Border = Color.FromArgb(0xE1, 0xE1, 0xE1),
                TextPrimary = Color.FromArgb(0x1B, 0x1B, 0x1B),
                TextSecondary = Color.FromArgb(0x61, 0x61, 0x61),
                Accent = Color.FromArgb(0x00, 0x78, 0xD4),
                PillFill = Color.FromArgb(0xE9, 0xF2, 0xFC),
                SwitchOff = Color.FromArgb(0xC8, 0xC6, 0xC4),
            },
            new()
            {
                Name = "方案 B　纯白 · 橙色强调（类火绒）",
                Description = "纯白底 + 橙色强调色（药丸/开关/当前模式），图标保留各自语义色",
                PanelBack = Color.White,
                Border = Color.FromArgb(0xE6, 0xE6, 0xE6),
                TextPrimary = Color.FromArgb(0x20, 0x20, 0x20),
                TextSecondary = Color.FromArgb(0x77, 0x77, 0x77),
                Accent = Color.FromArgb(0xE8, 0x77, 0x22),
                PillFill = Color.FromArgb(0xFD, 0xF0, 0xE2),
                SwitchOff = Color.FromArgb(0xCF, 0xCF, 0xCF),
            },
            new()
            {
                Name = "方案 C　深色主题",
                Description = "深灰底 + 亮字 + 蓝色强调；夜间/暗色桌面更协调",
                PanelBack = Color.FromArgb(0x2B, 0x2B, 0x2B),
                Border = Color.FromArgb(0x45, 0x45, 0x45),
                TextPrimary = Color.FromArgb(0xF3, 0xF3, 0xF3),
                TextSecondary = Color.FromArgb(0xB0, 0xB0, 0xB0),
                Accent = Color.FromArgb(0x60, 0x9C, 0xE0),
                PillFill = Color.FromArgb(0x3A, 0x47, 0x57),
                SwitchOff = Color.FromArgb(0x66, 0x66, 0x66),
            },
            new()
            {
                Name = "方案 D　极简 · 左侧色条",
                Description = "纯白、不用色块；当前模式用左侧一条细色条 + 加粗表示，最克制",
                PanelBack = Color.White,
                Border = Color.FromArgb(0xEA, 0xEA, 0xEA),
                TextPrimary = Color.FromArgb(0x1B, 0x1B, 0x1B),
                TextSecondary = Color.FromArgb(0x70, 0x70, 0x70),
                Accent = Color.FromArgb(0x00, 0x78, 0xD4),
                PillFill = null,
                LeftBar = Color.FromArgb(0x00, 0x78, 0xD4),
                SwitchOff = Color.FromArgb(0xCE, 0xCE, 0xCE),
            },
        };

        var files = new List<string>();
        for (int i = 0; i < themes.Count; i++)
        {
            string path = Path.Combine(outputDir, $"mockup-{(char)('A' + i)}.png");
            RenderOne(path, themes[i]);
            files.Add(path);
        }

        return files;
    }

    private static void RenderOne(string path, MockupTheme t)
    {
        List<Row> rows = BuildRows(t);

        int contentH = PadY * 2;
        foreach (Row r in rows) contentH += r.Kind == RowKind.Sep ? SepHeight : RowHeight;

        int titleH = 46;
        int tipH = 34;
        int panelW = PanelWidth;
        int canvasW = (panelW + 40) * Scale;
        int canvasH = (titleH + contentH + 16 + tipH) * Scale;

        using var bmp = new Bitmap(canvasW, canvasH, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.ScaleTransform(Scale, Scale);

            g.Clear(Color.FromArgb(0xF0, 0xF0, 0xF0));

            // 标题 + 说明
            using (var f = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold))
            using (var b = new SolidBrush(Color.FromArgb(0x1B, 0x1B, 0x1B)))
                g.DrawString(t.Name, f, b, 20, 8);
            using (var f2 = new Font("Microsoft YaHei UI", 8.5f))
            using (var b2 = new SolidBrush(Color.FromArgb(0x60, 0x60, 0x60)))
                g.DrawString(t.Description, f2, b2, 20, 28);

            var panel = new Rectangle(20, titleH, panelW, contentH);
            DrawPanel(g, panel, t);
            DrawRows(g, panel, rows, t);
            DrawTooltipSample(g, 20, titleH + contentH + 14, t);
        }

        bmp.Save(path, ImageFormat.Png);
    }

    private static List<Row> BuildRows(MockupTheme t) => new()
    {
        new Row { Text = "状态", Kind = RowKind.Header },
        new Row { Text = "当前屏幕：外接屏 (KG257S PLUS)", Icon = MenuIconFactory.ScreenIcon(ScreenState.ExternalOnly) },
        new Row { Text = "盖子状态：打开", Icon = MenuIconFactory.LidIcon(LidState.Open) },
        new Row { Text = "电源策略：合盖不操作（已下发）", Icon = MenuIconFactory.PolicyIcon(0, false) },
        new Row { Kind = RowKind.Sep },
        new Row { Text = "仅外接", Icon = MenuIconFactory.ModeExternal(), Active = true },
        new Row { Text = "仅笔记本", Icon = MenuIconFactory.ModeInternal() },
        new Row { Text = "扩展", Icon = MenuIconFactory.ModeExtend() },
        new Row { Kind = RowKind.Sep },
        new Row { Text = "插上外接屏时", Icon = MenuIconFactory.PlugArrow(), Kind = RowKind.Sub },
        new Row { Kind = RowKind.Sep },
        new Row { Text = "开机自启动", Icon = MenuIconFactory.Power(), Kind = RowKind.SwitchRow, SwitchOn = true },
        new Row { Text = "显示器型号配置…", Icon = MenuIconFactory.Settings() },
        new Row { Kind = RowKind.Sep },
        new Row { Text = "退出", Icon = MenuIconFactory.Blank() },
    };

    private static void DrawPanel(Graphics g, Rectangle panel, MockupTheme t)
    {
        using var path = Rounded(panel, 8f);
        using var back = new SolidBrush(t.PanelBack);
        g.FillPath(back, path);
        using var pen = new Pen(t.Border, 1f);
        g.DrawPath(pen, path);
    }

    private static void DrawRows(Graphics g, Rectangle panel, List<Row> rows, MockupTheme t)
    {
        using var font = new Font("Microsoft YaHei UI", 9f);
        using var boldFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        using var sepPen = new Pen(t.Border, 1f);
        using var textBrush = new SolidBrush(t.TextPrimary);
        using var subBrush = new SolidBrush(t.TextSecondary);

        int y = panel.Top + PadY;
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            int h = row.Kind == RowKind.Sep ? SepHeight : RowHeight;

            if (row.Kind == RowKind.Sep)
            {
                g.DrawLine(sepPen, panel.Left + 6, y + h / 2f, panel.Right - 6, y + h / 2f);
                y += h;
                continue;
            }

            var bounds = new Rectangle(panel.Left + PadX, y, panel.Width - PadX * 2, h);

            // 当前模式的高亮
            if (row.Active)
            {
                if (t.PillFill is { } fill)
                {
                    using var pill = Rounded(new Rectangle(bounds.Left, bounds.Top + 1, bounds.Width, bounds.Height - 2), 6f);
                    using var pb = new SolidBrush(fill);
                    g.FillPath(pb, pill);
                }
                else if (t.LeftBar is { } bar)
                {
                    using var barPath = Rounded(new Rectangle(bounds.Left + 2, bounds.Top + 5, 3, bounds.Height - 10), 1.5f);
                    using var bb = new SolidBrush(bar);
                    g.FillPath(bb, barPath);
                }
            }

            // 图标
            float iconX = bounds.Left + 8;
            if (row.Icon is not null)
            {
                int size = IconCol - 8;
                g.DrawImage(row.Icon, new RectangleF(iconX, bounds.Top + (bounds.Height - size) / 2f, size, size));
            }

            // 文字
            float textX = iconX + IconCol;
            float reserved = row.Kind switch
            {
                RowKind.SwitchRow => 50f,
                RowKind.Sub => 22f,
                _ => 10f,
            };
            Font useFont = row.Kind == RowKind.Header || row.Active ? boldFont : font;
            using var sf = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(row.Text, useFont, textBrush,
                new RectangleF(textX, bounds.Top, Math.Max(20f, bounds.Right - textX - reserved), bounds.Height), sf);

            // 子菜单箭头
            if (row.Kind == RowKind.Sub)
            {
                using var arrow = new SolidBrush(t.TextSecondary);
                float ax = bounds.Right - 12, ay = bounds.Top + bounds.Height / 2f;
                g.FillPolygon(arrow, new[]
                {
                    new PointF(ax, ay - 4), new PointF(ax + 5, ay), new PointF(ax, ay + 4),
                });
            }

            // 滑动开关
            if (row.Kind == RowKind.SwitchRow)
            {
                int sw = 34, sh = 18;
                var track = new Rectangle(bounds.Right - sw - 6, bounds.Top + (bounds.Height - sh) / 2, sw, sh);
                using (var tp = Rounded(track, sh / 2f))
                using (var tb = new SolidBrush(row.SwitchOn ? t.Accent : t.SwitchOff))
                    g.FillPath(tb, tp);
                int kd = sh - 4;
                int kx = row.SwitchOn ? track.Right - kd - 2 : track.Left + 2;
                using var kb = new SolidBrush(Color.White);
                g.FillEllipse(kb, kx, track.Top + 2, kd, kd);
            }

            y += h;
        }
    }

    private static void DrawTooltipSample(Graphics g, int x, int y, MockupTheme t)
    {
        const string tip = "托盘悬停提示：仅外接 · 盖子打开 · 合盖不操作";
        using var font = new Font("Microsoft YaHei UI", 8.5f);
        SizeF size = g.MeasureString(tip, font);
        var box = new Rectangle(x, y, (int)size.Width + 20, (int)size.Height + 12);
        using (var path = Rounded(box, 6f))
        using (var b = new SolidBrush(Color.FromArgb(0xFA, 0xFA, 0xFA)))
            g.FillPath(b, path);
        using (var pen = new Pen(Color.FromArgb(0xD8, 0xD8, 0xD8)))
        using (var path = Rounded(box, 6f))
            g.DrawPath(pen, path);

        // 托盘图标（当前状态：仅外接=蓝）；注意：该图标由工厂缓存，不能 Dispose
        Icon tray = TrayIconFactory.GetStateIcon(ScreenState.ExternalOnly);
        using (var trayBmp = tray.ToBitmap())
        {
            g.DrawImage(trayBmp, new RectangleF(x + 6, y + 6, 16, 16));
        }

        using var tb = new SolidBrush(t.TextPrimary);
        g.DrawString(tip, font, tb, x + 26, y + 7);
    }

    private static GraphicsPath Rounded(Rectangle r, float radius)
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
}
