using System;
using System.IO;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Net.NetworkInformation;
using System.Diagnostics;
using IWshRuntimeLibrary;
using Microsoft.Win32;
using Timer = System.Threading.Timer;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace UtilityApp
{
    public partial class MainWindow : Window
    {
        // 定时器，用于定时检测网关
        private Timer _timer;
        // 记录停电开始时间
        private DateTime? _powerOutageStartTime;
        // 标志是否已触发关机
        private bool _shutdownTriggered;
        // 标志是否正在进行关机倒计时
        private bool _countdownInProgress;
        // 锁对象，用于线程同步
        private readonly object _lockObject = new object();
        // 系统托盘图标
        private NotifyIcon _notifyIcon;
        // 标志是否正在加载设置
        private bool isLoadingSettings = true;
        // 注册表项路径
        private const string AutoStartRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        // 应用程序名称
        private const string AppName = "UtilityApp";
        // 启动文件夹路径
        private readonly string startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);

        /// <summary>
        /// 构造主窗口
        /// </summary>
        public MainWindow()
        {
            // 初始化组件
            InitializeComponent();
            // 标志设置为 true，防止初始化过程中的事件触发
            isLoadingSettings = true;
            // 绑定窗口加载事件
            this.Loaded += MainWindow_Loaded;
            // 初始化系统托盘图标
            InitializeNotifyIcon();
        }

        /// <summary>
        /// 窗口加载事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 加载设置
            LoadSettings();
            // 标志设置为 false，以便正常处理事件
            isLoadingSettings = false;
            // 根据加载的设置决定是否启动检测
            if (EnableCheckBox.IsChecked == true) StartMonitoring();
        }

        /// <summary>
        /// 初始化托盘图标
        /// </summary>
        private void InitializeNotifyIcon()
        {
            // 创建并设置托盘菜单及图标
            _notifyIcon = new NotifyIcon
            {
                // 设置托盘图标
                Icon = new Icon("app.ico"),
                // 设置图标可见
                Visible = true,
                // 设置文本
                Text = AppName,
                // 设置上下文菜单
                ContextMenuStrip = new ContextMenuStrip
                {
                    // 添加并绑定菜单项
                    Items = {
                        new ToolStripMenuItem("显示", null, (s, e) => ShowMainWindow()),
                        new ToolStripMenuItem("退出", null, (s, e) => ExitApplication())
                    }
                }
            };
            // 单击显示主窗口
            _notifyIcon.Click += (s, e) => ShowMainWindow();
        }

        /// <summary>
        /// 退出应用程序
        /// </summary>
        private void ExitApplication()
        {
            // 隐藏托盘图标
            _notifyIcon.Visible = false;
            // 关闭应用程序
            Application.Current.Shutdown();
        }

        /// <summary>
        /// 显示主窗口
        /// </summary>
        private void ShowMainWindow()
        {
            // 显示窗口
            Show();
            // 将窗口状态设置为正常
            WindowState = WindowState.Normal;
            // 激活窗口
            Activate();
        }

        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        /// <param name="e">事件参数</param>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 取消关闭事件
            e.Cancel = true;
            // 隐藏窗口
            Hide();
            // 调用基类的 OnClosing 方法
            base.OnClosing(e);
        }

        /// <summary>
        /// 文本框内容更改事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void AutoSave_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 防止初始化时事件触发
            if (isLoadingSettings) return;
            // 自动保存设置
            SaveSettings();
        }

        /// <summary>
        /// 复选框选中事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void AutoSave_Checked(object sender, RoutedEventArgs e)
        {
            if (isLoadingSettings) return;
            SaveSettings();
        }

        /// <summary>
        /// 开机自启复选框选中事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void AutoStart_Checked(object sender, RoutedEventArgs e)
        {
            if (isLoadingSettings) return;
            SetAutoStart(true);
        }

        /// <summary>
        /// 开机自启复选框取消选中事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void AutoStart_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isLoadingSettings) return;
            SetAutoStart(false);
        }

        /// <summary>
        /// 加载应用程序设置并更新 UI
        /// </summary>
        private void LoadSettings()
        {
            isLoadingSettings = true;

            // 加载设置到组件
            GatewayAddressTextBox.Text = Properties.Settings.Default.GatewayAddress;
            CheckIntervalTextBox.Text = Properties.Settings.Default.CheckInterval.ToString();
            ShutdownThresholdTextBox.Text = Properties.Settings.Default.ShutdownThreshold.ToString();
            EnableCheckBox.IsChecked = Properties.Settings.Default.EnableCheck;
            DebugCheckBox.IsChecked = Properties.Settings.Default.DebugCheck;
            AutoStartCheckBox.IsChecked = Properties.Settings.Default.AutoStart;
            SetAutoStart(Properties.Settings.Default.AutoStart);

            // 标志设置为 false，以便正常处理事件
            isLoadingSettings = false;

            // 事件绑定
            CheckIntervalTextBox.TextChanged += AutoSave_TextChanged;
            ShutdownThresholdTextBox.TextChanged += AutoSave_TextChanged;
            GatewayAddressTextBox.TextChanged += AutoSave_TextChanged;
            EnableCheckBox.Checked += AutoSave_Checked;
            EnableCheckBox.Unchecked += AutoSave_Checked;
            DebugCheckBox.Checked += AutoSave_Checked;
            DebugCheckBox.Unchecked += AutoSave_Checked;
            AutoStartCheckBox.Checked += AutoStart_CheckedChanged;
            AutoStartCheckBox.Unchecked += AutoStart_CheckedChanged;
        }

        /// <summary>
        /// 保存应用程序设置
        /// </summary>
        private void SaveSettings()
        {
            // 数值验证
            if (!ValidateControls()) return;

            // 保存设置
            Properties.Settings.Default.GatewayAddress = GatewayAddressTextBox.Text;
            Properties.Settings.Default.CheckInterval = int.TryParse(CheckIntervalTextBox.Text, out var interval) ? interval : 10000;
            Properties.Settings.Default.ShutdownThreshold = int.TryParse(ShutdownThresholdTextBox.Text, out var threshold) ? threshold : 60000;
            Properties.Settings.Default.EnableCheck = EnableCheckBox.IsChecked ?? false;
            Properties.Settings.Default.DebugCheck = DebugCheckBox.IsChecked ?? false;
            Properties.Settings.Default.AutoStart = AutoStartCheckBox.IsChecked ?? false;
            Properties.Settings.Default.Save();

            // 更新开机自启设置
            SetAutoStart(Properties.Settings.Default.AutoStart);
            StatusTextBlock.Text = "设置已自动保存";

            // 检查是否启用检测，启用时重新启动检测；否则停止检测
            if (EnableCheckBox.IsChecked == true) StartMonitoring(); else StopMonitoring();
        }

        /// <summary>
        /// 开机自启复选框选中事件处理
        /// </summary>
        private void AutoStart_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!isLoadingSettings) SaveSettings();
        }

        /// <summary>
        /// 设置开机自启并创建/删除快捷方式
        /// </summary>
        /// <param name="enable">是否启用</param>
        private void SetAutoStart(bool enable)
        {
            // 快捷方式路径
            string shortcutPath = Path.Combine(startupFolderPath, $"{AppName}.lnk");

            // 打开注册表项
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AutoStartRegistryKey, true))
            {
                if (key == null) return;

                // 当启用时
                if (enable)
                {
                    // 获取程序路径
                    string appPath = System.IO.Path.Combine(AppContext.BaseDirectory, "UtilityApp.exe");

                    // 检查应用程序是否存在
                    if (System.IO.File.Exists(appPath))
                    {
                        // 将程序路径写入到注册表
                        key.SetValue(AppName, $"\"{appPath}\"");
                        // 创建快捷方式
                        CreateShortcut(shortcutPath, appPath);
                    }
                    else
                    {
                        MessageBox.Show("未找到exe文件，无法设置开机自启。请检查程序路径。");
                    }
                }
                // 当禁用时
                else
                {
                    // 删除注册表项
                    key.DeleteValue(AppName, false);
                    // 删除快捷方式
                    RemoveShortcut(shortcutPath);
                }
            }
        }

        /// <summary>
        /// 创建快捷方式
        /// </summary>
        /// <param name="shortcutPath">快捷方式路径</param>
        /// <param name="appPath">程序路径</param>
        private void CreateShortcut(string shortcutPath, string appPath)
        {
            // 当快捷方式不存在时创建
            if (!System.IO.File.Exists(shortcutPath))
            {
                // 创建 WshShell 对象
                var shell = new WshShell();
                // 创建快捷方式
                IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
                // 设置快捷方式属性
                shortcut.TargetPath = appPath;
                shortcut.WorkingDirectory = AppContext.BaseDirectory;
                shortcut.WindowStyle = 1; // 正常窗口
                shortcut.Description = $"{AppName} - 开机自启";
                // 保存快捷方式
                shortcut.Save();
            }
        }

        /// <summary>
        /// 删除快捷方式
        /// </summary>
        /// <param name="shortcutPath">快捷方式路径</param>
        private void RemoveShortcut(string shortcutPath)
        {
            // 当快捷方式存在时删除
            if (System.IO.File.Exists(shortcutPath))
            {
                // 删除快捷方式
                System.IO.File.Delete(shortcutPath);
            }
        }

        /// <summary>
        /// 数值验证控件
        /// </summary>
        /// <returns>是否有效</returns>
        private bool ValidateControls()
        {
            if (isLoadingSettings) return true; // 初始化阶段直接跳过验证

            bool isValid = true;

            if (string.IsNullOrWhiteSpace(GatewayAddressTextBox.Text))
            {
                MessageBox.Show("网关地址为空或无效");
                // 将数值改为更改前
                GatewayAddressTextBox.Text = Properties.Settings.Default.GatewayAddress;
                isValid = false;
            }
            if (!int.TryParse(CheckIntervalTextBox.Text, out _))
            {
                MessageBox.Show("检查时间间隔不是有效整数");
                // 将数值改为更改前
                CheckIntervalTextBox.Text = Properties.Settings.Default.CheckInterval.ToString();
                isValid = false;
            }
            if (!int.TryParse(ShutdownThresholdTextBox.Text, out _))
            {
                MessageBox.Show("关机阈值不是有效整数");
                // 将数值改为更改前
                ShutdownThresholdTextBox.Text = Properties.Settings.Default.ShutdownThreshold.ToString();
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// 尝试解析整数(计划弃用)
        /// </summary>
        /// <param name="text">输入</param>
        /// <param name="result">输出</param>
        /// <returns></returns>
        private bool TryParseInt(string text, out int result)
        {
            if (int.TryParse(text, out result)) return true;
            MessageBox.Show("请输入有效的整数");
            return false;
        }

        /// <summary>
        /// 启动检测
        /// </summary>
        private void StartMonitoring()
        {
            // 线程同步
            lock (_lockObject)
            {
                // 确保在启动新检测前停止先前的检测
                StopMonitoring();
                // 重置标志
                ResetFlags();

                // 如果未启用或控件验证失败，则直接返回
                if (!EnableCheckBox.IsChecked == true || !ValidateControls()) return;

                // 获取网关地址
                string gatewayAddress = GatewayAddressTextBox.Text;
                // 获取检查间隔和关机阈值
                if (!TryParseInt(CheckIntervalTextBox.Text, out int checkInterval) ||
                    !TryParseInt(ShutdownThresholdTextBox.Text, out int shutdownThreshold)) return;

                // 启动定时器
                _timer = new Timer(CheckGateway, new object[] { gatewayAddress, shutdownThreshold }, 0, checkInterval);
                UpdateUI("检测中...", "检测已启动");
            }
        }

        /// <summary>
        /// 停止检测
        /// </summary>
        private void StopMonitoring()
        {
            // 线程同步
            lock (_lockObject)
            {
                // 如果定时器不为空，则停止检测
                if (_timer != null)
                {
                    // 释放定时器
                    _timer.Dispose();
                    // 置空定时器
                    _timer = null;
                }
            }
        }

        /// <summary>
        /// 重置Flags
        /// </summary>
        private void ResetFlags()
        {
            _shutdownTriggered = false;
            _countdownInProgress = false;
        }

        /// <summary>
        /// 更新 UI
        /// </summary>
        /// <param name="status">当前状态</param>
        /// <param name="logMessage">日志信息</param>
        private void UpdateUI(string status, string logMessage)
        {
            // 更新状态文本和日志
            Dispatcher.BeginInvoke(() =>
            {
                StatusTextBlock.Text = status;
                Log(logMessage);
            });
        }

        /// <summary>
        /// 检查网关可通性
        /// </summary>
        /// <param name="state">状态</param>
        private void CheckGateway(object state)
        {
            // 获取参数
            var parameters = (object[])state;
            string gatewayAddress = (string)parameters[0];
            int shutdownThreshold = (int)parameters[1];

            // 使用 Ping 类检查网关可达性
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

        /// <summary>
        /// 处理可达网关
        /// </summary>
        /// <param name="gatewayAddress">网关地址</param>
        /// <param name="reply">回应</param>
        private void HandleGatewayReachable(string gatewayAddress, PingReply reply)
        {
            // 线程同步，重置停电开始时间
            lock (_lockObject) { _powerOutageStartTime = null; }
            // 更新 UI
            UpdateUI($"网关 {gatewayAddress} 可达", $"网关 {gatewayAddress} 可达，延迟 {reply.RoundtripTime} 毫秒");
        }

        /// <summary>
        /// 处理停电事件
        /// </summary>
        /// <param name="gatewayAddress"></param>
        /// <param name="shutdownThreshold"></param>
        private void HandlePowerOutage(string gatewayAddress, int shutdownThreshold)
        {
            // 线程同步
            lock (_lockObject)
            {
                // 如果已触发关机或正在进行关机倒计时，则直接返回
                if (_shutdownTriggered || _countdownInProgress) return;

                // 如果停电开始时间为空
                if (_powerOutageStartTime == null)
                {
                    // 记录停电开始时间
                    _powerOutageStartTime = DateTime.Now;
                    // 更新 UI
                    UpdateUI("网关不可达", $"网关 {gatewayAddress} 不可达");
                }
                // 不为空且停电时间超过关机阈值
                else if ((DateTime.Now - _powerOutageStartTime.Value).TotalMilliseconds > shutdownThreshold)
                {
                    // 触发关机
                    _shutdownTriggered = true;
                    // 停止检测
                    _timer?.Dispose();
                    // 更新 UI
                    Dispatcher.BeginInvoke(() =>
                    {
                        // 执行关机逻辑
                        UpdateUI("关机中...", "关机中...");
                        if (DebugCheckBox.IsChecked == true) MessageBox.Show("已关机（调试模式）");
                        else ShowShutdownCountdown();
                    });
                }
            }
        }

        /// <summary>
        /// 显示关机倒计时窗口
        /// </summary>
        private void ShowShutdownCountdown()
        {
            lock (_lockObject)
            {
                // 如果已经在进行关机倒计时，则直接返回
                if (_countdownInProgress) return;
                // 倒计时进行中标志为true
                _countdownInProgress = true;
            }

            // 显示关机倒计时窗口
            Dispatcher.BeginInvoke(() =>
            {
                // 创建关机倒计时窗口
                var countdownWindow = new ShutdownCountdownWindow(120);
                // 订阅事件
                countdownWindow.CountdownCancelled += CountdownWindow_CountdownCancelled;
                // 订阅窗口关闭事件
                countdownWindow.Closed += (s, e) =>
                {
                    lock (_lockObject)
                    {
                        // 倒计时进行中标志为false
                        _countdownInProgress = false;
                        // 如果取消了关机，则更新UI
                        if (!_shutdownTriggered) UpdateUI("关机已取消", "关机已取消");
                    }
                };

                // 显示窗口, 如果返回值为true且未取消，则执行关机
                if (countdownWindow.ShowDialog() == true && !countdownWindow.IsCancelled)
                {
                    // 执行关机
                    ShutdownComputer();
                }
            });
        }

        // 关机倒计时窗口取消事件处理
        private void CountdownWindow_CountdownCancelled(object sender, EventArgs e)
        {
            lock (_lockObject)
            {
                // 倒计时进行中标志为false
                _countdownInProgress = false;
                // 更新UI
                Dispatcher.BeginInvoke(() =>
                {
                    EnableCheckBox.IsChecked = false;
                    UpdateUI("检测已禁用", "检测已禁用");
                });
            }
        }

        // 执行关机命令
        private void ShutdownComputer()
        {
            Log("即将执行关机命令");
            // 执行关机命令
            Process.Start(new ProcessStartInfo("shutdown", "/s /t 0") { CreateNoWindow = true, UseShellExecute = false });
        }

        // 记录日志
        private void Log(string message)
        {
            // 更新UI
            Dispatcher.BeginInvoke(() =>
            {
                LogTextBox.AppendText($"{DateTime.Now}: {message}{Environment.NewLine}");
                LogTextBox.ScrollToEnd();
            });
        }

        // 应用按钮点击事件(弃用)
        //private void ApplyButton_Click(object sender, RoutedEventArgs e)
        //{
        //    SaveSettings();
        //}
    }
}