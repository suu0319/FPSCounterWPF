using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;

namespace WpfFPS
{
    public partial class OSDWindow : Window
    {
        private readonly DispatcherTimer _positionTimer;

        public OSDWindow()
        {
            InitializeComponent();
            
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            Width = 200;
            Height = 100;
            
            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            _positionTimer.Tick += (s, e) => UpdatePosition();
            _positionTimer.Start();
        }
        
        private void UpdatePosition()
        {
            var hwnd = GetForegroundWindow();
            
            if (hwnd != IntPtr.Zero)
            {
                if (GetWindowRect(hwnd, out var rect))
                {
                    var screenWidth = SystemParameters.PrimaryScreenWidth;
                    var screenHeight = SystemParameters.PrimaryScreenHeight;
                    var width = rect.Right - rect.Left;
                    var height = rect.Bottom - rect.Top;
                    var style = GetWindowLong(hwnd, GWL_STYLE);
                    var isBorderless = (style & WS_CAPTION) == 0;
                    var isFullScreen = width == (int)screenWidth && height == (int)screenHeight && rect is { Left: 0, Top: 0 };

                    if (isFullScreen || isBorderless)
                    {
                        Left = rect.Left;
                        Top = rect.Top;
                    }
                    else
                    {
                        Left = rect.Left;
                        Top = rect.Top + 30;
                    }
                }
            }
        }
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        
        private const int GWL_STYLE = -16;
        private const int WS_CAPTION = 0x00C00000;
        
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        
        public void UpdateFPSDisplay(float fps)
        {
            Dispatcher.Invoke(() =>
            {
                if (fps == 0)
                {
                    FPSLabel.Visibility = Visibility.Hidden;
                }
                else
                {
                    FPSLabel.Visibility = Visibility.Visible;
                    FPSLabel.Text = $"FPS: {fps}";
                }
            });
        }
    }
}
