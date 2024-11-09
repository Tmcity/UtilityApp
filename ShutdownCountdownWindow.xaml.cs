using System;
using System.Timers;
using System.Windows;

namespace UtilityApp
{
    public partial class ShutdownCountdownWindow : Window
    {
        private Timer _countdownTimer;
        private int _countdownSeconds;

        public bool IsCancelled { get; private set; }
        public event EventHandler CountdownCancelled;

        public ShutdownCountdownWindow(int countdownSeconds)
        {
            InitializeComponent();
            _countdownSeconds = countdownSeconds;
            CountdownTextBlock.Text = _countdownSeconds.ToString();
            InitializeTimer();
        }

        private void InitializeTimer()
        {
            _countdownTimer = new Timer(1000); // ÿ�봥��һ��
            _countdownTimer.Elapsed += CountdownTimer_Elapsed;
            _countdownTimer.AutoReset = true;
            _countdownTimer.Start();
        }

        private void CountdownTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _countdownSeconds--;

            // ʹ�� Dispatcher.Invoke �� UI �߳��ϸ��� UI
            Dispatcher.Invoke(() =>
            {
                CountdownTextBlock.Text = _countdownSeconds.ToString();
                if (_countdownSeconds <= 0)
                {
                    _countdownTimer.Stop();
                    DialogResult = true;
                    Close();
                }
            });
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsCancelled = true;
            _countdownTimer.Stop();
            DialogResult = false;
            CountdownCancelled?.Invoke(this, EventArgs.Empty);
            Close();
        }

        // �ڴ��ڹر�ʱ����ʱ�����¼�����
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (_countdownTimer != null)
            {
                _countdownTimer.Elapsed -= CountdownTimer_Elapsed;
                _countdownTimer.Dispose();
                _countdownTimer = null;
            }
        }
    }
}
