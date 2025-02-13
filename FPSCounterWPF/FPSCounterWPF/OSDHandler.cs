using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WpfFPS;

public class OSDHandler
{
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    public static string GetForegroundAppName()
    {
        IntPtr hwnd = GetForegroundWindow();
        uint processId;
        GetWindowThreadProcessId(hwnd, out processId);

        Process process = Process.GetProcessById((int)processId);
        return process.ProcessName;
    }
}
