using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Microsoft.Windows.Storage.Pickers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Installer;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        AppWindow.Resize(new(800, 700));
        InstallPathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WinAIBar");
        if (!IsRunningAsAdmin())
        {
            InstallButton.IsEnabled = false;
            TextWarning.Text = "\u26A0 Please run the installer as administrator";
            InstallPathTextBox.IsReadOnly = true;
        }
        CheckEmptyFolder();
    }

    static bool IsRunningAsAdmin()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        FolderPicker folderPicker = new(AppWindow.Id)
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder
        };

        var folder = await folderPicker.PickSingleFolderAsync();

        if (folder is not null)
        {
            InstallPathTextBox.Text = folder.Path;
            CheckEmptyFolder();
        }
    }

    private void CheckEmptyFolder()
    {
        var path = InstallPathTextBox.Text;
        if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
        {
            TextWarning.Text = "\u26A0 The folder is not empty";
            return;
        }
        TextWarning.Text = "";
    }

    private static void ScrollToEnd(TextBox textBox)
    {
        var scrollViewer = textBox.FindDescendant<ScrollViewer>();
        if (scrollViewer is null) return;
        scrollViewer.ChangeView(null, scrollViewer.ExtentHeight, null, true);
    }

    private static string ToPrintable(string str)
    {
        var res = new string([.. str.Where(c => !char.IsControl(c))]);
        string ansiPattern = @"\u001b\[[\d;]*[mK]";
        return Regex.Replace(res, ansiPattern, string.Empty);
    }

    private void InstallPathTextBox_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        CheckEmptyFolder();
    }

    private void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        var stack = new StackPanel();
        var logTextBox = new TextBox
        {
            Height = 350,
            IsReadOnly = true,
            Background = null,
            AcceptsReturn = true,
            FontFamily = new("Consolas"),
            FontSize = 11,
            BorderThickness = new(0)
        };
        stack.Children.Add(new TextBlock()
        {
            Text = "Installing...",
            Margin = new Thickness(0, 0, 0, 10),
            FontSize = 20,
        });
        stack.Children.Add(logTextBox);
        MainGrid.Children.Clear();
        MainGrid.Children.Add(stack);
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-noprofile -executionpolicy bypass -file \"" + AppDomain.CurrentDomain.BaseDirectory + "Scripts\\Install.ps1\" \"" + InstallPathTextBox.Text + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                Verb = "runas",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName),

            });
            if (process is null)
            {
                return;
            }
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        logTextBox.Text += "(info) " + ToPrintable(args.Data) + Environment.NewLine;
                        ScrollToEnd(logTextBox);
                    });
                }
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        logTextBox.Text += "(warning) " + ToPrintable(args.Data) + Environment.NewLine;
                        ScrollToEnd(logTextBox);
                    });
                }
            };
            process.Exited += (sender, args) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        RegisterApp(InstallPathTextBox.Text);
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = Path.Combine(InstallPathTextBox.Text, "AIBar.exe"),
                        });
                        logTextBox.Text += "\u2714 Installation completed. You can close this window" + Environment.NewLine;
                    }
                    catch (Exception ex)
                    {
                        logTextBox.Text += $"\u26A0 Installation partially completed or failed: {ex}" + Environment.NewLine;
                        if (ex.InnerException is not null)
                            logTextBox.Text += $"\t{ex.InnerException.Message}";
                    }
                });
            };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            logTextBox.Text += $"\u274C Fatal error: {ex}" + Environment.NewLine;
            if (ex.InnerException is not null)
                logTextBox.Text += $"\t{ex.InnerException.Message}";
        }
    }

    private static void RegisterApp(string path)
    {
        string appName = "Win AIBar";
        string displayName = "Win AIBar";
        string displayIcon = Path.Combine(path, "AIBar.exe");
        string publisher = "Simone Ancona";
        string uninstallString = Path.Combine(path, "uninstall.bat");

        var key = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + appName
        );
        key ??= Registry.LocalMachine.CreateSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + appName
            );

        key.SetValue("DisplayName", displayName, RegistryValueKind.String);
        key.SetValue("Publisher", publisher, RegistryValueKind.String);
        key.SetValue("InstallLocation", path, RegistryValueKind.String);
        key.SetValue("DisplayIcon", displayIcon, RegistryValueKind.String);
        key.SetValue("UninstallString", uninstallString, RegistryValueKind.String);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.Close();
    }
}