using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using AutoDisplayPower.Services;

namespace AutoDisplayPower.Utils;

/// <summary>
/// 菜单样张渲染器：**直接使用真实主题系统（<see cref="UiTheme"/>）与真实图标工厂**绘制完整菜单，
/// 因此样张与程序实际外观一致，可用于定稿前确认风格。
/// </summary>
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

    /// <summary>为每个内置主题渲染一张样张，返回生成的文件列表。</summary>
    public static IReadOnlyList<string> RenderAll(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        string original = UiTheme.CurrentKey;
        var files = new List<string>();

        try
        {
            foreach (Theme theme in ThemeCatalog.All)
            {
                UiTheme.PreviewTheme(theme.Key);
                MenuIconFactory.ClearCache();
                TrayIconFactory.ClearCache();
                MenuIconFactory.Configure(32); // 2 倍样张：直接用 32px 图标

                string path = Path.Combine(outputDir, $"theme-{theme.Key}.png");
                RenderOne(path, theme);
                files.Add(path);
            }
        }
        finally
        {
            UiTheme.PreviewTheme(original);
            MenuIconFactory.ClearCache();
            TrayIconFactory.ClearCache();
        }

        return files;
    }

    private static void RenderOne(string path, Theme theme)
    {
        List<Row> rows = BuildRows();

        int contentH = PadY * 2;
        foreach (Row r in rows) contentH += r.Kind == RowKind.Sep ? SepHeight : RowHeight;

        int titleH = 46;
        int tipH = 36;
        int canvasW = (PanelWidth + 40) * Scale;
        int canvasH = (titleH + contentH + 16 + tipH) * Scale;

        using var bmp = new Bitmap(canvasW, canvasH, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.ScaleTransform(Scale, Scale);

            // 画布底：与主题面板区分开
            g.Clear(theme.Dark ? Color.FromArgb(0x1E, 0x1E, 0x1E) : Color.FromArgb(0xEC, 0xEC, 0xEC));

            using (var f = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold))
            using (var b = new SolidBrush(theme.Dark ? Color.White : Color.FromArgb(0x1B, 0x1B, 0x1B)))
                g.DrawString(theme.Name, f, b, 20, 8);
            using (var f2 = new Font("Microsoft YaHei UI", 8.5f))
            using (var b2 = new SolidBrush(theme.Dark ? Color.FromArgb(0xC8, 0xC8, 0xC8) : Color.FromArgb(0x60, 0x60, 0x60)))
                g.DrawString(theme.Description, f2, b2, 20, 28);

            var panel = new Rectangle(20, titleH, PanelWidth, contentH);
            DrawPanel(g, panel);
            DrawRows(g, panel, rows);
            DrawTooltipSample(g, 20, titleH + contentH + 14);
        }

        bmp.Save(path, ImageFormat.Png);
    }

    private static List<Row> BuildRows() => new()
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
        new Row { Text = "界面主题", Icon = MenuIconFactory.Palette(), Kind = RowKind.Sub },
        new Row { Kind = RowKind.Sep },
        new Row { Text = "开机自启动", Icon = MenuIconFactory.Power(), Kind = RowKind.SwitchRow, SwitchOn = true },
        new Row { Text = "显示器型号配置…", Icon = MenuIconFactory.Settings() },
        new Row { Kind = RowKind.Sep },
        new Row { Text = "退出", Icon = MenuIconFactory.Blank() },
    };

    private static void DrawPanel(Graphics g, Rectangle panel)
    {
        using var path = Rounded(panel, 8f);
        using var back = new SolidBrush(UiTheme.MenuBack);
        g.FillPath(back, path);
        using var pen = new Pen(UiTheme.MenuBorder, 1f);
        g.DrawPath(pen, path);
    }

    private static void DrawRows(Graphics g, Rectangle panel, List<Row> rows)
    {
        using var font = new Font("Microsoft YaHei UI", 9f);
        using var boldFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        using var sepPen = new Pen(UiTheme.MenuBorder, 1f);
        using var textBrush = new SolidBrush(UiTheme.TextPrimary);
        using var subBrush = new SolidBrush(UiTheme.TextSecondary);

        int y = panel.Top + PadY;
        foreach (Row row in rows)
        {
            int h = row.Kind == RowKind.Sep ? SepHeight : RowHeight;

            if (row.Kind == RowKind.Sep)
            {
                g.DrawLine(sepPen, panel.Left + 6, y + h / 2f, panel.Right - 6, y + h / 2f);
                y += h;
                continue;
            }

            var bounds = new Rectangle(panel.Left + PadX, y, panel.Width - PadX * 2, h);

            if (row.Active)
            {
                if (UiTheme.Current.UseLeftBar)
                {
                    using var barPath = Rounded(new Rectangle(bounds.Left + 4, bounds.Top + 4, 3, bounds.Height - 8), 1.5f);
                    using var bb = new SolidBrush(UiTheme.Accent);
                    g.FillPath(bb, barPath);
                }
                else
                {
                    using var pill = Rounded(new Rectangle(bounds.Left, bounds.Top + 1, bounds.Width, bounds.Height - 2), 6f);
                    using var pb = new SolidBrush(UiTheme.Pill);
                    g.FillPath(pb, pill);
                }
            }

            float iconX = bounds.Left + 8;
            if (row.Icon is not null)
            {
                int size = IconCol - 8;
                g.DrawImage(row.Icon, new RectangleF(iconX, bounds.Top + (bounds.Height - size) / 2f, size, size));
            }

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

            if (row.Kind == RowKind.Sub)
            {
                using var arrow = new SolidBrush(UiTheme.TextSecondary);
                float ax = bounds.Right - 12, ay = bounds.Top + bounds.Height / 2f;
                g.FillPolygon(arrow, new[] { new PointF(ax, ay - 4), new PointF(ax + 5, ay), new PointF(ax, ay + 4) });
            }

            if (row.Kind == RowKind.SwitchRow)
            {
                int sw = 34, sh = 18;
                var track = new Rectangle(bounds.Right - sw - 6, bounds.Top + (bounds.Height - sh) / 2, sw, sh);
                using (var tp = Rounded(track, sh / 2f))
                using (var tb = new SolidBrush(row.SwitchOn ? UiTheme.Accent : UiTheme.SwitchOff))
                    g.FillPath(tb, tp);
                int kd = sh - 4;
                int kx = row.SwitchOn ? track.Right - kd - 2 : track.Left + 2;
                using var kb = new SolidBrush(Color.White);
                g.FillEllipse(kb, kx, track.Top + 2, kd, kd);
            }

            y += h;
        }
    }

    private static void DrawTooltipSample(Graphics g, int x, int y)
    {
        const string tip = "托盘悬停：仅外接 · 盖子打开 · 合盖不操作";
        using var font = new Font("Microsoft YaHei UI", 8.5f);

        Icon tray = TrayIconFactory.GetStateIcon(ScreenState.ExternalOnly);
        SizeF size = g.MeasureString(tip, font);
        var box = new Rectangle(x, y, (int)size.Width + 34, 26);

        using (var path = Rounded(box, 6f))
        using (var b = new SolidBrush(UiTheme.Current.Dark ? Color.FromArgb(0x33, 0x33, 0x33) : Color.FromArgb(0xFA, 0xFA, 0xFA)))
            g.FillPath(b, path);
        using (var pen = new Pen(UiTheme.MenuBorder))
        using (var path = Rounded(box, 6f))
            g.DrawPath(pen, path);

        using (var trayBmp = tray.ToBitmap())
        {
            g.DrawImage(trayBmp, new RectangleF(x + 7, y + 5, 16, 16));
        }

        using var tb = new SolidBrush(UiTheme.TextPrimary);
        g.DrawString(tip, font, tb, x + 29, y + 7);
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
