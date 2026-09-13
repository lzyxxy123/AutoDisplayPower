using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AutoDisplayPower.Utils;

/// <summary>菜单项的角色标记（渲染器据此决定画法）。</summary>
internal static class MenuTag
{
    /// <summary>三个模式项：用“整行浅色高亮”表示当前模式，不画勾。</summary>
    public const string Mode = "mode";

    /// <summary>开关项：右侧绘制滑动开关，不画勾。</summary>
    public const string Switch = "switch";

    /// <summary>电源策略行：刚发生变化时短暂强调。</summary>
    public const string Emphasize = "emphasize";
}

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
/// 2) 只读（禁用）状态行也显示自定义颜色，不再被画成灰色；
/// 3) 模式项用“整行浅色药丸”表示当前模式（不画勾）；
/// 4) 开关项在右侧绘制滑动开关（轨道 + 圆钮）；
/// 5) 其余勾选项用矢量彩色对钩（颜色跟随文字色）。
/// </summary>
internal sealed class ModernMenuRenderer : ToolStripProfessionalRenderer
{
    private const int SwitchWidth = 30;
    private const int SwitchHeight = 15;

    public ModernMenuRenderer() : base(new ModernColorTable())
    {
        RoundedEdges = false;
    }

    /// <summary>
    /// 绘制菜单背景时顺带画“滑动开关”。
    /// 说明：实测 OnRenderMenuItemBackground 对未选中项不会被调用，OnRenderItemImage 的绘图区
    /// 又被裁剪在图标范围内，都不适合画开关；OnRenderToolStripBackground 拿到的是整个菜单表面、
    /// 无裁剪，最可靠。
    /// </summary>
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        base.OnRenderToolStripBackground(e);

        foreach (ToolStripItem item in e.ToolStrip.Items)
        {
            if ((item.Tag as string) == MenuTag.Switch)
                DrawSwitch(e.Graphics, item.Bounds, IsChecked(item));
        }
    }

    /// <summary>
    /// 图标绘制：不做“禁用变灰”——状态行是只读(禁用)项，但图标必须保持彩色
    /// （WinForms 默认渲染器会把禁用项的图标灰度化）。
    /// </summary>
    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Image is null) return;

        Graphics g = e.Graphics;
        Rectangle r = e.ImageRectangle;
        if (r.Width <= 0 || r.Height <= 0)
            r = new Rectangle(e.Item.ContentRectangle.Left + 4, e.Item.ContentRectangle.Top, 16, 16);

        SmoothingMode old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawImage(e.Image, r);
        g.SmoothingMode = old;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (!e.Item.Enabled)
        {
            // 让“只读状态行”保留自己的颜色，而不是被默认渲染器画成灰色
            Font font = e.TextFont ?? e.Item.Font;
            TextRenderer.DrawText(e.Graphics, e.Text, font, e.TextRectangle, e.Item.ForeColor, e.TextFormat);
            return;
        }

        base.OnRenderItemText(e);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        string? tag = e.Item.Tag as string;
        bool activeMode = tag == MenuTag.Mode && IsChecked(e.Item);
        bool emphasize = tag == MenuTag.Emphasize;

        if (e.Item.Selected)
        {
            base.OnRenderMenuItemBackground(e);
        }
        else if (emphasize)
        {
            DrawPill(e.Graphics, e.Item.Bounds, UiTheme.PillStrong);
        }

        if (activeMode)
        {
            if (UiTheme.Current.UseLeftBar)
            {
                DrawLeftBar(e.Graphics, e.Item.Bounds, UiTheme.Accent); // 极简主题：左侧细色条
            }
            else if (!e.Item.Selected)
            {
                DrawPill(e.Graphics, e.Item.Bounds, UiTheme.Pill);
            }
        }

        if (tag == MenuTag.Switch)
            DrawSwitch(e.Graphics, e.Item.Bounds, IsChecked(e.Item));
    }

    /// <summary>极简主题：当前模式用左侧细色条表示。</summary>
    private static void DrawLeftBar(Graphics g, Rectangle bounds, Color color)
    {
        var r = new Rectangle(bounds.Left + 4, bounds.Top + 4, 3, bounds.Height - 8);
        if (r.Height <= 0) return;

        SmoothingMode old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            using var path = Rounded(r, 1.5f);
            using var brush = new SolidBrush(color);
            g.FillPath(brush, path);
        }
        finally
        {
            g.SmoothingMode = old;
        }
    }

    private static bool IsChecked(ToolStripItem item) => item is ToolStripMenuItem m && m.Checked;

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        string? tag = e.Item.Tag as string;
        // 模式项用整行高亮表示、开关项用滑动开关表示 —— 都不画对钩
        if (tag is MenuTag.Mode or MenuTag.Switch) return;

        Rectangle r = e.ImageRectangle;
        if (r.IsEmpty || r.Width < 4 || r.Height < 4)
        {
            Rectangle c = e.Item.ContentRectangle;
            r = new Rectangle(c.Left + 2, c.Top + (c.Height - 14) / 2, 14, 14);
        }

        Graphics g = e.Graphics;
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

            g.DrawLines(pen, new[]
            {
                new PointF(r.Left + r.Width * 0.12f, r.Top + r.Height * 0.52f),
                new PointF(r.Left + r.Width * 0.40f, r.Bottom - r.Height * 0.20f),
                new PointF(r.Right - r.Width * 0.06f, r.Top + r.Height * 0.20f),
            });
        }
        finally
        {
            g.SmoothingMode = old;
        }
    }

    /// <summary>整行浅色药丸背景。</summary>
    private static void DrawPill(Graphics g, Rectangle bounds, Color color)
    {
        var r = new Rectangle(bounds.Left + 3, bounds.Top + 1, bounds.Width - 6, bounds.Height - 2);
        if (r.Width <= 0 || r.Height <= 0) return;

        SmoothingMode old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            using var path = Rounded(r, 5f);
            using var brush = new SolidBrush(color);
            g.FillPath(brush, path);
        }
        finally
        {
            g.SmoothingMode = old;
        }
    }

    /// <summary>右侧滑动开关（轨道 + 圆钮）。</summary>
    private static void DrawSwitch(Graphics g, Rectangle bounds, bool on)
    {
        var track = new Rectangle(
            bounds.Right - SwitchWidth - 10,
            bounds.Top + (bounds.Height - SwitchHeight) / 2,
            SwitchWidth,
            SwitchHeight);

        SmoothingMode old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            using (var path = Rounded(track, SwitchHeight / 2f))
            using (var brush = new SolidBrush(on ? UiTheme.Accent : UiTheme.SwitchOff))
            {
                g.FillPath(brush, path);
            }

            int knob = SwitchHeight - 4;
            int knobX = on ? track.Right - knob - 2 : track.Left + 2;
            using var knobBrush = new SolidBrush(Color.White);
            g.FillEllipse(knobBrush, knobX, track.Top + 2, knob, knob);
        }
        finally
        {
            g.SmoothingMode = old;
        }
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
