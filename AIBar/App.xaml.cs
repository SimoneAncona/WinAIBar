
using AIBar.Windows;
using Microsoft.UI.Xaml;
using System;
using System.IO;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AIBar;

public class Options
{
    public required string Model { get; set; }
    public bool SelfMode { get; set; }
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
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        var options = ParseArguments(arguments);
        Window = new MainWindow(options);
        Window.Activate();
    }

    private static Options ParseArguments(string[] args)
    {
        var options = new Options()
        { 
            Model = Path.Combine("C:", "Users", Environment.GetEnvironmentVariable("USERNAME") ?? "Public", ".cache", "huggingface", "hub", "models--Qwen--Qwen3-1.7B-GGUF", "snapshots", "90862c4b9d2787eaed51d12237eafdfe7c5f6077", "Qwen3-1.7B-Q8_0.gguf")
        };
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
