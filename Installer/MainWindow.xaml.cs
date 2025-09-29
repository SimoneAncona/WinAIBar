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
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

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

    private void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "pwsh.exe",
            Arguments = AppDomain.CurrentDomain.BaseDirectory + "Scripts\\Install.ps1 \"" + InstallPathTextBox.Text + "\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            Verb = "runas"
        });
    }
}