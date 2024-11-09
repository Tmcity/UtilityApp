using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace UtilityApp
{
    public partial class MainWindow : Window
    {
        private Timer _timer;
        private DateTime? _powerOutageStartTime;
        private bool _shutdownTriggered;
        private bool _countdownInProgress;
        private readonly object _lockObject = new object();

        public MainWindow()
        {
            InitializeComponent();
            InitializeSettings();
            StartMonitoring();
        }

        private void AutoSave_TextChanged(object sender, TextChangedEventArgs e) => SaveSettings();
        private void AutoSave_Checked(object sender, RoutedEventArgs e) => SaveSettings();

        private void InitializeSettings()
        {
            GatewayAddressTextBox.Text = Properties.Settings.Default.GatewayAddress;
            CheckIntervalTextBox.Text = Properties.Settings.Default.CheckInterval.ToString();
            ShutdownThresholdTextBox.Text = Properties.Settings.Default.ShutdownThreshold.ToString();
            EnableCheckBox.IsChecked = Properties.Settings.Default.EnableCheck;
            DebugCheckBox.IsChecked = Properties.Settings.Default.DebugCheck;
            StatusTextBlock?.SetText("未开始");
        }

        private void SaveSettings()
        {
            if (!ValidateControls()) return;

            Properties.Settings.Default.GatewayAddress = GatewayAddressTextBox.Text;
            if (!TryParseInt(CheckIntervalTextBox.Text, out int checkInterval) ||
                !TryParseInt(ShutdownThresholdTextBox.Text, out int shutdownThreshold)) return;

            Properties.Settings.Default.CheckInterval = checkInterval;
            Properties.Settings.Default.ShutdownThreshold = shutdownThreshold;
            Properties.Settings.Default.EnableCheck = EnableCheckBox.IsChecked ?? false;
            Properties.Settings.Default.DebugCheck = DebugCheckBox.IsChecked ?? false;

            Properties.Settings.Default.Save();

            StatusTextBlock.Text = "设置已自动保存";
            StartMonitoring();
        }

        private bool ValidateControls()
        {
            return GatewayAddressTextBox != null && CheckIntervalTextBox != null &&
                   ShutdownThresholdTextBox != null && EnableCheckBox != null &&
                   DebugCheckBox != null && StatusTextBlock != null;
        }

        private bool TryParseInt(string text, out int result)
        {
            if (int.TryParse(text, out result)) return true;
            MessageBox.Show("请输入有效的整数");
            return false;
        }

        private void StartMonitoring()
        {
            lock (_lockObject)
            {
                ResetFlags();

                if (!EnableCheckBox.IsChecked == true || !ValidateControls()) return;

                string gatewayAddress = GatewayAddressTextBox.Text;
                if (!TryParseInt(CheckIntervalTextBox.Text, out int checkInterval) ||
                    !TryParseInt(ShutdownThresholdTextBox.Text, out int shutdownThreshold)) return;

                _timer?.Dispose();
                _timer = new Timer(CheckGateway, new object[] { gatewayAddress, shutdownThreshold }, 0, checkInterval);
                UpdateUI("检测中...", "检测已启动");
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
