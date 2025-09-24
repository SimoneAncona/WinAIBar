using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace AIBar.Windows;

public partial class PowerWindow : Window
{
    private const int Width = 250;
    private const int Height = 250;

    public PowerWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        AppWindow.Resize(new SizeInt32(Width, Height));
        AppWindow.IsShownInSwitchers = true;
        var presenter = AppWindow.Presenter as OverlappedPresenter;
        presenter!.IsMaximizable = false;
        presenter!.IsMinimizable = false;
        presenter!.IsResizable = false;
        presenter!.SetBorderAndTitleBar(true, false);
        presenter!.IsAlwaysOnTop = true;
        CenterToScreen();
    }

    private void CenterToScreen()
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);

        var CenteredPosition = AppWindow.Position;
        CenteredPosition.X = ((displayArea.WorkArea.Width - AppWindow.Size.Width) / 2);
        CenteredPosition.Y = ((displayArea.WorkArea.Height - AppWindow.Size.Height) / 2);
        AppWindow.Move(CenteredPosition);

    }

    [DllImport("Powrprof.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    public static extern bool SetSuspendState(bool hiberate, bool forceCritical, bool disableWakeEvent);


    private void Shutdown_Click(object sender, RoutedEventArgs e)
    {
        var psi = new ProcessStartInfo("shutdown", "/s /t 0")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(psi);
    }

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        var psi = new ProcessStartInfo("shutdown", "/r")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(psi);
    }

    private void Sleep_Click(object sender, RoutedEventArgs e)
    {
        SetSuspendState(false, true, true);
    }

    private void Look_Click(object sender, RoutedEventArgs e)
    {
        var psi = new ProcessStartInfo("rundll32.exe", "user32.dll,LockWorkStation")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(psi);
    }
}
