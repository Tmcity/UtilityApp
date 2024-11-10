using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Drawing;
using MessageBox = System.Windows.MessageBox;
using Timer = System.Threading.Timer;
using Application = System.Windows.Application;


namespace UtilityApp
{
    public partial class MainWindow : Window
    {
        private Timer _timer;
        private DateTime? _powerOutageStartTime;
        private bool _shutdownTriggered;
        private bool _countdownInProgress;
        private readonly object _lockObject = new object();
        private NotifyIcon _notifyIcon;
        private bool isLoadingSettings = true;

        public MainWindow()
        {
            InitializeComponent();
            isLoadingSettings = true; // 标志设置为 true，防止初始化过程中的事件触发
            this.Loaded += MainWindow_Loaded;
            InitializeNotifyIcon();
            //StartMonitoring();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings(); // 加载设置

            isLoadingSettings = false; // 完成加载后，设置为 false
            Debug.WriteLine("初始化完成.");

            // 根据加载的设置决定是否启动检测
            if (EnableCheckBox.IsChecked == true)
            {
                StartMonitoring();
                Debug.WriteLine("检测已根据用户设置启动。");
            }
        }

        private void InitializeNotifyIcon()
        {
            // 创建 ContextMenuStrip 并添加菜单项
            var contextMenu = new ContextMenuStrip();
            var showMenuItem = new ToolStripMenuItem("显示");
            var exitMenuItem = new ToolStripMenuItem("退出");

            showMenuItem.Click += (s, e) => ShowMainWindow();
            exitMenuItem.Click += (s, e) => ExitApplication();

            contextMenu.Items.Add(showMenuItem);
            contextMenu.Items.Add(exitMenuItem);

            // 初始化 NotifyIcon 并关联 ContextMenuStrip
            _notifyIcon = new NotifyIcon
            {
                Icon = new Icon("app.ico"), // 确保项目中有一个名为 app.ico 的图标文件
                Visible = true,
                Text = "UtilityApp",
                ContextMenuStrip = contextMenu
            };
            _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
        }

        private void ExitApplication()
        {
            _notifyIcon.Visible = false; // 隐藏托盘图标
            Application.Current.Shutdown(); // 关闭应用程序
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true; // 取消关闭事件
            Hide(); // 隐藏窗口
            base.OnClosing(e);
        }

