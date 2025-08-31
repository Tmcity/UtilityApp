using System;
using System.Timers;
using System.Windows;

namespace PowerGuard
{
    public partial class ShutdownCountdownWindow : Window
    {
        // ��ʱ��
        private Timer _countdownTimer;
        // ����ʱ����
        private int _countdownSeconds;
        // ��ʼ����ʱ���������ڼ�����ȣ�
        private readonly int _initialCountdownSeconds;
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
            _initialCountdownSeconds = countdownSeconds;
            // ���õ���ʱ�ı�
            UpdateDisplay();
            // ��ʼ����ʱ��
            InitializeTimer();
            // ���ô�������
            InitializeWindow();
        }

        /// <summary>
        /// ��ʼ����������
        /// </summary>
        private void InitializeWindow()
        {
            try
            {
                // ���ô�����ʾ����ǰ��
                this.Topmost = true;
                // �����
                this.Activate();
                // ��ý���
                this.Focus();
                
                // ����ϵͳ��������
                System.Media.SystemSounds.Exclamation.Play();
            }
            catch (Exception ex)
            {
                // ����д��󣬼�¼������ֹ������ʾ
                Console.WriteLine($"��ʼ������ʱ����: {ex.Message}");
            }
        }

        /// <summary>
        /// ��ʼ����ʱ��
        /// </summary>
        private void InitializeTimer()
        {
            try
            {
                // ������ʱ��
                _countdownTimer = new Timer(1000); // ÿ�봥��һ��
                _countdownTimer.Elapsed += CountdownTimer_Elapsed;  // �����¼�
                _countdownTimer.AutoReset = true;   // �Զ�����
                _countdownTimer.Start();    // ������ʱ��
            }
            catch (Exception ex)
            {
                MessageBox.Show($"��ʼ������ʱ��ʧ��: {ex.Message}", "PowerGuard - ����", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                Close();
            }
        }

        /// <summary>
        /// ������ʾ����
        /// </summary>
        private void UpdateDisplay()
        {
            try
            {
                // ���µ���ʱ�ı�
                CountdownTextBlock.Text = _countdownSeconds.ToString();
                
                // ���½�����
                if (_initialCountdownSeconds > 0)
                {
                    double progress = (double)_countdownSeconds / _initialCountdownSeconds * 100;
                    CountdownProgressBar.Value = progress;
                }

                // ����ʣ��ʱ��ı���ɫ
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
                Console.WriteLine($"������ʾʱ����: {ex.Message}");
            }
        }

        /// <summary>
        /// ��ʱ���¼�����
        /// </summary>
        private void CountdownTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                // ������һ
                _countdownSeconds--;

                // ʹ�� Dispatcher.Invoke �� UI �߳��ϸ��� UI
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // ������ʾ
                        UpdateDisplay();
                        
                        // �����10�벥����ʾ��
                        if (_countdownSeconds <= 10 && _countdownSeconds > 0)
                        {
                            System.Media.SystemSounds.Beep.Play();
                        }
                        
                        // �������ʱ����
                        if (_countdownSeconds <= 0)
                        {
                            _countdownTimer?.Stop(); // ֹͣ��ʱ��
                            DialogResult = true;    // ���öԻ�����Ϊ true
                            Close();                // �رմ���
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"����UIʱ����: {ex.Message}");
                        // ���UI����ʧ�ܣ���Ȼ��������ʱ�߼�
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
                Console.WriteLine($"����ʱ���¼��������: {ex.Message}");
                // ����ʱ��ȫ�ر�
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
                        // ǿ�ƹر�
                        Environment.Exit(1);
                    }
                });
            }
        }

        /// <summary>
        /// ȡ����ť����¼�����
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // ����ȡ����־
                IsCancelled = true;
                // ֹͣ��ʱ��
                _countdownTimer?.Stop();
                // ���öԻ�����Ϊ false
                DialogResult = false;
                // ����ȡ���¼�
                CountdownCancelled?.Invoke(this, EventArgs.Empty);
                // �رմ���
                Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ȡ���ػ�ʱ����: {ex.Message}");
                // ȷ�����ڹر�
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
        /// �����������
        /// </summary>
        /// <param name="e"></param>
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                // ESC��ȡ���ػ�
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    CancelButton_Click(null, null);
                }
                // �ո��Ҳ����ȡ���ػ�
                else if (e.Key == System.Windows.Input.Key.Space)
                {
                    CancelButton_Click(null, null);
                }
                
                base.OnKeyDown(e);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"�����������ʱ����: {ex.Message}");
                base.OnKeyDown(e);
            }
        }

        /// <summary>
        /// �ڴ��ڹر�ʱ����ʱ�����¼�����
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // ����ʱ��
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
                Console.WriteLine($"������Դʱ����: {ex.Message}");
            }
            finally
            {
                // ���û��෽��
                base.OnClosed(e);
            }
        }

        /// <summary>
        /// ��ֹ���ڱ���С��
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
                Console.WriteLine($"������״̬�仯ʱ����: {ex.Message}");
                base.OnStateChanged(e);
            }
        }
    }
}