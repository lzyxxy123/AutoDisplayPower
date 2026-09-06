using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace AutoDisplayPower.Utils;

/// <summary>运行时绘制托盘图标：深色圆形 + 黄色闪电（电源管理语义），无需外部 .ico 资源。</summary>
internal static class TrayIconFactory
{
    public static Icon Create()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using (var bg = new SolidBrush(Color.FromArgb(30, 41, 59)))
                g.FillEllipse(bg, 0, 0, 31, 31);
            using (var ring = new Pen(Color.FromArgb(125, 211, 252), 2f))
                g.DrawEllipse(ring, 1.5f, 1.5f, 28f, 28f);

            var bolt = new[]
            {
                new PointF(19.5f, 4f),
                new PointF(10f, 17.5f),
                new PointF(14.5f, 17.5f),
                new PointF(12.5f, 28f),
                new PointF(22f, 14.5f),
                new PointF(17.5f, 14.5f),
            };
            using var boltBrush = new SolidBrush(Color.FromArgb(253, 224, 71));
            g.FillPolygon(boltBrush, bolt);
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

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
