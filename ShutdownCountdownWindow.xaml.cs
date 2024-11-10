using System;
using System.Timers;
using System.Windows;

namespace UtilityApp
{
    public partial class ShutdownCountdownWindow : Window
    {
        // ��ʱ��
        private Timer _countdownTimer;
        // ����ʱ����
        private int _countdownSeconds;
        // �Ƿ�ȡ��
        public bool IsCancelled { get; private set; }
        // ȡ���¼�
        public event EventHandler CountdownCancelled;

        // ���ڹ��캯��
        public ShutdownCountdownWindow(int countdownSeconds)
        {
            // ��ʼ�����
            InitializeComponent();
            // ��ʼ������ʱ����
            _countdownSeconds = countdownSeconds;
            // ���õ���ʱ�ı�
            CountdownTextBlock.Text = _countdownSeconds.ToString();
            // ��ʼ����ʱ��
            InitializeTimer();
        }

        /// <summary>
        /// ��ʼ����ʱ��
        /// </summary>
        private void InitializeTimer()
        {
            // ������ʱ��
            _countdownTimer = new Timer(1000); // ÿ�봥��һ��
            _countdownTimer.Elapsed += CountdownTimer_Elapsed;  // �����¼�
            _countdownTimer.AutoReset = true;   // �Զ�����
            _countdownTimer.Start();    // ������ʱ��
        }

        /// <summary>
        /// ��ʱ���¼�����
        /// </summary>
        private void CountdownTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // ������һ
            _countdownSeconds--;

            // ʹ�� Dispatcher.Invoke �� UI �߳��ϸ��� UI
            Dispatcher.Invoke(() =>
            {
                // ���µ���ʱ�ı�
                CountdownTextBlock.Text = _countdownSeconds.ToString();
                // �������ʱ����
                if (_countdownSeconds <= 0)
                {
                    _countdownTimer.Stop(); // ֹͣ��ʱ��
                    DialogResult = true;    // ���öԻ�����Ϊ true
                    Close();                // �رմ���
                }
            });
        }

        /// <summary>
        /// ȡ����ť����¼�����
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // ����ȡ����־
            IsCancelled = true;
            // ֹͣ��ʱ��
            _countdownTimer.Stop();
            // ���öԻ�����Ϊ false
            DialogResult = false;
            // ����ȡ���¼�
            CountdownCancelled?.Invoke(this, EventArgs.Empty);
            // �رմ���
            Close();
        }

        /// <summary>
        /// �ڴ��ڹر�ʱ����ʱ�����¼�����
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            // ���û��෽��
            base.OnClosed(e);
            // ����ʱ��
            if (_countdownTimer != null)
            {
                _countdownTimer.Elapsed -= CountdownTimer_Elapsed;
                _countdownTimer.Dispose();
                _countdownTimer = null;
            }
        }
    }
}