        //private void AutoSave_TextChanged(object sender, TextChangedEventArgs e) => SaveSettings();
        //private void AutoSave_Checked(object sender, RoutedEventArgs e) => SaveSettings();
        private void AutoSave_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoadingSettings) return; // 防止初始化时事件触发
            Debug.WriteLine("AutoSave_TextChanged triggered.");
            SaveSettings();
        }

        private void AutoSave_Checked(object sender, RoutedEventArgs e)
        {
            if (isLoadingSettings) return; // 防止初始化时事件触发
            Debug.WriteLine("AutoSave_Checked triggered.");
            SaveSettings();
        }

        private void LoadSettings()
        {
            isLoadingSettings = true; // 开始初始化

            // 加载设置到控件
            GatewayAddressTextBox.Text = Properties.Settings.Default.GatewayAddress;
            CheckIntervalTextBox.Text = Properties.Settings.Default.CheckInterval.ToString();
            ShutdownThresholdTextBox.Text = Properties.Settings.Default.ShutdownThreshold.ToString();
            EnableCheckBox.IsChecked = Properties.Settings.Default.EnableCheck;
            DebugCheckBox.IsChecked = Properties.Settings.Default.DebugCheck;

            Debug.WriteLine("加载设置:");
            Debug.WriteLine($"网关地址: {Properties.Settings.Default.GatewayAddress}");
            Debug.WriteLine($"检查时间间隔: {Properties.Settings.Default.CheckInterval}");
            Debug.WriteLine($"关机阈值: {Properties.Settings.Default.ShutdownThreshold}");
            Debug.WriteLine($"启用检测: {Properties.Settings.Default.EnableCheck}");
            Debug.WriteLine($"调试模式: {Properties.Settings.Default.DebugCheck}");

            isLoadingSettings = false; // 完成初始化

            // 在加载完成后再绑定事件
            CheckIntervalTextBox.TextChanged += AutoSave_TextChanged;
            ShutdownThresholdTextBox.TextChanged += AutoSave_TextChanged;
            GatewayAddressTextBox.TextChanged += AutoSave_TextChanged;
            EnableCheckBox.Checked += AutoSave_Checked;
            EnableCheckBox.Unchecked += AutoSave_Checked;
            DebugCheckBox.Checked += AutoSave_Checked;
            DebugCheckBox.Unchecked += AutoSave_Checked;

            Debug.WriteLine("初始化完成。");
        }

        private void SaveSettings()
        {
            if (!ValidateControls()) return;

            Properties.Settings.Default.GatewayAddress = GatewayAddressTextBox.Text;

            if (!TryParseInt(CheckIntervalTextBox.Text, out int checkInterval))
            {
                checkInterval = 10000; // 提供默认值
            }
            if (!TryParseInt(ShutdownThresholdTextBox.Text, out int shutdownThreshold))
            {
                shutdownThreshold = 60000; // 提供默认值
            }

            Properties.Settings.Default.CheckInterval = checkInterval;
            Properties.Settings.Default.ShutdownThreshold = shutdownThreshold;
            Properties.Settings.Default.EnableCheck = EnableCheckBox.IsChecked ?? false;
            Properties.Settings.Default.DebugCheck = DebugCheckBox.IsChecked ?? false;

            Properties.Settings.Default.Save();

            StatusTextBlock.Text = "设置已自动保存";

            // 检查是否启用检测，启用时重新启动检测；否则停止检测
            if (EnableCheckBox.IsChecked == true)
            {
                StartMonitoring();
                Debug.WriteLine("检测已根据新设置重新启动。");
            }
            else
            {
                StopMonitoring();
                Debug.WriteLine("检测已关闭。");
            }
        }

        private bool ValidateControls()
        {
            if (isLoadingSettings) return true; // 初始化阶段直接跳过验证

            Debug.WriteLine("验证控件...");
            bool isValid = true;

            // 示例验证逻辑
            if (string.IsNullOrWhiteSpace(GatewayAddressTextBox.Text))
            {
                Debug.WriteLine("网关地址为空或无效");
                MessageBox.Show("网关地址为空或无效");
                // 将数值改为更改前
                GatewayAddressTextBox.Text = Properties.Settings.Default.GatewayAddress;
                isValid = false;
            }

            if (!int.TryParse(CheckIntervalTextBox.Text, out _))
            {
                Debug.WriteLine("检查时间间隔不是有效整数");
                MessageBox.Show("检查时间间隔不是有效整数");
                // 将数值改为更改前
                CheckIntervalTextBox.Text = Properties.Settings.Default.CheckInterval.ToString();
                isValid = false;
            }

            if (!int.TryParse(ShutdownThresholdTextBox.Text, out _))
            {
                Debug.WriteLine("关机阈值不是有效整数");
                MessageBox.Show("关机阈值不是有效整数");
                // 将数值改为更改前
                ShutdownThresholdTextBox.Text = Properties.Settings.Default.ShutdownThreshold.ToString();
                isValid = false;
            }

            return isValid;
        }

        private bool TryParseInt(string text, out int result)
        {
            Debug.WriteLine("尝试解析Int");
            if (int.TryParse(text, out result)) return true;
            Debug.WriteLine("请输入有效的整数");
            MessageBox.Show("请输入有效的整数");
            return false;
        }

        private void StartMonitoring()
        {
            lock (_lockObject)
            {
                StopMonitoring(); // 确保在启动新检测前停止先前的检测

                ResetFlags();

                if (!EnableCheckBox.IsChecked == true || !ValidateControls()) return;

                string gatewayAddress = GatewayAddressTextBox.Text;

                if (!TryParseInt(CheckIntervalTextBox.Text, out int checkInterval) ||
                    !TryParseInt(ShutdownThresholdTextBox.Text, out int shutdownThreshold)) return;

                _timer = new Timer(CheckGateway, new object[] { gatewayAddress, shutdownThreshold }, 0, checkInterval);
                UpdateUI("检测中...", "检测已启动");
            }
        }

        private void StopMonitoring()
        {
            lock (_lockObject)
            {
                if (_timer != null)
                {
                    _timer.Dispose();
                    _timer = null;
                    Debug.WriteLine("检测已停止。");
                }
            }
        }

        private void ResetFlags()
        {
            _shutdownTriggered = false;
            _countdownInProgress = false;
        }

        private void UpdateUI(string status, string logMessage)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StatusTextBlock.Text = status;
                Log(logMessage);
            }));
        }

        private void CheckGateway(object state)
        {
            var parameters = (object[])state;
            string gatewayAddress = (string)parameters[0];
            int shutdownThreshold = (int)parameters[1];

            using (Ping ping = new Ping())
            {
                try
                {
                    PingReply reply = ping.Send(gatewayAddress, 1000);
                    if (reply.Status == IPStatus.Success)
                    {
                        HandleGatewayReachable(gatewayAddress, reply);
                    }
                    else
                    {
                        HandlePowerOutage(gatewayAddress, shutdownThreshold);
                    }
                }
                catch (Exception ex)
                {
                    HandlePowerOutage(gatewayAddress, shutdownThreshold);
                    Log($"Ping 失败: {ex.Message}");
                }
            }
        }

        private void HandleGatewayReachable(string gatewayAddress, PingReply reply)
        {
            lock (_lockObject)
            {
                _powerOutageStartTime = null;
            }
            UpdateUI($"网关 {gatewayAddress} 可达，延迟 {reply.RoundtripTime} 毫秒", $"网关 {gatewayAddress} 可达");
        }

        private void HandlePowerOutage(string gatewayAddress, int shutdownThreshold)
        {
            lock (_lockObject)
            {
                if (_shutdownTriggered || _countdownInProgress) return;

                if (_powerOutageStartTime == null)
                {
                    _powerOutageStartTime = DateTime.Now;
                    UpdateUI("网关不可达", $"网关 {gatewayAddress} 不可达");
                }
                else if ((DateTime.Now - _powerOutageStartTime.Value).TotalMilliseconds > shutdownThreshold)
                {
                    _shutdownTriggered = true;
                    _timer?.Dispose();
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateUI("关机中...", "关机中...");
                        if (DebugCheckBox.IsChecked == true)
                        {
                            MessageBox.Show("已关机（调试模式）");
                        }
                        else
                        {
                            ShowShutdownCountdown();
                        }
                    }));
                }
            }
        }

        private void ShowShutdownCountdown()
        {
            lock (_lockObject)
            {
                if (_countdownInProgress) return;
                _countdownInProgress = true;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var countdownWindow = new ShutdownCountdownWindow(30);
                countdownWindow.CountdownCancelled += CountdownWindow_CountdownCancelled;
                countdownWindow.Closed += (s, e) =>
                {
                    lock (_lockObject)
                    {
                        _countdownInProgress = false;
                        if (!_shutdownTriggered) UpdateUI("关机已取消", "关机已取消");
                    }
                };

                if (countdownWindow.ShowDialog() == true && !countdownWindow.IsCancelled)
                {
                    ShutdownComputer();
                }
            }));
        }

        private void CountdownWindow_CountdownCancelled(object sender, EventArgs e)
        {
            lock (_lockObject)
            {
                _countdownInProgress = false;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    EnableCheckBox.IsChecked = false;
                    UpdateUI("检测已禁用", "检测已禁用");
                }));
            }
        }

        private void ShutdownComputer()
        {
            Log("即将执行关机命令");
            Process.Start(new ProcessStartInfo("shutdown", "/s /t 0")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        private void Log(string message)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LogTextBox.AppendText($"{DateTime.Now}: {message}{Environment.NewLine}");
                LogTextBox.ScrollToEnd();
            }));
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            StartMonitoring();
        }
    }

    // Extension method for TextBlock text update
    public static class UIExtensions
    {
        public static void SetText(this TextBlock textBlock, string text)
        {
            if (textBlock != null) textBlock.Text = text;
        }
    }
}
