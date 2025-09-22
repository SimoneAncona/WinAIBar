using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AIBar;


public class ActionResult
{
    public string Action { get; set; } = string.Empty;
    public string Argument { get; set; } = string.Empty;
}
public static class ExecuteActions
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

    private const uint HWND_BROADCAST = 0xFFFF;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    struct INPUT
    {
        public uint type;
        public InputUnion U;
        public static int Size => Marshal.SizeOf(typeof(INPUT));
    }

    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    const uint INPUT_KEYBOARD = 1;
    const uint KEYEVENTF_KEYUP = 0x0002;
    const ushort VK_MENU = 0x12;
    const ushort VK_F4 = 0x73;
    const int WM_COMMAND = 0x0111;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, UIntPtr wParam, string? lParam);

    private readonly static ListView s_chat = new()
    {
        Margin = new(20),
    };

    public static async Task ExecuteAsync(string prompt, List<ActionResult> actions, MainWindow window)
    {
        foreach (var action in actions) await ExecuteAsync(prompt, action, window);
    }

    private static void SendNotification(string title, string? message = null)
    {
        var notification = new AppNotificationBuilder()
                .AddText(title);
        if (message is not null)
            notification.AddText(message);
        AppNotificationManager.Default.Show(notification.BuildNotification());
    }

    private static async Task ExecuteAsync(string prompt, ActionResult action, MainWindow window)
    {
        switch (action.Action)
        {
            case "setTheme":
                SetTheme(action.Argument == "dark"); 
                break;
            case "open":
                StartProcess(action.Argument);
                break;
            case "searchWeb":
                StartProcess($"www.google.com/search?q={action.Argument.Replace(" ", "%20")}");
                break;
            case "close":
                KillProcess(action.Argument);
                break;
            case "searchFile":
                SearchFilesInUsers(action.Argument);
                break;
            case "response":
                Response(prompt, action.Argument, window);
                break;
            case "takeScreenshot":
                StartProcess("ms-screenclip:");
                break;
            case "setWifi":
                SetWifi(action.Argument == "on");
                break;
            case "shutdown":
            case "restart":
                await ShutdownDialog(window);
                break;
            case "showDesktop":
                ShowDesktop(window);
                break;

        }
    }

    private static void ShowDesktop(MainWindow window)
    {
        var desktop = FindWindow("Shell_TrayWnd", null);
        SendMessage(desktop, WM_COMMAND, 419, null);
        SetForegroundWindow(desktop);
        window.Hide();
    }

    private static async Task ShutdownDialog(MainWindow window)
    {
        var desktop = FindWindow("Shell_TrayWnd", null);
        SendMessage(desktop, WM_COMMAND, 419, null);
        SetForegroundWindow(desktop);
        window.Hide();
        await Task.Delay(1000);
        INPUT[] inputs = new INPUT[4];

        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].U.ki.wVk = VK_MENU;
        inputs[0].U.ki.dwFlags = 0;

        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].U.ki.wVk = VK_F4;
        inputs[1].U.ki.dwFlags = 0;

        inputs[2].type = INPUT_KEYBOARD;
        inputs[2].U.ki.wVk = VK_F4;
        inputs[2].U.ki.dwFlags = KEYEVENTF_KEYUP;

        inputs[3].type = INPUT_KEYBOARD;
        inputs[3].U.ki.wVk = VK_MENU;
        inputs[3].U.ki.dwFlags = KEYEVENTF_KEYUP;

        SendInput((uint)inputs.Length, inputs, INPUT.Size);
    }

    private static void SetWifi(bool on)
    {
        SendNotification("Admin is required");
        StartProcess($"netsh set interface Wi-Fi admin={(on ? "enable" : "disable")}", true);
        SendNotification("WiFi", $"WiFi is now {(on ? "on" : "off")}");
    }

    private static void Response(string prompt, string response, MainWindow window)
    {
        window.AppWindow.Resize(new(1200, 500));
        window.ClearItems();
        var request = new TextBlock()
        {
            Text = prompt,
            FontSize = 15,
            TextAlignment = Microsoft.UI.Xaml.TextAlignment.Right
        };
        var responseEl = new TextBlock()
        {
            Text = response,
            FontSize = 15,
            TextAlignment = Microsoft.UI.Xaml.TextAlignment.Left
        };
        s_chat.Items.Add(request);
        s_chat.Items.Add(responseEl);
        window.AddItem(s_chat);
    }

    private static void SearchFilesInUsers(string namePart)
    {
        var result = new List<string>();
        var stack = new Stack<string>();

        stack.Push(@"C:\Users");

        while (stack.Count > 0)
        {
            var currentDir = stack.Pop();

            try
            {
                foreach (var file in Directory.EnumerateFiles(currentDir))
                {
                    if (Path.GetFileName(file).Contains(namePart, StringComparison.OrdinalIgnoreCase))
                        result.Add(file);
                }

                foreach (var dir in Directory.EnumerateDirectories(currentDir))
                    stack.Push(dir);
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (PathTooLongException)
            {
            }
        }

        foreach (var file in result)
        {
            Process.Start("explorer.exe", $"/select,\"{file}\"");
        }
    }

    private static void KillProcess(string name)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/C taskkill /F /IM " + name + ".exe",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        SendNotification("Terminate a process", $"The process {name} has been terminated");
    }

    private static void SetTheme(bool dark)
    {
        try
        {
            string personalizationKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(personalizationKey, true))
            {
                if (key is null)
                    throw new InvalidOperationException("Could not access the registry key.");
                key.SetValue("AppsUseLightTheme", dark ? 0 : 1, RegistryValueKind.DWord);
                key.SetValue("SystemUsesLightTheme", dark ? 0 : 1, RegistryValueKind.DWord);
            }

            UIntPtr result;
            SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero,
                "ImmersiveColorSet", SMTO_ABORTIFHUNG, 100, out result);

            var toast = new AppNotificationBuilder()
                .AddText("Theme changed")
                .AddText($"Theme set to ${(dark ? "dark" : "light")}")
                .BuildNotification();

            AppNotificationManager.Default.Show(toast);
        } 
        catch (Exception ex)
        {
            var toast = new AppNotificationBuilder()
                .AddText("Unable to change theme")
                .BuildNotification();
            AppNotificationManager.Default.Show(toast);
            Debug.WriteLine(ex);
        }
    }

    private static void StartProcess(string name, bool asAdmin = false)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/C start " + name,
            UseShellExecute = false,
            CreateNoWindow = true,
            Verb = asAdmin ? "runas" : string.Empty
        });
    }
}
