using System;
using System.Timers;
using System.Windows;

namespace UtilityApp
{
    public partial class ShutdownCountdownWindow : Window
    {
        // 定时器
        private Timer _countdownTimer;
        // 倒计时秒数
        private int _countdownSeconds;
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
            // 设置倒计时文本
            CountdownTextBlock.Text = _countdownSeconds.ToString();
            // 初始化定时器
            InitializeTimer();
        }

        /// <summary>
        /// 初始化定时器
        /// </summary>
        private void InitializeTimer()
        {
            // 创建定时器
            _countdownTimer = new Timer(1000); // 每秒触发一次
            _countdownTimer.Elapsed += CountdownTimer_Elapsed;  // 订阅事件
            _countdownTimer.AutoReset = true;   // 自动重置
            _countdownTimer.Start();    // 启动定时器
        }

        /// <summary>
        /// 定时器事件处理
        /// </summary>
        private void CountdownTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // 秒数减一
            _countdownSeconds--;

            // 使用 Dispatcher.Invoke 在 UI 线程上更新 UI
            Dispatcher.Invoke(() =>
            {
                // 更新倒计时文本
                CountdownTextBlock.Text = _countdownSeconds.ToString();
                // 如果倒计时结束
                if (_countdownSeconds <= 0)
                {
                    _countdownTimer.Stop(); // 停止定时器
                    DialogResult = true;    // 设置对话框结果为 true
                    Close();                // 关闭窗口
                }
            });
        }

        /// <summary>
        /// 取消按钮点击事件处理
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // 设置取消标志
            IsCancelled = true;
            // 停止定时器
            _countdownTimer.Stop();
            // 设置对话框结果为 false
            DialogResult = false;
            // 触发取消事件
            CountdownCancelled?.Invoke(this, EventArgs.Empty);
            // 关闭窗口
            Close();
        }

        /// <summary>
        /// 在窗口关闭时清理定时器和事件订阅
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            // 调用基类方法
            base.OnClosed(e);
            // 清理定时器
            if (_countdownTimer != null)
            {
                _countdownTimer.Elapsed -= CountdownTimer_Elapsed;
                _countdownTimer.Dispose();
                _countdownTimer = null;
            }
        }
    }
}