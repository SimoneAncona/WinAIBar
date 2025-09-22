using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AIBar;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window, IDisposable
{
    private readonly SLMClient _client;
    private readonly Options _options;
    private static bool s_hidden = false;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int VK_SPACE = 0x20;
    private const int VK_LMENU = 0xA4;
    private const int WM_KEYDOWN = 0x0100;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYUP = 0x0101;

    private const int Width = 1200;
    private const int Height = 130;
    private string Placeholder = "Ask me everything...";

    private readonly LowLevelKeyboardProc _proc;
    private static IntPtr _hookID = IntPtr.Zero;
    private static bool altPressed = false;
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
        IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
    public MainWindow(Options options)
    {
        InitializeComponent();
        _options = options;
        _proc = HookCallback;
        _hookID = SetHook(_proc);
        ExtendsContentIntoTitleBar = true;
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(Width, Height));
        appWindow.IsShownInSwitchers = true;
        var presenter = appWindow.Presenter as OverlappedPresenter;
        presenter!.IsMaximizable = false;
        presenter!.IsMinimizable = false;
        presenter!.IsResizable = false;
        presenter!.SetBorderAndTitleBar(true, false);
        presenter!.IsAlwaysOnTop = true;
        CenterToScreen(appWindow, windowId);
        _client = new(
            modelName: _options.Model,
            ollamaPath: Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? throw new Exception("Cannot find LOCALAPPDATA"), "Programs", "Ollama", "ollama.exe")
        );
        if (_options.SelfMode)
            Placeholder = "(Self mode) Start typing...";
        SearchBox.PlaceholderText = Placeholder;
        Task.Run(_client.StartOllama);
        Closed += MainWindow_Closed;
    }

    #region Win32 calls
    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
            GetModuleHandle(curModule.ModuleName), 0);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WM_SYSKEYDOWN || wParam == WM_KEYDOWN))
        {
            int vkCode = Marshal.ReadInt32(lParam);

            if (vkCode == VK_LMENU)
            {
                altPressed = true;
                return 0;
            }
            if (altPressed && vkCode == VK_SPACE)
            {
                 IntPtr hwnd = WindowNative.GetWindowHandle(this);
                var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
                if (s_hidden) appWindow.Show();
                else appWindow.Hide();
                s_hidden = !s_hidden;
                return 1;
            }
        }
        if (nCode >= 0 && (wParam == WM_KEYUP))
        {
            int vkCode = Marshal.ReadInt32(lParam);

            if (vkCode == VK_LMENU) altPressed = false;
            return IntPtr.Zero;
        }


        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    #endregion

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        args.Handled = true;
        AppWindow.Hide();
        s_hidden = true;
    }


    public void Resize(SizeInt32 x)
    {
        AppWindow.Resize(x);
    }
    private static void CenterToScreen(AppWindow appWindow, WindowId winId)
    {
        if (appWindow is null) return;
        DisplayArea displayArea = DisplayArea.GetFromWindowId(winId, DisplayAreaFallback.Nearest);

        var CenteredPosition = appWindow.Position;
        CenteredPosition.X = ((displayArea.WorkArea.Width - appWindow.Size.Width) / 2);
        CenteredPosition.Y = ((displayArea.WorkArea.Height - appWindow.Size.Height) / 4);
        appWindow.Move(CenteredPosition);
    
    }

    private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            Resize(new(Width, Height));
            var text = SearchBox.Text;
            Debug.WriteLine($"Prompt: {text}");
            SearchBox.Text = null;
            SearchBox.IsReadOnly = true;
            SearchBox.PlaceholderText = "Loading...";
            try
            {
                string res;
                if (_options.SelfMode && text.StartsWith('>'))
                    res = text.TrimStart('>');
                else
                    res = (await _client.GenerateAsync(text)).Replace("```json", "").Replace("```", "");
                Debug.WriteLine(res);
                var actions = JsonConvert.DeserializeObject<List<ActionResult>>(res) ?? throw new Exception($"Cannot convert {res}");
                await ExecuteActions.ExecuteAsync(text, actions, this);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            SearchBox.IsReadOnly = false;
            SearchBox.PlaceholderText = Placeholder;
        }
    }

    public void AddItem(UIElement item)
    {
        ItemsContainer.Children.Add(item);
    }

    public void ClearItems()
    {
        ItemsContainer.Children.Clear();
    }

    public void Hide()
    {
        s_hidden = true;
        AppWindow.Hide();
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
