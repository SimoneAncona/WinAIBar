using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AIBar;

public class Options
{
    public string Model { get; set; } = "gemma3:4b";
    public bool SelfMode { get; set; } = false;
}

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    public Window? Window;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        var options = ParseArguments(arguments);
        Window = new MainWindow(options);
        Window.Activate();
    }

    private static Options ParseArguments(string[] args)
    {
        var options = new Options();
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-m":
                    if (i == args.Length - 1) throw new Exception("Model name was expected after -m");
                    options.Model = args[i + 1];
                    i++;
                    break;
                case "-s":
                    options.SelfMode = true;
                    break;
                default:
                    throw new Exception($"Unrecogniezed {args[i]} argument");
            }
        }
        return options;
    }
}
