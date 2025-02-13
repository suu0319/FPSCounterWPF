using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Windows;

namespace WpfFPS;

public partial class MainWindow : Window
{
    private bool _isRunning ;
    private OSDWindow? _osdWindow;
    private Thread? _runningThread;

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
        _osdWindow = new OSDWindow();
        _osdWindow.Show();
        _runningThread = new Thread(StartRunning) { IsBackground = true };
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
        _osdWindow?.Close();
        _osdWindow = null;
        
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

    private bool IsAppRunning(string appName)
    {
        return Process.GetProcesses().Any(process => process.ProcessName.Equals(appName, StringComparison.OrdinalIgnoreCase));
    }

    private void StartRunning()
    {
        Task.Run(() =>
        {
            while (_isRunning)
            {
                try
                {
                    using var mmf = MemoryMappedFile.OpenExisting("RTSSSharedMemoryV2");
                    using var accessor = mmf.CreateViewAccessor();
                    
                    unsafe
                    {
                        var ptr = (byte*)accessor.SafeMemoryMappedViewHandle.DangerousGetHandle().ToPointer();
                        var pMem = (RTSS_SHARED_MEMORY*)ptr;

                        if (pMem->dwSignature == 0x52545353 && pMem->dwVersion >= 0x00020000)
                        {
                            for (uint i = 0; i < pMem->dwAppArrSize; i++)
                            {
                                var pEntry = (RTSS_SHARED_MEMORY_APP_ENTRY*)(ptr + pMem->dwAppArrOffset + i * pMem->dwAppEntrySize);
                                var nameChars = new char[256];
                                
                                for (int j = 0; j < 256; j++)
                                {
                                    nameChars[j] = (char)pEntry->szName[j];
                                }

                                var appPath = new string(nameChars).TrimEnd('\0');
                                var appName = Path.GetFileNameWithoutExtension(appPath);

                                if (!IsAppRunning(appName))
                                {
                                    continue;
                                }

                                var fps = pEntry->dwStatData[0];

                                if (fps > 0)
                                {
                                    var topExe = OSDHandler.GetForegroundAppName();
                                    
                                    UpdateLog($"FPS: {fps} for exe: {appName}");
                                
                                    if (appName == topExe)
                                    {
                                        _osdWindow?.UpdateFPSDisplay(fps);
                                    }
                                }
                                else
                                {
                                    _osdWindow?.UpdateFPSDisplay(0);
                                }
                            }
                        }
                    }
                }
                catch (FileNotFoundException)
                {
                    UpdateLog("RTSS not running...");
                }
                catch (UnauthorizedAccessException)
                {
                    UpdateLog("Access denied. Run as administrator.");
                }
                catch (Exception ex)
                {
                    UpdateLog($"Error: {ex.Message}");
                }
                
                Thread.Sleep(1000);
            }
        });
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RTSS_SHARED_MEMORY
{
    public uint dwSignature; // Signature ("RTSS" in ASCII)
    public uint dwVersion; // Version (e.g., 0x00020000 for v2.0)
    public uint dwAppEntrySize; // Size of RTSS_SHARED_MEMORY_APP_ENTRY
    public uint dwAppArrOffset; // Offset of the application array
    public uint dwAppArrSize; // Number of application entries
    public uint dwOSDEntrySize; // Size of OSD entry (not used here)
    public uint dwOSDArrOffset; // Offset of OSD array (not used here)
    public uint dwOSDArrSize; // Number of OSD entries (not used here)
    public uint dwOSDFrame; // OSD frame counter
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public unsafe struct RTSS_SHARED_MEMORY_APP_ENTRY
{
    public fixed byte szName[256]; // Application name
    public uint dwProcessID; // Process ID
    public uint dwStatFlags; // Statistics flags
    public uint dwStatFrames; // Frames rendered
    public uint dwStatFrameTime; // Frame time (in milliseconds)
    public uint dwStatCount; // Statistics count
    public fixed uint dwStatData[8]; // Statistics data (e.g., FPS, frame time, etc.)
}