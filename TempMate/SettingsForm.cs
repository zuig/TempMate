using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TempMate
{
    public partial class SettingsForm : Form
    {
        private readonly AppConfig _config;
        private readonly TemperatureMonitor _monitor;

        private CheckBox _chkTopMost = null!;
        private CheckBox _chkMousePassThrough = null!;
        private CheckBox _chkLockPosition = null!;
        private ComboBox _cmbOpacity = null!;
        private ComboBox _cmbDrive = null!;
        private CheckBox _chkSecondaryScreen = null!;
        private CheckBox _chkStartWithWindows = null!;
        private Button _btnOk = null!;
        private Button _btnCancel = null!;

        public SettingsForm(AppConfig config, TemperatureMonitor monitor)
        {
            _config = config;
            _monitor = monitor;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            Text = "TempMate 设置";
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(360, 350);
            Padding = new Padding(12);
            Font = new Font("Microsoft YaHei UI", 9F);

            int y = 16;
            int x = 20;

            _chkTopMost = CreateCheckBox("总是置顶", x, y);
            y += 32;

            _chkMousePassThrough = CreateCheckBox("鼠标穿透", x, y);
            y += 32;

            _chkLockPosition = CreateCheckBox("锁定窗口位置", x, y);
            y += 40;

            int maxLabelWidth = Math.Max(
                TextRenderer.MeasureText("窗口不透明度：", Font).Width,
                TextRenderer.MeasureText("监控硬盘：", Font).Width);
            int comboX = x + maxLabelWidth + 12;

            var lblOpacity = new Label
            {
                Text = "窗口不透明度：",
                Location = new Point(x, y + 3),
                AutoSize = true
            };
            _cmbOpacity = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(comboX, y),
                Width = 80
            };
            _cmbOpacity.Items.AddRange(new object[] { "100%", "75%", "50%", "25%" });
            y += 40;

            var lblDrive = new Label
            {
                Text = "监控硬盘：",
                Location = new Point(x, y + 3),
                AutoSize = true
            };
            _cmbDrive = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(comboX, y),
                Width = ClientSize.Width - comboX - 24
            };
            y += 44;

            _chkSecondaryScreen = CreateCheckBox("在副屏右下角显示（双屏）", x, y);
            y += 32;

            _chkStartWithWindows = CreateCheckBox("开机自启动", x, y);
            y += 32;

            _btnOk = new Button
            {
                Text = "确定",
                DialogResult = DialogResult.OK,
                Location = new Point(x + 130, y),
                Size = new Size(90, 30)
            };
            _btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(x + 230, y),
                Size = new Size(90, 30)
            };

            _btnOk.Click += BtnOk_Click;

            Controls.Add(_chkTopMost);
            Controls.Add(_chkMousePassThrough);
            Controls.Add(_chkLockPosition);
            Controls.Add(lblOpacity);
            Controls.Add(_cmbOpacity);
            Controls.Add(lblDrive);
            Controls.Add(_cmbDrive);
            Controls.Add(_chkSecondaryScreen);
            Controls.Add(_chkStartWithWindows);
            Controls.Add(_btnOk);
            Controls.Add(_btnCancel);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            Load += SettingsForm_Load;

            ResumeLayout(false);
            PerformLayout();
        }

        private CheckBox CreateCheckBox(string text, int x, int y)
        {
            return new CheckBox
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true
            };
        }

        private async void SettingsForm_Load(object? sender, EventArgs e)
        {
            _chkTopMost.Checked = _config.TopMost;
            _chkMousePassThrough.Checked = _config.MousePassThrough;
            _chkLockPosition.Checked = _config.LockPosition;
            _chkSecondaryScreen.Checked = _config.UseSecondaryScreen;
            _chkStartWithWindows.Checked = StartupHelper.IsStartupEnabled();

            _cmbOpacity.SelectedItem = $"{_config.OpacityPercent}%";
            if (_cmbOpacity.SelectedIndex < 0)
                _cmbOpacity.SelectedIndex = 0;

            // 硬盘列表可能在 WMI 查询时耗时，放到后台线程加载，避免设置窗口卡顿。
            _cmbDrive.Items.Clear();
            _cmbDrive.Items.Add("正在读取硬盘列表...");
            _cmbDrive.SelectedIndex = 0;
            _cmbDrive.Enabled = false;

            try
            {
                List<HardDriveInfo> drives = await Task.Run(() => _monitor.GetAllHardDrives());
                if (IsDisposed) return;
                PopulateDriveList(drives);
            }
            catch
            {
                if (IsDisposed) return;
                _cmbDrive.Items.Clear();
                _cmbDrive.Items.Add("无法枚举硬盘");
                _cmbDrive.SelectedIndex = 0;
            }
            finally
            {
                if (!IsDisposed)
                    _cmbDrive.Enabled = true;
            }
        }

        private void PopulateDriveList(List<HardDriveInfo> drives)
        {
            _cmbDrive.Items.Clear();
            if (drives.Count == 0)
            {
                _cmbDrive.Items.Add("未检测到硬盘");
                _cmbDrive.SelectedIndex = 0;
                return;
            }

            foreach (var drive in drives)
            {
                string display = string.IsNullOrEmpty(drive.DriveLetter)
                    ? drive.Name
                    : $"[{drive.DriveLetter}] {drive.Name}";
                _cmbDrive.Items.Add(display);
            }

            // 尝试选中当前配置的盘符（未配置则跟随系统盘）
            string target = (string.IsNullOrEmpty(_config.DriveLetter)
                ? AppConfig.SystemDriveLetter()
                : _config.DriveLetter).Trim().TrimEnd(':', '\\').ToUpperInvariant();
            for (int i = 0; i < _cmbDrive.Items.Count; i++)
            {
                string item = _cmbDrive.Items[i]?.ToString() ?? string.Empty;
                if (item.Contains($"[{target}:]", StringComparison.OrdinalIgnoreCase))
                {
                    _cmbDrive.SelectedIndex = i;
                    return;
                }
            }

            if (_cmbDrive.Items.Count > 0)
                _cmbDrive.SelectedIndex = 0;
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            _config.TopMost = _chkTopMost.Checked;
            _config.MousePassThrough = _chkMousePassThrough.Checked;
            _config.LockPosition = _chkLockPosition.Checked;
            _config.UseSecondaryScreen = _chkSecondaryScreen.Checked;
            _config.StartWithWindows = _chkStartWithWindows.Checked;
            StartupHelper.SetStartup(_chkStartWithWindows.Checked);

            string? opacityText = _cmbOpacity.SelectedItem?.ToString();
            if (opacityText != null && int.TryParse(opacityText.TrimEnd('%'), out int opacity))
            {
                _config.OpacityPercent = opacity;
            }

            string? driveText = _cmbDrive.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(driveText) && driveText.Contains('['))
            {
                int bracketStart = driveText.IndexOf('[');
                int bracketEnd = driveText.IndexOf(']', bracketStart);
                if (bracketStart >= 0 && bracketEnd > bracketStart)
                {
                    string letter = driveText.Substring(bracketStart + 1, bracketEnd - bracketStart - 1)
                        .TrimEnd(':', '\\');
                    _config.DriveLetter = letter;
                }
            }
            else if (!string.IsNullOrEmpty(driveText))
            {
                // 没有盘符的纯名称项：保持原配置或默认 C
                _config.DriveLetter = string.IsNullOrEmpty(_config.DriveLetter)
                    ? AppConfig.SystemDriveLetter()
                    : _config.DriveLetter;
            }

            _config.Save();
        }
    }
}
