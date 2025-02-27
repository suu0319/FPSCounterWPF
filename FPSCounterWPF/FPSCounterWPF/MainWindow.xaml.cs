using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using PresentMonFps;

namespace WpfFPS
{
    public partial class MainWindow : Window
    {
        private bool _isRunning;
        private Thread _runningThread;
        private Task? _fpsMonitoringTask = null; 
        private CancellationTokenSource _fpsCancelToken = new CancellationTokenSource();
        private IntPtr _currentHwnd = IntPtr.Zero; // Track current hooked HWND
        
        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                return;
            }

            UpdateLog("Start");
            _isRunning = true;

            _runningThread = new Thread(() => StartRunning()) { IsBackground = true };
            _runningThread.Start();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRunning)
            {
                return;
            }

            UpdateLog("Stop");
            _isRunning = false;
            _runningThread?.Join();
            _runningThread = null;

            Dispatcher.Invoke(() =>
            {
                txtLog.Clear();
            });
        }

        private void UpdateLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtLog.AppendText(message + Environment.NewLine);
                txtLog.ScrollToEnd();
            });
        }

        private string GetProcessNameByHwnd(IntPtr hwnd)
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return null;

            try
            {
                return Process.GetProcessById((int)pid).ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }
        
        private async void StartRunning()
        {
            Console.WriteLine("[INFO] FPS monitoring started");

            while (_isRunning)
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd != IntPtr.Zero && hwnd != _currentHwnd)
                {
                    _currentHwnd = hwnd;
                    Console.WriteLine($"[INFO] Detected new foreground application HWND: {hwnd}");
                    
                    _fpsCancelToken.Cancel();
                    _fpsCancelToken = new CancellationTokenSource(); // Create a new token
                    
                    string processName = GetProcessNameByHwnd(hwnd);
                    if (string.IsNullOrEmpty(processName))
                    {
                        Console.WriteLine("[INFO] Unable to retrieve process name");
                        await Task.Delay(1000);
                        continue;
                    }
                    
                    GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid == 0)
                    {
                        Console.WriteLine($"[INFO] Process not found: {processName}");
                        await Task.Delay(1000);
                        continue;
                    }

                    Console.WriteLine($"[INFO] Monitoring FPS for {processName} (PID: {pid})");
                    
                    _fpsMonitoringTask = MonitorFPSAsync(pid, _fpsCancelToken.Token);
                }

                await Task.Delay(1000);
            }
        }
        
        private async Task MonitorFPSAsync(uint pid, CancellationToken cancellationToken)
        {
            try
            {
                await FpsInspector.StartForeverAsync(new FpsRequest(pid), (fpsData) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("[INFO] FPS monitoring canceled");
                        return;
                    }

                    Console.WriteLine($"[INFO] FPS: {fpsData.Fps:F1}");
                    Dispatcher.Invoke(() =>
                    {
                        UpdateLog($"FPS: {fpsData.Fps:F1}");
                    });
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] FPS monitoring failed: {ex.Message}");
            }
        }
        
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

    }
}
