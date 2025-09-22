using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Media.Protection.PlayReady;
using WinRT.Interop;

namespace AIBar;

public partial class TimerWindow : Window
{
    public TimerWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(400, 400));
        appWindow.IsShownInSwitchers = true;
        var presenter = appWindow.Presenter as OverlappedPresenter;
        presenter!.IsMaximizable = false;
        presenter!.IsMinimizable = true;
    }
}
