using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AutoDisplayPower.Utils;

/// <summary>菜单项的角色标记。</summary>
internal enum MenuMarkKind
{
    /// <summary>无特殊标记。</summary>
    None,

    /// <summary>右侧显示对钩（Active=true 时）。</summary>
    CheckRight,

    /// <summary>右侧显示滑动开关（状态取 Checked）。</summary>
    Switch,

    /// <summary>短暂强调（电源策略变化时）。</summary>
    Emphasize,
}

/// <summary>挂在 <see cref="ToolStripItem.Tag"/> 上的标记对象。</summary>
internal sealed class MenuMark
{
    public MenuMarkKind Kind { get; init; }

    /// <summary>对钩项：是否处于选中状态。</summary>
    public bool Active { get; set; }
}

/// <summary>现代浅色菜单配色表（跟随主题）。</summary>
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
/// 菜单渲染器：
/// 1) 浅色/深色背景 + 悬停高亮 + 细边框（跟随主题）；
/// 2) 只读（禁用）状态行仍显示彩色图标与自定义文字色（默认渲染器会把禁用项灰度化）；
/// 3) “选中”由**右侧**的对钩表示；“开机自启动”为**右侧**滑动开关；
///    两者都在 OnRenderToolStripBackground（整个菜单表面、无裁剪）里绘制 —— 实测
///    OnRenderMenuItemBackground 对未选中项不会被调用，OnRenderItemImage 的绘图区又被裁剪。
/// </summary>
internal sealed class ModernMenuRenderer : ToolStripProfessionalRenderer
{
    private const int SwitchWidth = 30;
    private const int SwitchHeight = 15;
    private const int CheckBoxSize = 14;

    public ModernMenuRenderer() : base(new ModernColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        base.OnRenderToolStripBackground(e);

        foreach (ToolStripItem item in e.ToolStrip.Items)
        {
            if (item.Tag is not MenuMark mark) continue;

            switch (mark.Kind)
            {
                case MenuMarkKind.Switch:
                    DrawSwitch(e.Graphics, item.Bounds, IsChecked(item));
                    break;
                case MenuMarkKind.CheckRight when mark.Active:
                    DrawRightCheck(e.Graphics, item.Bounds);
                    break;
                case MenuMarkKind.Emphasize:
                    DrawPill(e.Graphics, item.Bounds, UiTheme.PillStrong);
                    break;
            }
        }
    }

    /// <summary>整行浅色高亮（策略变化时的强调）。</summary>
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

    /// <summary>图标绘制：不做“禁用变灰”（状态行是只读项，但图标必须保持彩色）。</summary>
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
            // 让“只读状态行”保留自己的颜色，而不是被画成灰色
            Font font = e.TextFont ?? e.Item.Font;
            TextRenderer.DrawText(e.Graphics, e.Text, font, e.TextRectangle, e.Item.ForeColor, e.TextFormat);
            return;
        }

        base.OnRenderItemText(e);
    }

    /// <summary>带标记的项不画默认对钩（选中由右侧对钩/开关表示）。</summary>
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Item.Tag is MenuMark) return;
        base.OnRenderItemCheck(e);
    }

    /// <summary>当前模式：右侧对钩（主色）。</summary>
    private static void DrawRightCheck(Graphics g, Rectangle bounds)
    {
        float size = CheckBoxSize;
        float right = bounds.Right - 14;
        var r = new RectangleF(right - size, bounds.Top + (bounds.Height - size) / 2f, size, size);

        SmoothingMode old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            using var pen = new Pen(UiTheme.Accent, 1.9f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };

            g.DrawLines(pen, new[]
            {
                new PointF(r.Left, r.Top + r.Height * 0.52f),
                new PointF(r.Left + r.Width * 0.38f, r.Bottom - r.Height * 0.06f),
                new PointF(r.Right, r.Top + r.Height * 0.08f),
            });
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
            bounds.Right - SwitchWidth - 12,
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

    private static bool IsChecked(ToolStripItem item) => item is ToolStripMenuItem m && m.Checked;

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
