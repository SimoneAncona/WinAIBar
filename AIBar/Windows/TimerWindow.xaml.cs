using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Threading.Tasks;
using Windows.Foundation;

namespace AIBar.Windows;

public partial class TimerWindow : Window
{
    private readonly DispatcherTimer _timer;
    private TimeSpan _timeRemaining;
    private readonly TimeSpan _initialTime;

    public TimerWindow(TimeSpan timeout)
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        AppWindow.Resize(new(400, 400));
        var presenter = AppWindow.Presenter as OverlappedPresenter;
        presenter!.IsMaximizable = false;
        presenter!.IsResizable = false;

        _initialTime = timeout;
        _timeRemaining = _initialTime;

        TimerPath.Width = 400;
        TimerPath.Height = 400;
        TimerPath.HorizontalAlignment = HorizontalAlignment.Center;
        TimerPath.VerticalAlignment = VerticalAlignment.Center;


        TimerText.Text = _timeRemaining.ToString(@"mm\:ss");

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private async void Timer_Tick(object? sender, object e)
    {
        if (_timeRemaining.TotalSeconds > 0)
        {
            _timeRemaining = _timeRemaining.Subtract(TimeSpan.FromSeconds(1));
            TimerText.Text = _timeRemaining.ToString(@"hh\:mm\:ss");
            UpdateArc();
        }
        else
        {
            _timer.Stop();
            var notification = new AppNotificationBuilder()
                .AddText("Timer expired");
            AppNotificationManager.Default.Show(notification.BuildNotification());
            await Task.Delay(2000);
            Close();
        }
    }

    private void UpdateArc()
    {
        double progress = 1 - (_timeRemaining.TotalSeconds / _initialTime.TotalSeconds);

        double centerX = 200;
        double centerY = 199;
        double radius = 70;

        double angle = progress * 360;

        double endX = centerX + radius * Math.Sin(Math.PI * angle / 180);
        double endY = centerY - radius * Math.Cos(Math.PI * angle / 180);

       
        double startX = centerX;
        double startY = centerY - radius;

        bool isLargeArc = angle > 180.0;

        PathGeometry pathGeometry = new();
        PathFigure pathFigure = new()
        {
            StartPoint = new Point(startX, startY)
        };

        ArcSegment arcSegment = new()
        {
            Point = new Point(endX, endY),
            Size = new Size(radius, radius),
            IsLargeArc = isLargeArc,
            SweepDirection = SweepDirection.Clockwise

        };
        pathFigure.Segments.Add(arcSegment);
        pathGeometry.Figures.Add(pathFigure);

        TimerPath.Data = pathGeometry;
    }
}