using System;
using System.Timers;
using System.Windows;

namespace PowerGuard
{
    public partial class ShutdownCountdownWindow : Window
    {
        // 定时器
        private Timer _countdownTimer;
        // 倒计时秒数
        private int _countdownSeconds;
        // 初始倒计时秒数（用于计算进度）
        private readonly int _initialCountdownSeconds;
        // 是否取消
        public bool IsCancelled { get; private set; }
        // 取消事件
        public event EventHandler CountdownCancelled;

        // 窗口构造函数
        public ShutdownCountdownWindow(int countdownSeconds)
        {
            // 初始化组件
            InitializeComponent();
            // 初始化倒计时秒数
            _countdownSeconds = countdownSeconds;
            _initialCountdownSeconds = countdownSeconds;
            // 设置倒计时文本
            UpdateDisplay();
            // 初始化定时器
            InitializeTimer();
            // 设置窗口属性
            InitializeWindow();
        }

        /// <summary>
        /// 初始化窗口属性
        /// </summary>
        private void InitializeWindow()
        {
            try
            {
                // 设置窗口显示在最前面
                this.Topmost = true;
                // 激活窗口
                this.Activate();
                // 获得焦点
                this.Focus();
                
                // 播放系统警告声音
                System.Media.SystemSounds.Exclamation.Play();
            }
            catch (Exception ex)
            {
                // 如果有错误，记录但不阻止窗口显示
                Console.WriteLine($"初始化窗口时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化定时器
        /// </summary>
        private void InitializeTimer()
        {
            try
            {
                // 创建定时器
                _countdownTimer = new Timer(1000); // 每秒触发一次
                _countdownTimer.Elapsed += CountdownTimer_Elapsed;  // 订阅事件
                _countdownTimer.AutoReset = true;   // 自动重置
                _countdownTimer.Start();    // 启动定时器
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化倒计时器失败: {ex.Message}", "PowerGuard - 错误", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                Close();
            }
        }

        /// <summary>
        /// 更新显示内容
        /// </summary>
        private void UpdateDisplay()
        {
            try
            {
                // 更新倒计时文本
                CountdownTextBlock.Text = _countdownSeconds.ToString();
                
                // 更新进度条
                if (_initialCountdownSeconds > 0)
                {
                    double progress = (double)_countdownSeconds / _initialCountdownSeconds * 100;
                    CountdownProgressBar.Value = progress;
                }

                // 根据剩余时间改变颜色
                if (_countdownSeconds <= 10)
                {
                    CountdownTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                    CountdownProgressBar.Foreground = System.Windows.Media.Brushes.Red;
                }
                else if (_countdownSeconds <= 30)
                {
                    CountdownTextBlock.Foreground = System.Windows.Media.Brushes.Orange;
                    CountdownProgressBar.Foreground = System.Windows.Media.Brushes.Orange;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新显示时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 定时器事件处理
        /// </summary>
        private void CountdownTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                // 秒数减一
                _countdownSeconds--;

                // 使用 Dispatcher.Invoke 在 UI 线程上更新 UI
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // 更新显示
                        UpdateDisplay();
                        
                        // 在最后10秒播放提示音
                        if (_countdownSeconds <= 10 && _countdownSeconds > 0)
                        {
                            System.Media.SystemSounds.Beep.Play();
                        }
                        
                        // 如果倒计时结束
                        if (_countdownSeconds <= 0)
                        {
                            _countdownTimer?.Stop(); // 停止定时器
                            DialogResult = true;    // 设置对话框结果为 true
                            Close();                // 关闭窗口
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"更新UI时出错: {ex.Message}");
                        // 如果UI更新失败，仍然继续倒计时逻辑
                        if (_countdownSeconds <= 0)
                        {
                            _countdownTimer?.Stop();
                            DialogResult = true;
                            Close();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"倒计时器事件处理出错: {ex.Message}");
                // 出错时安全关闭
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        _countdownTimer?.Stop();
                        DialogResult = false;
                        Close();
                    }
                    catch
                    {
                        // 强制关闭
                        Environment.Exit(1);
                    }
                });
            }
        }

        /// <summary>
        /// 取消按钮点击事件处理
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 设置取消标志
                IsCancelled = true;
                // 停止定时器
                _countdownTimer?.Stop();
                // 设置对话框结果为 false
                DialogResult = false;
                // 触发取消事件
                CountdownCancelled?.Invoke(this, EventArgs.Empty);
                // 关闭窗口
                Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"取消关机时出错: {ex.Message}");
                // 确保窗口关闭
                try
                {
                    Close();
                }
                catch
                {
                    Environment.Exit(0);
                }
            }
        }

        /// <summary>
        /// 处理键盘输入
        /// </summary>
        /// <param name="e"></param>
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                // ESC键取消关机
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    CancelButton_Click(null, null);
                }
                // 空格键也可以取消关机
                else if (e.Key == System.Windows.Input.Key.Space)
                {
                    CancelButton_Click(null, null);
                }
                
                base.OnKeyDown(e);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理键盘输入时出错: {ex.Message}");
                base.OnKeyDown(e);
            }
        }

        /// <summary>
        /// 在窗口关闭时清理定时器和事件订阅
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 清理定时器
                if (_countdownTimer != null)
                {
                    _countdownTimer.Elapsed -= CountdownTimer_Elapsed;
                    _countdownTimer.Stop();
                    _countdownTimer.Dispose();
                    _countdownTimer = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清理资源时出错: {ex.Message}");
            }
            finally
            {
                // 调用基类方法
                base.OnClosed(e);
            }
        }

        /// <summary>
        /// 防止窗口被最小化
        /// </summary>
        /// <param name="e"></param>
        protected override void OnStateChanged(EventArgs e)
        {
            try
            {
                if (WindowState == WindowState.Minimized)
                {
                    WindowState = WindowState.Normal;
                    Activate();
                }
                base.OnStateChanged(e);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理窗口状态变化时出错: {ex.Message}");
                base.OnStateChanged(e);
            }
        }
    }
}