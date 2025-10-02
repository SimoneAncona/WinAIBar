using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using static System.Net.Mime.MediaTypeNames;

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
        AppWindow.Resize(new(1000, 800));
        InstallPathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WinAIBar");
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        FolderPicker folderPicker = new()
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder
        };
        folderPicker.FileTypeFilter.Add("*");

        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(folderPicker, hwnd);

        StorageFolder folder = await folderPicker.PickSingleFolderAsync();

        if (folder != null)
        {
            InstallPathTextBox.Text = folder.Path;
        }
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

    private void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "pwsh.exe",
            Arguments = "-NoProfile -Command \"" + AppDomain.CurrentDomain.BaseDirectory + "Scripts\\Install.ps1\" \"" + InstallPathTextBox.Text + "\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            Verb = "runas",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)
        });
        if (process is null)
        {
            return;
        }
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
                    logTextBox.Text += "(error) " + ToPrintable(args.Data) + Environment.NewLine;
                    ScrollToEnd(logTextBox);
                });
            }
        };
        process.Exited += (sender, args) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                logTextBox.Text += "Installation completed." + Environment.NewLine;
            });
        };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }
}