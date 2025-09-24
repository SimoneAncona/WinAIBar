using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace AIBar.Windows;

internal record FileSystemElement(string Path, string Type);


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
                await SearchFilesInUsers(action.Argument, window);
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
            case "showTime":
                ShowTime(window);
                break;
            case "setTimer":
                new TimerWindow(TimeSpan.Parse(action.Argument)).Activate();
                break;

        }
    }

    private static void ShowTime(MainWindow window)
    {
        var time = DateTime.Now.ToString("HH:mm");
        window.Resize(new(1200, 320));
        window.ClearItems();
        var responseEl = new TextBlock()
        {
            Text = time,
            FontSize = 45,
            TextAlignment = TextAlignment.Left,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Margin = new(20)
        };
        window.AddItem(responseEl);
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
            TextAlignment = TextAlignment.Right,

        };
        var responseEl = new TextBlock()
        {
            Text = response,
            FontSize = 15,
            TextAlignment = TextAlignment.Left
        };
        s_chat.Items.Add(request);
        s_chat.Items.Add(responseEl);
        window.AddItem(s_chat);
    }

    private static void Search(string root, string keyword, ConcurrentBag<FileSystemElement> results, CancellationToken token)
    {
        try
        {

            foreach (var file in Directory.EnumerateFiles(root))
            {
                token.ThrowIfCancellationRequested();

                if (file.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    results.Add(new FileSystemElement(file, "file"));
            }

            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                token.ThrowIfCancellationRequested();

                if (dir.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    results.Add(new FileSystemElement(dir, "dir"));

                Search(dir, keyword, results, token);
            }


        }
        catch { }
    }

    private static async Task SearchFilesInUsers(string namePart, MainWindow window)
    {
        List<string> userDirs =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        ];

        window.AppWindow.Resize(new(1200, 800));
        window.ClearItems();

        var listView = new ListView
        {
            Margin = new(19),
            Height = 400,
            MaxHeight = 400,
        };

        await window.DispatcherQueue.EnqueueAsync(() => window.AddItem(listView));

        var results = new ConcurrentBag<FileSystemElement>();
        var cts = new CancellationTokenSource();

        var searchTask = Task.Run(async () =>
        {
            List<Task> tasks = [];
            foreach (var dir in userDirs)
            {
                tasks.Add(Task.Run(() => Search(dir, namePart, results, cts.Token)));
            }
            await Task.WhenAll(tasks);
        }, cts.Token);

        var uiUpdateTask = Task.Run(async () =>
        {
            var shown = new HashSet<FileSystemElement>();

            while (!searchTask.IsCompleted)
            {
                if (results.Count >= 300 || MainWindow.Interrupt) cts.Cancel();
                var toShow = results.Except(shown).OrderByDescending(r => r.Type).ThenBy(r => r.Path).ToList();
                if (toShow.Count > 0)
                {
                    shown.UnionWith(toShow);
                    await window.DispatcherQueue.EnqueueAsync(() =>
                    {
                        foreach (var el in toShow)
                        {
                            var stackPanel = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Spacing = 10,
                            };
                            stackPanel.Children.Add(new FontIcon
                            {
                                Glyph = el.Type == "dir" ? "\uE8B7" : "\uE7C3",
                                FontSize = 20,
                                VerticalAlignment = VerticalAlignment.Center,
                            });
                            stackPanel.Children.Add(new TextBlock
                            {
                                Text = el.Path,
                                FontSize = 15,
                                TextAlignment = TextAlignment.Left,
                                TextTrimming = TextTrimming.CharacterEllipsis,
                            });
                            var b = new Button
                            {
                                Content = stackPanel
                            };
                            b.Click += (_, _) => StartProcess("explorer.exe /select, \"" + el.Path + "\"");
                            listView.Items.Add(new ListViewItem { Content = b });
                        }
                    });
                }
            }
        });

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
