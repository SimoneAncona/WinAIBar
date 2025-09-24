using CommunityToolkit.WinUI;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
            case "getDir":
                await GetDir(action.Argument, window);
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
            case "look":
            case "sleep":
                new PowerWindow().Activate();
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


    private static void SetWifi(bool on)
    {
        SendNotification("Admin is required");
        StartProcess($"netsh set interface Wi-Fi admin={(on ? "enable" : "disable")}", true);
        SendNotification("WiFi", $"WiFi is now {(on ? "on" : "off")}");
    }

    private static void Response(string prompt, string response, MainWindow window)
    {
        window.Resize(new(1200, 500));
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

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;

                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1,
                             d[i, j - 1] + 1),
                             d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

    private static bool AreStringsSimilar(string s1, string s2, double threshold = 0.5)
    {
        s1 = s1.ToLower();
        s2 = s2.ToLower();

        int distance = LevenshteinDistance(s1, s2);
        int maxLen = Math.Max(s1.Length, s2.Length);

        double similarity = 1.0 - (double)distance / maxLen;

        return similarity >= threshold;
    }

    private static void Search(string root, string keyword, ConcurrentBag<FileSystemElement> results, CancellationToken token, bool recursive = true)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(root))
            {
                token.ThrowIfCancellationRequested();

                if (file.Contains(keyword, StringComparison.OrdinalIgnoreCase) || AreStringsSimilar(Path.GetFileNameWithoutExtension(file), keyword))
                    results.Add(new FileSystemElement(file, "file"));
            }

            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                if (new DirectoryInfo(dir).Attributes.HasFlag(FileAttributes.ReparsePoint))
                    continue;
                token.ThrowIfCancellationRequested();

                if (dir.Contains(keyword, StringComparison.OrdinalIgnoreCase) || AreStringsSimilar(Path.GetDirectoryName(dir) ?? dir, keyword))
                    results.Add(new FileSystemElement(dir, "dir"));

                if (recursive)
                    Search(dir, keyword, results, token);
            }


        }
        catch { }
    }

    private static async Task ShowFilesAsync(Task searchTask, ConcurrentBag<FileSystemElement> results, CancellationTokenSource cts, MainWindow window, ListView listView)
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
                            Background = new SolidColorBrush(Colors.Transparent),
                            Content = stackPanel,
                            BorderThickness = new Thickness(0)
                        };
                        b.Click += (_, _) => StartProcess("explorer.exe /select, \"" + el.Path + "\"");
                        listView.Items.Add(new ListViewItem { Content = b });
                    }
                });
            }

        }
    }

    private static async Task GetDir(string path, MainWindow window)
    {
        if (!Directory.Exists(path))
        {
            window.Resize();
            return;
        }
        window.Resize(new(1200, 800));
        window.ClearItems();
        var listView = new ListView
        {
            Margin = new(15),
            Height = 350,
            MaxHeight = 350,

        };

        listView.Loaded += (listView, _) =>
        {
            if (listView is not ListView lv) return;
            var scrollViewer = lv.FindDescendant<ScrollViewer>();
            if (scrollViewer is not null)
                scrollViewer.HorizontalScrollMode = ScrollMode.Enabled;
        };

        await window.DispatcherQueue.EnqueueAsync(() => window.AddItem(listView));
        var results = new ConcurrentBag<FileSystemElement>();
        var cts = new CancellationTokenSource();
        var searchTask = Task.Run(() =>
        {
            Search(path, string.Empty, results, cts.Token, false);
        }, cts.Token);
        var _ = Task.Run(() => ShowFilesAsync(searchTask, results, cts, window, listView));
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
            Margin = new(15),
            Height = 350,
            MaxHeight = 350,
        };

        listView.Loaded += (listView, _) =>
        {
            if (listView is not ListView lv) return;
            var scrollViewer = lv.FindDescendant<ScrollViewer>();
            if (scrollViewer is not null)
                scrollViewer.HorizontalScrollMode = ScrollMode.Enabled;
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

        var _ = Task.Run(() => ShowFilesAsync(searchTask, results, cts, window, listView));
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
