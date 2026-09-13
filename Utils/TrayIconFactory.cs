using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using AutoDisplayPower.Services;

namespace AutoDisplayPower.Utils;

/// <summary>
/// 运行时绘制托盘图标：彩色"显示器 + 电源闪电"造型，颜色随当前屏幕状态变化
/// （仅外接=蓝 / 仅笔记本=青 / 扩展=紫 / 未知=灰）。按状态缓存，避免重复绘制与句柄泄漏。
/// </summary>
internal static class TrayIconFactory
{
    private static readonly Dictionary<ScreenState, Icon> Cache = new();

    public static Icon GetStateIcon(ScreenState state)
    {
        if (Cache.TryGetValue(state, out Icon? cached) && cached is not null) return cached;

        Icon icon = Create(state);
        Cache[state] = icon;
        return icon;
    }

    private static Icon Create(ScreenState state)
    {
        const int S = 32;
        Color baseColor = UiTheme.ScreenStateColor(state);

        using var bmp = new Bitmap(S, S);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            // ---- 显示器主体（圆角矩形 + 斜向渐变）----
            var body = new Rectangle(2, 5, 28, 18);
            using (var bodyPath = RoundedRect(body, 4f))
            using (var bodyBrush = new LinearGradientBrush(
                       new Rectangle(body.X - 1, body.Y - 1, body.Width + 2, body.Height + 2),
                       UiTheme.Lighten(baseColor, 0.35), UiTheme.Darken(baseColor, 0.12), 55f))
            {
                g.FillPath(bodyBrush, bodyPath);
            }

            // 屏幕内层高光，增加立体感
            var inner = new Rectangle(body.X + 2, body.Y + 2, body.Width - 4, body.Height - 4);
            using (var innerPath = RoundedRect(inner, 3f))
            using (var innerBrush = new SolidBrush(Color.FromArgb(46, 255, 255, 255)))
            {
                g.FillPath(innerBrush, innerPath);
            }

            // ---- 中央白色闪电（电源语义）----
            using (var bolt = new SolidBrush(Color.White))
            {
                g.FillPolygon(bolt, new[]
                {
                    new PointF(18.2f, 7.6f),
                    new PointF(11.4f, 15.0f),
                    new PointF(15.3f, 15.0f),
                    new PointF(13.6f, 20.6f),
                    new PointF(20.6f, 12.8f),
                    new PointF(16.4f, 12.8f),
                });
            }

            // ---- 支架与底座 ----
            using (var standBrush = new SolidBrush(UiTheme.Darken(baseColor, 0.18)))
            {
                g.FillRectangle(standBrush, 14, 23, 4, 3);
                using var basePath = RoundedRect(new Rectangle(9, 26, 14, 3), 1.5f);
                g.FillPath(standBrush, basePath);
            }
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static GraphicsPath RoundedRect(Rectangle r, float radius)
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

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
