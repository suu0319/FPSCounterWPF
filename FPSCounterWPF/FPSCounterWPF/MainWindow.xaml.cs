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
        private GraphicsWindow _graphicsWindow;
        private IntPtr _currentHwnd = IntPtr.Zero;
        
        public MainWindow()
        {
            InitializeComponent();
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
        
        private (int x, int y, int width, int height) GetWindowInfo(IntPtr hwnd)
        {
            GetWindowRect(hwnd, out var rect);
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            var x = rect.Left;
            var y = rect.Top;
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            var style = GetWindowLong(hwnd, GWL_STYLE);
            var isBorderless = (style & WS_CAPTION) == 0;
            var isFullScreen = width == (int)screenWidth && height == (int)screenHeight && rect.Left == 0 && rect.Top == 0;
                
            if (!isBorderless || !isFullScreen)
            {
                y += 20;
            }

            return (x, y, width, height);
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
                    _fpsCancelToken = new CancellationTokenSource(); // 創建新的 CancellationToken

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

                    _fpsMonitoringTask = MonitorFPSAsync(pid, hwnd, _fpsCancelToken.Token);
                }

                await Task.Delay(1000);
            }
        }

        private async Task MonitorFPSAsync(uint pid, IntPtr hwnd, CancellationToken cancellationToken)
        {
            try
            {
                await FpsInspector.StartForeverAsync(new FpsRequest(pid), (fpsData) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("[INFO] FPS monitoring canceled");

                        if (_graphicsWindow != null)
                        {
                            _graphicsWindow.Hide();
                        }
                        
                        return;
                    }

                    Console.WriteLine($"[INFO] FPS: {fpsData.Fps}");
                    Dispatcher.Invoke(() =>
                    {
                        UpdateLog($"FPS: {fpsData.Fps}");
                    });

                    var (x, y, width, height) = GetWindowInfo(hwnd);

                    if (_graphicsWindow == null)
                    {
                        _graphicsWindow = new GraphicsWindow(hwnd, x, y, width, height);
                        _graphicsWindow.Run();
                    }
                    else
                    {
                        _graphicsWindow.SetWindowInfo(x, y, width, height, (int)fpsData.Fps);
                        _graphicsWindow.Show();
                        _graphicsWindow.KeepOverlayVisible();
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] FPS monitoring failed: {ex.Message}");
            }
        }
        
        [DllImport("user32.dll")]
        private static extern long GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_STYLE = -16;
        private const long WS_CAPTION = 0x00C00000;  // 視窗標題欄
        
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
