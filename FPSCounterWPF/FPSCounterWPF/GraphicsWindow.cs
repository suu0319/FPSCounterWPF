using System.Runtime.InteropServices;
using GameOverlay.Drawing;
using GameOverlay.Windows;
using SharpDX.Direct2D1;

namespace WpfFPS;

public class GraphicsWindow
{
    private readonly GameOverlay.Windows.GraphicsWindow _window;
    private readonly Graphics _graphics;

    private readonly Dictionary<string, SolidBrush> _brushes;
    private readonly Dictionary<string, Font> _fonts;
    private readonly Dictionary<string, GameOverlay.Drawing.Image> _images;
    private int _appFps;
    
    public GraphicsWindow(IntPtr hwnd, int posX, int posY, int appWidth, int appHeight)
    {
        _brushes = new Dictionary<string, SolidBrush>();
        _fonts = new Dictionary<string, Font>();
        _images = new Dictionary<string, GameOverlay.Drawing.Image>();

        _graphics = new Graphics()
        {
            MeasureFPS = true,
            PerPrimitiveAntiAliasing = true,
            TextAntiAliasing = true,
            UseMultiThreadedFactories = false,
            VSync = false,
            WindowHandle = hwnd
        };
        
        _window = new GameOverlay.Windows.GraphicsWindow(_graphics)
        {
            IsTopmost = true,
            IsVisible = true,
            FPS = 30,
            X = posX,
            Y = posY,
            Width = appWidth,
            Height = appHeight
        };
        
        _window.SetupGraphics += _window_SetupGraphics;
        _window.DestroyGraphics += _window_DestroyGraphics;
        _window.DrawGraphics += _window_DrawGraphics;
    }

    ~GraphicsWindow()
    {
        //_window.Dispose();
        //_graphics.Dispose();
    }

    public void Run()
    {
        _window.Create();
        SetLayeredWindow(_window.Handle);
        EnableOverlayDWM(_window.Handle);
    }

    public void Join()
    {
        _window.Join();
    }

    public void Stop()
    {
        _window.Dispose();
        _graphics.Dispose();
    }

    public void ReCreate()
    {
        _window.Recreate();
    }
    
    public void Show()
    {
        _window.IsVisible = true;
    }

    public void Hide()
    {
        _window.IsVisible = false;
    }
    
    public void KeepOverlayVisible()
    {
        ShowWindow(_window.Handle, SW_RESTORE);
    }

    public void SetWindowInfo(int posX, int posY, int appWidth, int appHeight, int fps)
    {
        _window.X = posX;
        _window.Y = posY;
        _window.Width = appWidth;
        _window.Height = appHeight;
        _appFps = fps;
    }
    
    private void _window_SetupGraphics(object sender, SetupGraphicsEventArgs e)
    {
        var gfx = e.Graphics;

        _brushes["black"] = gfx.CreateSolidBrush(0, 0, 0);
        _brushes["darkOrange"] = gfx.CreateSolidBrush(255, 140, 0);
        _brushes["background"] = gfx.CreateSolidBrush(0, 0, 0, 0);

        Console.WriteLine(_window.Handle.ToString("X"));

        // fonts don't need to be recreated since they are owned by the font factory and not the drawing device
        if (e.RecreateResources) return;

        _fonts.Add("Segoe UI", gfx.CreateFont("Segoe UI", 14));
    }

    private void _window_DestroyGraphics(object sender, DestroyGraphicsEventArgs e)
    {
        foreach (var pair in _brushes) pair.Value.Dispose();
        foreach (var pair in _fonts) pair.Value.Dispose();
        foreach (var pair in _images) pair.Value.Dispose();
    }

    private void _window_DrawGraphics(object sender, DrawGraphicsEventArgs e)
    {
        var gfx = e.Graphics;

        gfx.ClearScene(_brushes["background"]);
        gfx.DrawText(_fonts["Segoe UI"], 22, _brushes["darkOrange"], 20, 20, $"FPS: {_appFps}");

        var device = gfx.GetRenderTarget() as WindowRenderTarget;
        var factory = gfx.GetFactory();

        //var region = new SharpDX.Direct2D1.RoundedRectangle()
        //{
        //	RadiusX = 16.0f,
        //	RadiusY = 16.0f,
        //	Rect = new SharpDX.Mathematics.Interop.RawRectangleF(200, 200, 300, 300)
        //};

        var geometry = new PathGeometry(factory);

        var sink = geometry.Open();
        sink.SetFillMode(FillMode.Winding);
        sink.BeginFigure(new SharpDX.Mathematics.Interop.RawVector2(200, 200), FigureBegin.Filled);

        sink.AddLine(new SharpDX.Mathematics.Interop.RawVector2(300, 200));
        sink.AddArc(new ArcSegment()
        {
            ArcSize = ArcSize.Small,
            Point = new SharpDX.Mathematics.Interop.RawVector2(300, 300),
            RotationAngle = 0.0f,
            Size = new SharpDX.Size2F(16.0f, 16.0f),
            SweepDirection = SweepDirection.Clockwise
        });
        sink.AddLine(new SharpDX.Mathematics.Interop.RawVector2(200, 300));
        sink.AddArc(new ArcSegment()
        {
            ArcSize = ArcSize.Small,
            Point = new SharpDX.Mathematics.Interop.RawVector2(200, 200),
            RotationAngle = 0.0f,
            Size = new SharpDX.Size2F(16.0f, 16.0f),
            SweepDirection = SweepDirection.Clockwise
        });

        sink.EndFigure(FigureEnd.Open);
        sink.Close();
        sink.Dispose();

        // device.FillGeometry(geometry, _brushes["darkOrange"]);

        var options = new LayerParameters()
        {
            //ContentBounds = new SharpDX.Mathematics.Interop.RawRectangleF(float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, float.PositiveInfinity),
            GeometricMask = geometry,
            //Opacity = 1.0f
        };

        var layer = new Layer(device, new SharpDX.Size2F(gfx.Width, gfx.Height));

        device.PushLayer(ref options, layer);

        gfx.FillRectangle(_brushes["darkOrange"], 100, 100, 400, 400);

        device.PopLayer();

        layer.Dispose();
        geometry.Dispose();
    }
    
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SetWindowLong(IntPtr hwnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowLong(IntPtr hwnd, int nIndex);
    
    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
    
    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [StructLayout(LayoutKind.Sequential)]
    public struct Margins
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const int SW_RESTORE = 9;

    private void SetLayeredWindow(IntPtr hwnd)
    {
        uint extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST);
    }
    
    private void EnableOverlayDWM(IntPtr hwnd)
    {
        Margins margins = new Margins() { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }
}