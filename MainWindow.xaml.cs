using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows;

namespace UtilityApp
{
    public partial class MainWindow : Window
    {
        private Timer _timer;
        private DateTime? _powerOutageStartTime;

        public MainWindow()
        {
            InitializeComponent();
            StartMonitoring();
        }

        private void StartMonitoring()
        {
            if (EnableCheckBox.IsChecked == true)
            {
                string gatewayAddress = GatewayAddressTextBox.Text;
                if (!int.TryParse(CheckIntervalTextBox.Text, out int checkInterval))
                {
                    MessageBox.Show("检查间隔必须是一个有效的整数。");
                    return;
                }
                if (!int.TryParse(ShutdownThresholdTextBox.Text, out int shutdownThreshold))
                {
                    MessageBox.Show("断电时间阈值必须是一个有效的整数。");
                    return;
                }

                _timer?.Dispose();
                _timer = new Timer(CheckGateway, new object[] { gatewayAddress, shutdownThreshold }, 0, checkInterval);
                StatusTextBlock.Text = "检测中...";
                Log("检测已启动");
            }
            else
            {
                StatusTextBlock.Text = "检测已禁用";
                _timer?.Dispose();
                Log("检测已禁用");
            }
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
                    PingReply reply = ping.Send(gatewayAddress);
                    if (reply.Status == IPStatus.Success)
                    {
                        _powerOutageStartTime = null; // 网关可达，重置断电开始时间
                        Dispatcher.Invoke(() => StatusTextBlock.Text = "网关可达");
                        Log($"网关 {gatewayAddress} 可达，延迟 {reply.RoundtripTime} 毫秒");
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

        private void HandlePowerOutage(string gatewayAddress, int shutdownThreshold)
        {
            if (_powerOutageStartTime == null)
            {
                _powerOutageStartTime = DateTime.Now; // 记录断电开始时间
                Dispatcher.Invoke(() => StatusTextBlock.Text = "网关不可达");
                Log($"网关 {gatewayAddress} 不可达");
            }
            else if ((DateTime.Now - _powerOutageStartTime.Value).TotalMilliseconds > shutdownThreshold)
            {
                Dispatcher.Invoke(() => StatusTextBlock.Text = "关机中...");
                Log("关机中...");
                Dispatcher.Invoke(() =>
                {
                    if (DebugCheckBox.IsChecked == true)
                    {
                        MessageBox.Show("已关机（调试模式）");
                    }
                    else
                    {
                        ShutdownComputer();
                    }
                });
            }
        }

        private void ShutdownComputer()
        {
            Process.Start(new ProcessStartInfo("shutdown", "/s /t 0")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        private void Log(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"{DateTime.Now}: {message}{Environment.NewLine}");
                LogTextBox.ScrollToEnd();
            });
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            StartMonitoring();
        }
    }
}
