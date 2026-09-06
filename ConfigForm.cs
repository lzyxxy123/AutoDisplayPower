using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AutoDisplayPower.Services;
using AutoDisplayPower.Utils;

namespace AutoDisplayPower;

/// <summary>显示器型号配置对话框：输入内屏/外接屏型号，程序据此归类，支持换显示器后重新配置。</summary>
public sealed class ConfigForm : Form
{
    private readonly TextBox _txtInternal;
    private readonly TextBox _txtExternal;
    private readonly CheckBox _chkAnyExternal;
    private readonly Label _lblDetected;
    private readonly Button _btnSave;
    private readonly Button _btnCancel;

    public ConfigForm()
    {
        Text = "显示器型号配置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F);
        ClientSize = new Size(440, 280);

        int y = 16;
        var lblDetectedTitle = new Label { Text = "当前检测到的显示器：", AutoSize = true, Location = new Point(15, y) };
        y += 24;
        _lblDetected = new Label
        {
            Text = BuildDetectedText(),
            AutoSize = true,
            Location = new Point(15, y),
            ForeColor = Color.DimGray,
        };
        y += 28;

        var lblInternal = new Label { Text = "内屏型号：", AutoSize = true, Location = new Point(15, y) };
        _txtInternal = new TextBox { Location = new Point(120, y - 2), Width = 300 };
        y += 34;

        var lblExternal = new Label { Text = "外接屏型号：", AutoSize = true, Location = new Point(15, y) };
        _txtExternal = new TextBox { Location = new Point(120, y - 2), Width = 300 };
        y += 28;
        var lblExtHint = new Label
        {
            Text = "多个型号用逗号分隔；勾选下方选项后可不填",
            AutoSize = true,
            Location = new Point(120, y - 2),
            ForeColor = Color.Gray,
        };
        y += 26;

        _chkAnyExternal = new CheckBox
        {
            Text = "任意外接屏（换外接屏免配置，推荐）",
            AutoSize = true,
            Location = new Point(120, y),
        };
        y += 40;

        _btnSave = new Button { Text = "保存", Location = new Point(ClientSize.Width - 195, y), Size = new Size(85, 32) };
        _btnCancel = new Button { Text = "取消", Location = new Point(ClientSize.Width - 100, y), Size = new Size(85, 32) };
        AcceptButton = _btnSave;
        CancelButton = _btnCancel;

        Controls.AddRange(new Control[]
        {
            lblDetectedTitle, _lblDetected, lblInternal, _txtInternal,
            lblExternal, _txtExternal, lblExtHint, _chkAnyExternal, _btnSave, _btnCancel,
        });

        // 预填当前配置
        var cfg = MonitorConfig.Load();
        _txtInternal.Text = cfg.InternalModel;
        _txtExternal.Text = string.Join(",", cfg.ExternalModels);
        _chkAnyExternal.Checked = cfg.AnyExternal;

        _btnSave.Click += (_, _) => OnSave();
        _btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
    }

    private static string BuildDetectedText()
    {
        var snap = DisplayDetector.Scan();
        if (!snap.Ok) return "读取失败";
        if (snap.Monitors.Count == 0) return "未检测到显示器";
        return string.Join("、", snap.Monitors.Select(m => m.ModelName));
    }

    private void OnSave()
    {
        var cfg = new MonitorConfig
        {
            InternalModel = _txtInternal.Text.Trim(),
            AnyExternal = _chkAnyExternal.Checked,
        };
        cfg.ExternalModels = _txtExternal.Text
            .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        cfg.Save();
        MessageBox.Show(this, "配置已保存，将立即生效。", "AutoDisplayPower",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }
}
