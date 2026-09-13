using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AutoDisplayPower.Utils;

/// <summary>现代浅色菜单配色表（Fluent 风格）。</summary>
internal sealed class ModernColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => UiTheme.MenuBack;
    public override Color MenuBorder => UiTheme.MenuBorder;
    public override Color MenuItemBorder => UiTheme.HoverBorder;
    public override Color MenuItemSelected => UiTheme.Hover;
    public override Color MenuItemSelectedGradientBegin => UiTheme.Hover;
    public override Color MenuItemSelectedGradientEnd => UiTheme.Hover;
    public override Color MenuItemPressedGradientBegin => UiTheme.Hover;
    public override Color MenuItemPressedGradientEnd => UiTheme.Hover;
    public override Color ImageMarginGradientBegin => UiTheme.MenuBack;
    public override Color ImageMarginGradientMiddle => UiTheme.MenuBack;
    public override Color ImageMarginGradientEnd => UiTheme.MenuBack;
    public override Color SeparatorDark => UiTheme.MenuBorder;
    public override Color SeparatorLight => UiTheme.MenuBack;
    public override Color CheckBackground => UiTheme.Hover;
    public override Color CheckSelectedBackground => UiTheme.Hover;
    public override Color CheckPressedBackground => UiTheme.Hover;
}

/// <summary>
/// 现代风格菜单渲染器：
/// 1) 浅色背景 + 淡蓝悬停 + 细边框/分隔线；
/// 2) 只读（禁用）的状态行也能显示自定义颜色（默认渲染器会把禁用项一律画成灰）；
/// 3) 对钩用矢量绘制，颜色跟随该项文字色。
/// </summary>
internal sealed class ModernMenuRenderer : ToolStripProfessionalRenderer
{
    public ModernMenuRenderer() : base(new ModernColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (!e.Item.Enabled)
        {
            // 让“只读状态行”保留自己的语义色，而不是被画成灰色
            Font font = e.TextFont ?? e.Item.Font;
            TextRenderer.DrawText(e.Graphics, e.Text, font, e.TextRectangle, e.Item.ForeColor, e.TextFormat);
            return;
        }

        base.OnRenderItemText(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        Rectangle r = e.ImageRectangle;
        if (r.IsEmpty || r.Width < 4 || r.Height < 4)
        {
            Rectangle c = e.Item.ContentRectangle;
            r = new Rectangle(c.Left + 2, c.Top + (c.Height - 14) / 2, 14, 14);
        }

        var g = e.Graphics;
        SmoothingMode old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            using var pen = new Pen(e.Item.ForeColor, 1.9f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };

            float x0 = r.Left + r.Width * 0.12f;
            float y0 = r.Top + r.Height * 0.52f;
            float x1 = r.Left + r.Width * 0.40f;
            float y1 = r.Bottom - r.Height * 0.20f;
            float x2 = r.Right - r.Width * 0.06f;
            float y2 = r.Top + r.Height * 0.20f;

            g.DrawLines(pen, new[]
            {
                new PointF(x0, y0),
                new PointF(x1, y1),
                new PointF(x2, y2),
            });
        }
        finally
        {
            g.SmoothingMode = old;
        }
    }
}
