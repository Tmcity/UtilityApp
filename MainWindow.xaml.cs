using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Net.NetworkInformation;
using System.Diagnostics;
using System.Windows.Input;
using IWshRuntimeLibrary;
using Microsoft.Win32;
using Timer = System.Threading.Timer;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;
using System.Threading.Tasks;
using System.Linq;  // 添加 LINQ 支持

namespace PowerGuard
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
        private const string AppName = "PowerGuard";

        // 启动文件夹路径
        private readonly string startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);

        // 委托，用于自动保存设置
        private delegate void AutoSaveAction();

        // 延迟保存定时器
        private Timer _saveDelayTimer;
        
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
            if (EnableCheckBox.IsChecked == true) 
            {
                StartMonitoring();
            }
        }

        /// <summary>
        /// 初始化托盘图标
        /// </summary>
        private void InitializeNotifyIcon()
        {
            try
            {
                // 创建并设置托盘菜单及图标
                _notifyIcon = new NotifyIcon
                {
                    // 设置托盘图标 - 使用内嵌图标作为备用
                    Icon = GetApplicationIcon(),
                    // 设置图标可见
                    Visible = true,
                    // 设置文本
                    Text = AppName,
                    // 设置上下文菜单
                    ContextMenuStrip = new ContextMenuStrip
                    {
                        // 添加并绑定菜单项
                        Items =
                        {
                            new ToolStripMenuItem("显示", null, (s, e) => ShowMainWindow()),
                            new ToolStripMenuItem("退出", null, (s, e) => ExitApplication())
                        }
                    }
                };
                // 单击显示主窗口
                _notifyIcon.Click += (s, e) => ShowMainWindow();
            }
            catch (Exception ex)
            {
                // 如果托盘图标初始化失败，记录错误但不阻止应用启动
                Log($"托盘图标初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取应用程序图标，如果文件不存在则使用默认图标
        /// </summary>
        /// <returns></returns>
        private Icon GetApplicationIcon()
        {
            try
            {
                string iconPath = "app.ico";
                if (System.IO.File.Exists(iconPath))  // 使用完全限定名
                {
                    return new Icon(iconPath);
                }
            }
            catch (Exception ex)
            {
                Log($"无法加载图标文件: {ex.Message}");
            }
            
            // 使用系统默认应用程序图标作为备用
            return SystemIcons.Application;
        }

        /// <summary>
        /// 退出应用程序
        /// </summary>
        private void ExitApplication()
        {
            try
            {
                // 停止检测
                StopMonitoring();
                // 隐藏托盘图标
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                // 关闭应用程序
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Log($"退出应用程序时出错: {ex.Message}");
                // 强制退出
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// 显示主窗口
        /// </summary>
        private void ShowMainWindow()
        {
            try
            {
                // 显示窗口
                Show();
                // 将窗口状态设置为正常
                WindowState = WindowState.Normal;
                // 激活窗口
                Activate();
                // 将窗口移到前台
                Topmost = true;
                Topmost = false;
            }
            catch (Exception ex)
            {
                Log($"显示主窗口时出错: {ex.Message}");
            }
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
        private void AutoSave_TextChanged(object sender, TextChangedEventArgs e) => ExecuteAutoSaveAction(SaveSettings);

        /// <summary>
        /// 复选框选中事件处理
        /// </summary>
        private void AutoSave_Checked(object sender, RoutedEventArgs e) => ExecuteAutoSaveAction(SaveSettings);
        
        /// <summary>
        /// 执行自动保存操作
        /// </summary>
        /// <param name="action">自动保存操作</param>
        private void ExecuteAutoSaveAction(AutoSaveAction action)
        {
            if (isLoadingSettings) return;
            
            // 取消之前的延迟保存
            _saveDelayTimer?.Dispose();
            
            // 设置新的延迟保存（1秒后保存）
            _saveDelayTimer = new Timer(_ =>
            {
                try
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception ex)
                        {
                            Log($"延迟保存设置时出错: {ex.Message}");
                            StatusTextBlock.Text = "保存设置失败";
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"延迟保存定时器出错: {ex.Message}");
                }
                finally
                {
                    _saveDelayTimer?.Dispose();
                    _saveDelayTimer = null;
                }
            }, null, 1000, System.Threading.Timeout.Infinite); // 1秒延迟，只执行一次
        }

        /// <summary>
        /// 加载应用程序设置并更新 UI
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                // 标志设置为 true，防止初始化过程中的事件触发
                isLoadingSettings = true;

                // 加载设置到组件
                GatewayAddressTextBox.Text = Properties.Settings.Default.GatewayAddress;
                CheckIntervalTextBox.Text = Properties.Settings.Default.CheckInterval.ToString();
                ShutdownThresholdTextBox.Text = Properties.Settings.Default.ShutdownThreshold.ToString();
                ShutdownCountdownTextBox.Text = Properties.Settings.Default.ShutdownCountdown.ToString();
                EnableCheckBox.IsChecked = Properties.Settings.Default.EnableCheck;
                DebugCheckBox.IsChecked = Properties.Settings.Default.DebugCheck;
                AutoStartCheckBox.IsChecked = Properties.Settings.Default.AutoStart;

                // 标志设置为 false，以便正常处理事件
                isLoadingSettings = false;

                // 事件绑定
                CheckIntervalTextBox.TextChanged += AutoSave_TextChanged;
                ShutdownThresholdTextBox.TextChanged += AutoSave_TextChanged;
                ShutdownCountdownTextBox.TextChanged += AutoSave_TextChanged;
                GatewayAddressTextBox.TextChanged += AutoSave_TextChanged;
                EnableCheckBox.Checked += AutoSave_Checked;
                EnableCheckBox.Unchecked += AutoSave_Checked;
                DebugCheckBox.Checked += AutoSave_Checked;
                DebugCheckBox.Unchecked += AutoSave_Checked;
                AutoStartCheckBox.Checked += AutoStart_CheckedChanged;
                AutoStartCheckBox.Unchecked += AutoStart_CheckedChanged;
                
                Log("设置加载完成");
            }
            catch (Exception ex)
            {
                Log($"加载设置时出错: {ex.Message}");
                MessageBox.Show($"加载设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 保存应用程序设置
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                // 静默验证：尝试解析数值，如果失败则使用默认值，不弹出错误框
                if (!SilentValidateAndFix()) return;

                // 保存设置（界面显示秒，内部存储秒，使用时转换为毫秒）
                Properties.Settings.Default.GatewayAddress = string.IsNullOrWhiteSpace(GatewayAddressTextBox.Text) ? "192.168.1.1" : GatewayAddressTextBox.Text.Trim();
                Properties.Settings.Default.CheckInterval = int.TryParse(CheckIntervalTextBox.Text, out var interval) && interval > 0 ? interval : 10;
                Properties.Settings.Default.ShutdownThreshold = int.TryParse(ShutdownThresholdTextBox.Text, out var threshold) && threshold > 0 ? threshold : 60;
                Properties.Settings.Default.ShutdownCountdown = int.TryParse(ShutdownCountdownTextBox.Text, out var countdown) && countdown > 0 ? countdown : 120;
                Properties.Settings.Default.EnableCheck = EnableCheckBox.IsChecked ?? false;
                Properties.Settings.Default.DebugCheck = DebugCheckBox.IsChecked ?? false;
                Properties.Settings.Default.AutoStart = AutoStartCheckBox.IsChecked ?? false;
                Properties.Settings.Default.Save();

                // 更新开机自启设置
                SetAutoStart(Properties.Settings.Default.AutoStart);
                StatusTextBlock.Text = "设置已自动保存";

                // 检查是否启用检测，启用时重新启动检测；否则停止检测
                if (EnableCheckBox.IsChecked == true) 
                {
                    StartMonitoring();
                } 
                else 
                {
                    StopMonitoring();
                }
            }
            catch (Exception ex)
            {
                Log($"保存设置时出错: {ex.Message}");
                StatusTextBlock.Text = "保存设置失败";
            }
        }

        /// <summary>
        /// 静默验证和修复数值，现在改为弹窗提醒
        /// </summary>
        /// <returns>是否需要继续保存</returns>
        private bool SilentValidateAndFix()
        {
            if (isLoadingSettings) return true;

            // 网关地址验证
            if (string.IsNullOrWhiteSpace(GatewayAddressTextBox.Text))
            {
                MessageBox.Show("网关地址不能为空", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                GatewayAddressTextBox.Text = Properties.Settings.Default.GatewayAddress;
                return false;
            }

            // 检查间隔验证 - 带弹窗提醒
            if (int.TryParse(CheckIntervalTextBox.Text, out int interval))
            {
                if (interval < 1)
                {
                    MessageBox.Show("检查时间间隔不能小于1秒", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CheckIntervalTextBox.Text = "1";
                    return false;
                }
                else if (interval > 3600)
                {
                    MessageBox.Show("检查时间间隔不能超过3600秒（1小时）", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CheckIntervalTextBox.Text = "3600";
                    return false;
                }
            }
            else if (!string.IsNullOrEmpty(CheckIntervalTextBox.Text))
            {
                MessageBox.Show("检查时间间隔必须是有效的整数（秒）", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                CheckIntervalTextBox.Text = Properties.Settings.Default.CheckInterval.ToString();
                return false;
            }

            // 停电容忍时间验证 - 带弹窗提醒
            if (int.TryParse(ShutdownThresholdTextBox.Text, out int threshold))
            {
                if (threshold < 5)
                {
                    MessageBox.Show("停电容忍时间不能小于5秒", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShutdownThresholdTextBox.Text = "5";
                    return false;
                }
                else if (threshold > 7200)
                {
                    MessageBox.Show("停电容忍时间不能超过7200秒（2小时）", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShutdownThresholdTextBox.Text = "7200";
                    return false;
                }
            }
            else if (!string.IsNullOrEmpty(ShutdownThresholdTextBox.Text))
            {
                MessageBox.Show("停电容忍时间必须是有效的整数（秒）", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                ShutdownThresholdTextBox.Text = Properties.Settings.Default.ShutdownThreshold.ToString();
                return false;
            }

            // 关机倒计时验证 - 带弹窗提醒
            if (int.TryParse(ShutdownCountdownTextBox.Text, out int countdown))
            {
                if (countdown < 10)
                {
                    MessageBox.Show("关机倒计时时长不能小于10秒", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShutdownCountdownTextBox.Text = "10";
                    return false;
                }
                else if (countdown > 600)
                {
                    MessageBox.Show("关机倒计时时长不能超过600秒（10分钟）", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShutdownCountdownTextBox.Text = "600";
                    return false;
                }
            }
            else if (!string.IsNullOrEmpty(ShutdownCountdownTextBox.Text))
            {
                MessageBox.Show("关机倒计时时长必须是有效的整数（秒）", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                ShutdownCountdownTextBox.Text = Properties.Settings.Default.ShutdownCountdown.ToString();
                return false;
            }

            return true;
        }

        /// <summary>
        /// 开机自启复选框选中事件处理
        /// </summary>
        private void AutoStart_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!isLoadingSettings) 
            {
                SaveSettings();
            }
        }

        /// <summary>
        /// 设置开机自启并创建/删除快捷方式
        /// </summary>
        /// <param name="enable">是否启用</param>
        private void SetAutoStart(bool enable)
        {
            try
            {
                // 快捷方式路径
                string shortcutPath = Path.Combine(startupFolderPath, $"{AppName}.lnk");

                // 打开注册表项
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(AutoStartRegistryKey, true))
                {
                    if (key == null) 
                    {
                        Log("无法打开注册表项");
                        return;
                    }

                    // 当启用时
                    if (enable)
                    {
                        // 获取程序路径
                        string appPath = Path.Combine(AppContext.BaseDirectory, "PowerGuard.exe");

                        // 检查应用程序是否存在
                        if (System.IO.File.Exists(appPath))  // 使用完全限定名
                        {
                            // 将程序路径写入到注册表
                            key.SetValue(AppName, $"\"{appPath}\"");
                            // 创建快捷方式
                            CreateShortcut(shortcutPath, appPath);
                            Log("开机自启已启用");
                        }
                        else
                        {
                            Log($"未找到exe文件: {appPath}");
                            MessageBox.Show("未找到exe文件，无法设置开机自启。请检查程序路径。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    // 当禁用时
                    else
                    {
                        // 删除注册表项
                        key.DeleteValue(AppName, false);
                        // 删除快捷方式
                        RemoveShortcut(shortcutPath);
                        Log("开机自启已禁用");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"设置开机自启时出错: {ex.Message}");
                MessageBox.Show($"设置开机自启失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 创建快捷方式
        /// </summary>
        /// <param name="shortcutPath">快捷方式路径</param>
        /// <param name="appPath">程序路径</param>
        private void CreateShortcut(string shortcutPath, string appPath)
        {
            try
            {
                // 当快捷方式不存在时创建
                if (!System.IO.File.Exists(shortcutPath))  // 使用完全限定名
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
            catch (Exception ex)
            {
                Log($"创建快捷方式失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 删除快捷方式
        /// </summary>
        /// <param name="shortcutPath">快捷方式路径</param>
        private void RemoveShortcut(string shortcutPath)
        {
            try
            {
                // 当快捷方式存在时删除
                if (System.IO.File.Exists(shortcutPath))  // 使用完全限定名
                {
                    // 删除快捷方式
                    System.IO.File.Delete(shortcutPath);  // 使用完全限定名
                }
            }
            catch (Exception ex)
            {
                Log($"删除快捷方式失败: {ex.Message}");
                throw;
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

            // 验证网关地址
            if (string.IsNullOrWhiteSpace(GatewayAddressTextBox.Text))
            {
                MessageBox.Show("网关地址不能为空", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                GatewayAddressTextBox.Text = Properties.Settings.Default.GatewayAddress;
                isValid = false;
            }

            // 验证检查间隔（现在是秒）- 更宽松的验证
            if (int.TryParse(CheckIntervalTextBox.Text, out int interval))
            {
                if (interval < 1)
                {
                    MessageBox.Show("检查时间间隔不能小于1秒", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CheckIntervalTextBox.Text = "1";
                    isValid = false;
                }
                else if (interval > 3600) // 最大1小时
                {
                    MessageBox.Show("检查时间间隔不能超过3600秒（1小时）", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CheckIntervalTextBox.Text = "3600";
                    isValid = false;
                }
            }
            else if (!string.IsNullOrEmpty(CheckIntervalTextBox.Text))
            {
                // 只有在输入了非空内容且不是有效数字时才提示错误
                MessageBox.Show("检查时间间隔必须是有效的整数（秒）", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                CheckIntervalTextBox.Text = Properties.Settings.Default.CheckInterval.ToString();
                isValid = false;
            }

            // 验证停电容忍时间（现在是秒）- 更宽松的验证
            if (int.TryParse(ShutdownThresholdTextBox.Text, out int threshold))
            {
                if (threshold < 5)
                {
                    MessageBox.Show("停电容忍时间不能小于5秒", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShutdownThresholdTextBox.Text = "5";
                    isValid = false;
                }
                else if (threshold > 7200) // 最大2小时
                {
                    MessageBox.Show("停电容忍时间不能超过7200秒（2小时）", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShutdownThresholdTextBox.Text = "7200";
                    isValid = false;
                }
            }
            else if (!string.IsNullOrEmpty(ShutdownThresholdTextBox.Text))
            {
                MessageBox.Show("停电容忍时间必须是有效的整数（秒）", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                ShutdownThresholdTextBox.Text = Properties.Settings.Default.ShutdownThreshold.ToString();
                isValid = false;
            }

            // 验证关机倒计时时长（秒）- 更宽松的验证
            if (int.TryParse(ShutdownCountdownTextBox.Text, out int countdown))
            {
                if (countdown < 10)
                {
                    MessageBox.Show("关机倒计时时长不能小于10秒", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShutdownCountdownTextBox.Text = "10";
                    isValid = false;
                }
                else if (countdown > 600)
                {
                    MessageBox.Show("关机倒计时时长不能超过600秒（10分钟）", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShutdownCountdownTextBox.Text = "600";
                    isValid = false;
                }
            }
            else if (!string.IsNullOrEmpty(ShutdownCountdownTextBox.Text))
            {
                MessageBox.Show("关机倒计时时长必须是有效的整数（秒）", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                ShutdownCountdownTextBox.Text = Properties.Settings.Default.ShutdownCountdown.ToString();
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// 启动检测
        /// </summary>
        private void StartMonitoring()
        {
            // 线程同步
            lock (_lockObject)
            {
                try
                {
                    // 确保在启动新检测前停止先前的检测
                    StopMonitoring();
                    // 重置标志
                    ResetFlags();

                    // 如果未启用或控件验证失败，则直接返回
                    if (EnableCheckBox.IsChecked != true || !ValidateControls()) 
                    {
                        return;
                    }

                    // 获取网关地址
                    string gatewayAddress = GatewayAddressTextBox.Text.Trim();
                    // 获取检查间隔和关机阈值（从秒转换为毫秒）
                    if (!int.TryParse(CheckIntervalTextBox.Text, out int checkIntervalSeconds) ||
                        !int.TryParse(ShutdownThresholdTextBox.Text, out int shutdownThresholdSeconds)) 
                    {
                        return;
                    }

                    // 转换为毫秒用于内部计算
                    int checkInterval = checkIntervalSeconds * 1000;
                    int shutdownThreshold = shutdownThresholdSeconds * 1000;

                    // 启动定时器
                    _timer = new Timer(CheckGateway, new object[] { gatewayAddress, shutdownThreshold }, 0, checkInterval);
                    UpdateUI("检测中...", "检测已启动");
                }
                catch (Exception ex)
                {
                    Log($"启动监控时出错: {ex.Message}");
                    UpdateUI("启动监控失败", $"启动监控失败: {ex.Message}");
                }
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
                try
                {
                    // 如果定时器不为空，则停止检测
                    if (_timer != null)
                    {
                        // 释放定时器
                        _timer.Dispose();
                        // 置空定时器
                        _timer = null;
                        UpdateUI("检测已停止", "检测已停止");
                    }
                }
                catch (Exception ex)
                {
                    Log($"停止监控时出错: {ex.Message}");
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
            _powerOutageStartTime = null;
        }

        /// <summary>
        /// 更新 UI
        /// </summary>
        /// <param name="status">当前状态</param>
        /// <param name="logMessage">日志信息</param>
        private void UpdateUI(string status, string logMessage)
        {
            try
            {
                // 更新状态文本和日志
                Dispatcher.BeginInvoke(() =>
                {
                    StatusTextBlock.Text = status;
                    Log(logMessage);
                });
            }
            catch (Exception ex)
            {
                // 如果UI更新失败，至少记录到控制台
                Console.WriteLine($"UI更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查网关可通性
        /// </summary>
        /// <param name="state">状态</param>
        private void CheckGateway(object state)
        {
            try
            {
                // 获取参数
                var parameters = (object[])state;
                string gatewayAddress = (string)parameters[0];
                int shutdownThreshold = (int)parameters[1];

                // 使用 Ping 类检查网关可达性
                using (Ping ping = new Ping())
                {
                    PingReply reply = ping.Send(gatewayAddress, 3000); // 增加超时时间到3秒
                    if (reply.Status == IPStatus.Success)
                    {
                        HandleGatewayReachable(gatewayAddress, reply);
                    }
                    else
                    {
                        HandlePowerOutage(gatewayAddress, shutdownThreshold, $"Ping状态: {reply.Status}");
                    }
                }
            }
            catch (Exception ex)
            {
                var parameters = (object[])state;
                string gatewayAddress = (string)parameters[0];
                int shutdownThreshold = (int)parameters[1];
                HandlePowerOutage(gatewayAddress, shutdownThreshold, $"Ping异常: {ex.Message}");
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
            lock (_lockObject) 
            { 
                _powerOutageStartTime = null;
                // 如果之前已经触发了关机，现在网络恢复了，重置状态
                if (_shutdownTriggered && !_countdownInProgress)
                {
                    _shutdownTriggered = false;
                }
            }
            // 更新 UI
            UpdateUI($"网关 {gatewayAddress} 可达", $"网关 {gatewayAddress} 可达，延迟 {reply.RoundtripTime} 毫秒");
        }

        /// <summary>
        /// 处理停电事件
        /// </summary>
        /// <param name="gatewayAddress">网关地址</param>
        /// <param name="shutdownThreshold">关机阈值</param>
        /// <param name="reason">不可达原因</param>
        private void HandlePowerOutage(string gatewayAddress, int shutdownThreshold, string reason = "")
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
                    string message = string.IsNullOrEmpty(reason) 
                        ? $"网关 {gatewayAddress} 不可达" 
                        : $"网关 {gatewayAddress} 不可达 ({reason})";
                    UpdateUI("网关不可达", message);
                }
                // 不为空且停电时间超过关机阈值
                else if ((DateTime.Now - _powerOutageStartTime.Value).TotalMilliseconds > shutdownThreshold)
                {
                    // 触发关机
                    _shutdownTriggered = true;
                    // 停止检测
                    _timer?.Dispose();
                    _timer = null;
                    
                    // 更新 UI
                    Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            // 执行关机逻辑
                            UpdateUI("关机中...", "网络中断时间超过阈值，准备关机...");
                            if (DebugCheckBox.IsChecked == true) 
                            {
                                MessageBox.Show("已触发关机（调试模式）", "调试信息", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else 
                            {
                                ShowShutdownCountdown();
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"处理关机事件时出错: {ex.Message}");
                        }
                    });
                }
                else
                {
                    // 显示剩余时间
                    var elapsedTime = DateTime.Now - _powerOutageStartTime.Value;
                    var remainingTime = TimeSpan.FromMilliseconds(shutdownThreshold) - elapsedTime;
                    if (remainingTime.TotalSeconds > 0)
                    {
                        UpdateUI($"网关不可达 (剩余 {remainingTime.TotalSeconds:F0} 秒)", 
                               $"网关持续不可达，剩余 {remainingTime.TotalSeconds:F0} 秒后将关机");
                    }
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

            try
            {
                // 显示关机倒计时窗口
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        // 获取用户设置的倒计时时长
                        int countdownSeconds = Properties.Settings.Default.ShutdownCountdown;
                        
                        // 创建关机倒计时窗口
                        var countdownWindow = new ShutdownCountdownWindow(countdownSeconds);
                        // 订阅事件
                        countdownWindow.CountdownCancelled += CountdownWindow_CountdownCancelled;
                        // 订阅窗口关闭事件
                        countdownWindow.Closed += (s, e) =>
                        {
                            lock (_lockObject)
                            {
                                // 倒计时进行中标志为false
                                _countdownInProgress = false;
                            }
                        };

                        // 显示窗口, 如果返回值为true且未取消，则执行关机
                        if (countdownWindow.ShowDialog() == true && !countdownWindow.IsCancelled)
                        {
                            // 执行关机
                            ShutdownComputer();
                        }
                        else
                        {
                            // 用户取消了关机
                            UpdateUI("关机已取消", "用户取消了关机操作");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"显示关机倒计时窗口时出错: {ex.Message}");
                        lock (_lockObject)
                        {
                            _countdownInProgress = false;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Log($"启动关机倒计时时出错: {ex.Message}");
                lock (_lockObject)
                {
                    _countdownInProgress = false;
                }
            }
        }

        // 关机倒计时窗口取消事件处理
        private void CountdownWindow_CountdownCancelled(object sender, EventArgs e)
        {
            lock (_lockObject)
            {
                // 重置所有状态
                _countdownInProgress = false;
                _shutdownTriggered = false;
                _powerOutageStartTime = null;
                
                // 更新UI
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        EnableCheckBox.IsChecked = false;
                        UpdateUI("检测已禁用", "用户取消关机，检测已禁用");
                    }
                    catch (Exception ex)
                    {
                        Log($"取消关机时更新UI出错: {ex.Message}");
                    }
                });
            }
        }

        // 执行关机命令
        private void ShutdownComputer()
        {
            try
            {
                Log("即将执行关机命令");
                // 执行关机命令
                Process.Start(new ProcessStartInfo("shutdown", "/s /t 0") 
                { 
                    CreateNoWindow = true, 
                    UseShellExecute = false 
                });
            }
            catch (Exception ex)
            {
                Log($"执行关机命令失败: {ex.Message}");
                MessageBox.Show($"执行关机命令失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 记录日志
        private void Log(string message)
        {
            try
            {
                // 使用 StringBuilder 构建日志消息（不添加换行符，由TextBox自动处理）
                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}";

                // 更新UI
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        // 如果不是第一条日志，添加换行符
                        if (!string.IsNullOrEmpty(LogTextBox.Text))
                        {
                            LogTextBox.AppendText(Environment.NewLine);
                        }
                        
                        LogTextBox.AppendText(logMessage);
                        
                        // 限制日志长度，避免内存占用过多
                        if (LogTextBox.Text.Length > 10000)
                        {
                            var lines = LogTextBox.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                            if (lines.Length > 100)
                            {
                                var keepLines = lines.Skip(lines.Length - 50).ToArray();
                                LogTextBox.Text = string.Join(Environment.NewLine, keepLines);
                                LogTextBox.AppendText(Environment.NewLine + "--- 日志已截断 ---");
                            }
                        }
                        
                        // 使用更精确的滚动控制
                        LogTextBox.CaretIndex = LogTextBox.Text.Length;
                        
                        // 强制刷新布局
                        LogTextBox.UpdateLayout();
                        
                        // 滚动到最后一行
                        LogTextBox.ScrollToEnd();
                        
                        // 确保光标在正确位置
                        LogTextBox.Select(LogTextBox.Text.Length, 0);
                    }
                    catch (Exception ex)
                    {
                        // 如果日志记录失败，输出到控制台
                        Console.WriteLine($"日志记录失败: {ex.Message}");
                        Console.WriteLine($"原始日志: {message}");
                    }
                });
            }
            catch (Exception ex)
            {
                // 如果完全失败，至少输出到控制台
                Console.WriteLine($"日志系统完全失败: {ex.Message}");
                Console.WriteLine($"原始日志: {message}");
            }
        }
        
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ClickCount == 2)
                {
                    this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                }
                else
                {
                    this.DragMove();
                }
            }
            catch (Exception ex)
            {
                Log($"标题栏操作出错: {ex.Message}");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// 窗口关闭时的清理工作
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 停止监控
                StopMonitoring();
                // 清理延迟保存定时器
                _saveDelayTimer?.Dispose();
                _saveDelayTimer = null;
                // 清理托盘图标
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
            }
            catch (Exception ex)
            {
                Log($"窗口关闭清理时出错: {ex.Message}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }
}