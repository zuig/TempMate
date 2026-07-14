using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TempMate
{
    public partial class MainForm : Form
    {
        private readonly TemperatureMonitor _monitor;
        private readonly AppConfig _config;
        private readonly System.Windows.Forms.Timer _updateTimer;
        private readonly ToolTip _toolTip;
        private NotifyIcon? _notifyIcon;

        private readonly Font _fontLabel;
        private readonly Font _fontValue;

        private bool _isDragging;
        private Point _dragStartMouse;
        private Point _dragStartLocation;

        private readonly List<DisplayItem> _items = new();

        private ToolStripMenuItem? _topMostMenuItem;
        private ToolStripMenuItem? _passThroughMenuItem;
        private ToolStripMenuItem? _lockMenuItem;
        private ToolStripMenuItem? _opacityMenuItem;

        // 视觉常量
        private const int CARD_RADIUS = 12;
        private const int CARD_PAD_H = 14;
        private const int CARD_PAD_V = 9;
        private const int INDICATOR_SIZE = 6;
        private const int INDICATOR_GAP = 5;
        private const int LABEL_VALUE_GAP = 5;
        private const int ITEM_GAP = 20;
        private const int DIVIDER_H = 14;

        public MainForm()
        {
            _config = AppConfig.Load();
            _monitor = new TemperatureMonitor();

            _fontLabel = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular);
            _fontValue = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);

            _updateTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _updateTimer.Tick += UpdateTimer_Tick;

            _toolTip = new ToolTip();

            InitializeComponent();
            InitializeContextMenu();
            InitNotifyIcon();
            ApplyConfig();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(26, 27, 31); // 深色底色，Win11 会被 Acrylic 覆盖
            ClientSize = new Size(280, 38);
            ControlBox = false;
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "MainForm";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "TempMate";
            TopMost = true;

            Load += MainForm_Load;
            Paint += MainForm_Paint;
            MouseDown += MainForm_MouseDown;
            MouseMove += MainForm_MouseMove;
            MouseUp += MainForm_MouseUp;
            MouseClick += MainForm_MouseClick;

            ResumeLayout(false);
        }

        #region 系统托盘

        private void InitNotifyIcon()
        {
            try
            {
                Icon? appIcon = LoadAppIcon();
                _notifyIcon = new NotifyIcon
                {
                    Icon = appIcon ?? SystemIcons.Application,
                    Text = "TempMate 温度监控",
                    Visible = true
                };

                var menu = new ContextMenuStrip();
                var settingsItem = new ToolStripMenuItem("设置");
                settingsItem.Click += (_, _) => OpenSettings();
                var exitItem = new ToolStripMenuItem("退出");
                exitItem.Click += (_, _) => Exit();
                menu.Items.Add(settingsItem);
                menu.Items.Add(exitItem);

                _notifyIcon.ContextMenuStrip = menu;
                _notifyIcon.DoubleClick += (_, _) => OpenSettings();
            }
            catch
            {
                // 托盘创建失败也不影响主窗体
            }
        }

        private static Icon? LoadAppIcon()
        {
            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region 右键菜单

        private void InitializeContextMenu()
        {
            var menu = new ContextMenuStrip();

            var settingsItem = new ToolStripMenuItem("设置");
            settingsItem.Click += (_, _) => OpenSettings();

            _topMostMenuItem = new ToolStripMenuItem("总是置顶") { Checked = _config.TopMost };
            _topMostMenuItem.Click += (_, _) =>
            {
                _config.TopMost = !_config.TopMost;
                _topMostMenuItem.Checked = _config.TopMost;
                ApplyConfig();
            };

            _passThroughMenuItem = new ToolStripMenuItem("鼠标穿透") { Checked = _config.MousePassThrough };
            _passThroughMenuItem.Click += (_, _) =>
            {
                _config.MousePassThrough = !_config.MousePassThrough;
                _passThroughMenuItem.Checked = _config.MousePassThrough;
                ApplyConfig();
            };

            _lockMenuItem = new ToolStripMenuItem("锁定窗口位置") { Checked = _config.LockPosition };
            _lockMenuItem.Click += (_, _) =>
            {
                _config.LockPosition = !_config.LockPosition;
                _lockMenuItem.Checked = _config.LockPosition;
                ApplyConfig();
            };

            _opacityMenuItem = new ToolStripMenuItem("窗口不透明度");
            foreach (int pct in new[] { 100, 75, 50, 25 })
            {
                int local = pct;
                var item = new ToolStripMenuItem($"{pct}%") { Checked = _config.OpacityPercent == pct };
                item.Click += (_, _) =>
                {
                    _config.OpacityPercent = local;
                    ApplyConfig();
                    UpdateOpacityMenu(_opacityMenuItem);
                };
                _opacityMenuItem.DropDownItems.Add(item);
            }

            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += (_, _) => Exit();

            menu.Items.Add(settingsItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_topMostMenuItem);
            menu.Items.Add(_passThroughMenuItem);
            menu.Items.Add(_lockMenuItem);
            menu.Items.Add(_opacityMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            ContextMenuStrip = menu;
        }

        private void UpdateOpacityMenu(ToolStripMenuItem? opacityMenu)
        {
            if (opacityMenu == null) return;
            foreach (ToolStripMenuItem item in opacityMenu.DropDownItems)
                item.Checked = item.Text == $"{_config.OpacityPercent}%";
        }

        private void RefreshContextMenuState()
        {
            if (_topMostMenuItem != null) _topMostMenuItem.Checked = _config.TopMost;
            if (_passThroughMenuItem != null) _passThroughMenuItem.Checked = _config.MousePassThrough;
            if (_lockMenuItem != null) _lockMenuItem.Checked = _config.LockPosition;
            UpdateOpacityMenu(_opacityMenuItem);
        }

        #endregion

        private void ApplyConfig()
        {
            TopMost = _config.TopMost;
            Opacity = _config.WindowOpacity;
            ApplyMousePassThrough(_config.MousePassThrough);
            EnableGlass();
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            PositionWindow();
            StartupHelper.SetStartup(_config.StartWithWindows);
            _updateTimer.Start();
            RefreshTemperatures();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Handle 刚创建时立即应用 DWM 圆角与毛玻璃，避免启动时先显示直角窗体
            EnableGlass();
            ApplyMousePassThrough(_config.MousePassThrough);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // 窗口首次显示后再确认一次，防止某些 DWM 效果在 Show 前未生效
            EnableGlass();
        }

        private void PositionWindow()
        {
            // 有记录：直接按记录的右下角锚点摆放，不重新计算
            if (_config.AnchorRight != int.MinValue && _config.AnchorBottom != int.MinValue)
            {
                Location = new Point(_config.AnchorRight - Width, _config.AnchorBottom - Height);
                return;
            }

            // 无记录：首次计算一次默认位置（主屏工作区右下角），并保存
            Screen targetScreen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
            Rectangle workArea = targetScreen.WorkingArea;
            Location = new Point(workArea.Right - Width - 6, workArea.Bottom - Height - 6);
            SavePosition();
        }

        private void SavePosition()
        {
            if (_isDragging || _config.LockPosition) return;

            // 记录窗口右下角的绝对屏幕坐标作为锚点。
            // 窗口宽度会随温度数值变化，用右下角锚点可保证视觉位置稳定。
            _config.AnchorRight = Right;
            _config.AnchorBottom = Bottom;
            _config.WindowX = Location.X;
            _config.WindowY = Location.Y;
            _config.Save();
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            // 拖拽时暂停数据刷新，避免重绘冲突导致卡顿
            if (_isDragging) return;
            RefreshTemperatures();
        }

        private string EffectiveDrive() =>
            string.IsNullOrEmpty(_config.DriveLetter) ? AppConfig.SystemDriveLetter() : _config.DriveLetter;

        private void RefreshTemperatures()
        {
            try
            {
                _monitor.Update();
                _items.Clear();

                var cpu = _monitor.GetCpuTemperature();
                if (cpu.Available) _items.Add(new DisplayItem("CPU", cpu.Value));

                var gpu = _monitor.GetGpuTemperature();
                if (gpu.Available) _items.Add(new DisplayItem("GPU", gpu.Value));

                string drive = EffectiveDrive();
                var hdd = _monitor.GetHardDriveTemperature(drive);
                if (hdd.Available) _items.Add(new DisplayItem($"{drive}盘", hdd.Value));

                var board = _monitor.GetMainboardTemperature();
                string boardLabel = "主板";
                if (!board.Available)
                {
                    var mem = _monitor.GetMemoryTemperature();
                    if (mem.Available) { board = mem; boardLabel = "内存"; }
                }
                if (board.Available) _items.Add(new DisplayItem(boardLabel, board.Value));

                _toolTip.SetToolTip(this,
                    string.Join("   ", _items.ConvertAll(i => $"{i.Label}:{i.Temp:F0}°")));
            }
            catch
            {
                // 读取失败保持已有项
            }

            UpdateLayout();
            Invalidate();
        }

        private void UpdateLayout()
        {
            int totalW = CARD_PAD_H * 2;
            using var g = CreateGraphics();
            for (int i = 0; i < _items.Count; i++)
            {
                string label = _items[i].Label;
                string value = $"{_items[i].Temp:F0}°";
                int lw = (int)Math.Ceiling(g.MeasureString(label, _fontLabel).Width);
                int vw = (int)Math.Ceiling(g.MeasureString(value, _fontValue).Width);
                totalW += INDICATOR_SIZE + INDICATOR_GAP + lw + LABEL_VALUE_GAP + vw;
                if (i < _items.Count - 1) totalW += ITEM_GAP;
            }

            int w = Math.Max(totalW, 80);
            int h = CARD_PAD_V * 2 + DIVIDER_H + 4;

            if (!_config.LockPosition && !_isDragging)
            {
                // 以记录的右下角锚点为准重新摆放；宽度变化时向左伸展，视觉位置保持不变。
                // 无记录时退化为当前右下角。
                int anchorRight = _config.AnchorRight != int.MinValue ? _config.AnchorRight : Right;
                int anchorBottom = _config.AnchorBottom != int.MinValue ? _config.AnchorBottom : Bottom;
                Width = w;
                Height = h;
                Location = new Point(anchorRight - Width, anchorBottom - Height);
            }
            else
            {
                Width = w;
                Height = h;
            }

            // Win10 下通过 Region 裁剪出圆角；Win11 由 DWM 原生处理。
            if (!IsWindows11())
                SetRoundedRegion();
        }

        private void MainForm_Paint(object? sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            DrawCard(g);
            DrawContent(g);
        }

        /// <summary>
        /// 绘制毛玻璃上方的半透明卡片：渐变填充 + 高光 + 边框。
        /// </summary>
        private void DrawCard(Graphics g)
        {
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // 渐变卡片底色
            using (var grad = new LinearGradientBrush(
                rect,
                Color.FromArgb(86, 30, 32, 38),
                Color.FromArgb(70, 22, 24, 30),
                90f))
            {
                DrawRoundRect(g, grad, rect.X, rect.Y, rect.Width, rect.Height, CARD_RADIUS);
            }

            // 顶部高光（1px）
            using (var highlight = new Pen(Color.FromArgb(35, 255, 255, 255), 1f))
            {
                var topPath = new GraphicsPath();
                int r = CARD_RADIUS;
                topPath.AddArc(r, 0, r, r, 180, 90);
                topPath.AddArc(Width - 1 - r, 0, r, r, 270, 90);
                g.DrawPath(highlight, topPath);
                topPath.Dispose();
            }

            // 边框
            using (var border = new Pen(Color.FromArgb(42, 255, 255, 255), 1f))
            {
                DrawRoundRectPath(g, border, rect.X, rect.Y, rect.Width, rect.Height, CARD_RADIUS);
            }
        }

        /// <summary>
        /// 绘制一行温度项：指示点 + 标签 + 数值 + 分隔线。
        /// </summary>
        private void DrawContent(Graphics g)
        {
            int x = CARD_PAD_H;
            int yCenter = Height / 2;

            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                string label = it.Label;
                string value = $"{it.Temp:F0}°";
                Color tempColor = GetTemperatureColor(it.Temp);

                SizeF labelSize = g.MeasureString(label, _fontLabel);
                SizeF valueSize = g.MeasureString(value, _fontValue);

                // 温度指示点（带微渐变）
                var dotRect = new Rectangle(x, yCenter - INDICATOR_SIZE / 2, INDICATOR_SIZE, INDICATOR_SIZE);
                using (var dotBrush = new LinearGradientBrush(dotRect,
                    Color.FromArgb(255, tempColor),
                    Color.FromArgb(220, Darken(tempColor, 0.15f)), 45f))
                {
                    g.FillEllipse(dotBrush, dotRect);
                }
                x += INDICATOR_SIZE + INDICATOR_GAP;

                // 标签（ muted 灰）
                using (var labelBrush = new SolidBrush(Color.FromArgb(155, 255, 255, 255)))
                {
                    float ly = yCenter - labelSize.Height / 2 - 1f;
                    g.DrawString(label, _fontLabel, labelBrush, x, ly);
                    x += (int)Math.Ceiling(labelSize.Width) + LABEL_VALUE_GAP;
                }

                // 数值（温度色，大一号，粗体）
                using (var valueBrush = new SolidBrush(tempColor))
                {
                    float vy = yCenter - valueSize.Height / 2 + 1f;
                    g.DrawString(value, _fontValue, valueBrush, x, vy);
                    x += (int)Math.Ceiling(valueSize.Width);
                }

                // 项间分隔线
                if (i < _items.Count - 1)
                {
                    x += ITEM_GAP / 2;
                    using (var divPen = new Pen(Color.FromArgb(25, 255, 255, 255), 1f))
                    {
                        g.DrawLine(divPen, x, yCenter - DIVIDER_H / 2, x, yCenter + DIVIDER_H / 2);
                    }
                    x += ITEM_GAP / 2;
                }
            }
        }

        #region 拖拽优化（用 SetWindowPos 替代 Location 属性）

        private void MainForm_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !_config.MousePassThrough && !_config.LockPosition)
            {
                _isDragging = true;
                _dragStartMouse = MousePosition;
                _dragStartLocation = Location;
                Capture = true;
            }
        }

        private void MainForm_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                int dx = MousePosition.X - _dragStartMouse.X;
                int dy = MousePosition.Y - _dragStartMouse.Y;
                int newX = _dragStartLocation.X + dx;
                int newY = _dragStartLocation.Y + dy;
                SetWindowPos(Handle, IntPtr.Zero, newX, newY, 0, 0,
                    SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }

        private void MainForm_MouseUp(object? sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                Capture = false;
                SavePosition();
            }
        }

        #endregion

        private void MainForm_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
                ContextMenuStrip?.Show(this, e.Location);
        }

        private void OpenSettings()
        {
            _updateTimer.Stop();
            using var dlg = new SettingsForm(_config, _monitor);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                ApplyConfig();
                PositionWindow();
                RefreshContextMenuState();
            }
            _updateTimer.Start();
        }

        private void Exit()
        {
            SavePosition();
            _updateTimer?.Stop();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            _monitor?.Dispose();
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SavePosition();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            _monitor?.Dispose();
            base.OnFormClosing(e);
        }

        #region Win32 / 工具

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMSBT_TRANSIENTWINDOW = 3;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;
        private const int DWMWCP_ROUNDSMALL = 3;

        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOZORDER = 0x0004;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        private static bool IsWindows11() =>
            Environment.OSVersion.Version.Major == 10 && Environment.OSVersion.Version.Build >= 22000;

        private void EnableGlass()
        {
            if (!IsHandleCreated) return;
            if (IsWindows11())
            {
                int backdrop = DWMSBT_TRANSIENTWINDOW;
                DwmSetWindowAttribute(Handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, Marshal.SizeOf(backdrop));
                int corner = DWMWCP_ROUND;
                DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, Marshal.SizeOf(corner));
            }
            else
            {
                SetRoundedRegion();
            }
        }

        private void SetRoundedRegion()
        {
            using var path = RoundedRectPath(0, 0, Width, Height, CARD_RADIUS);
            Region = new Region(path);
        }

        private void ApplyMousePassThrough(bool passThrough)
        {
            if (!IsHandleCreated) return;
            int exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
            exStyle = passThrough ? exStyle | WS_EX_TRANSPARENT : exStyle & ~WS_EX_TRANSPARENT;
            SetWindowLong(Handle, GWL_EXSTYLE, exStyle);
        }

        private static Color GetTemperatureColor(float temp)
        {
            if (temp < 45) return Color.FromArgb(78, 205, 196); // 青
            if (temp < 60) return Color.FromArgb(160, 220, 90); // 黄绿
            if (temp < 75) return Color.FromArgb(247, 183, 49); // 橙
            return Color.FromArgb(252, 92, 101);                // 红
        }

        private static Color Darken(Color c, float factor)
        {
            return Color.FromArgb(c.A,
                (int)(c.R * (1 - factor)),
                (int)(c.G * (1 - factor)),
                (int)(c.B * (1 - factor)));
        }

        private static void DrawRoundRect(Graphics g, Brush brush, int x, int y, int w, int h, int r)
        {
            using var path = RoundedRectPath(x, y, w, h, r);
            g.FillPath(brush, path);
        }

        private static void DrawRoundRectPath(Graphics g, Pen pen, int x, int y, int w, int h, int r)
        {
            using var path = RoundedRectPath(x, y, w, h, r);
            g.DrawPath(pen, path);
        }

        private static GraphicsPath RoundedRectPath(int x, int y, int w, int h, int r)
        {
            var path = new GraphicsPath();
            path.AddArc(x, y, r, r, 180, 90);
            path.AddArc(x + w - r, y, r, r, 270, 90);
            path.AddArc(x + w - r, y + h - r, r, r, 0, 90);
            path.AddArc(x, y + h - r, r, r, 90, 90);
            path.CloseFigure();
            return path;
        }

        #endregion

        private sealed class DisplayItem
        {
            public string Label { get; }
            public float Temp { get; }
            public DisplayItem(string label, float temp) { Label = label; Temp = temp; }
        }
    }
}
