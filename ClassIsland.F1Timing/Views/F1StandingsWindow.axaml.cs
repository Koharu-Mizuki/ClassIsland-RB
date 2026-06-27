using System;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using ClassIsland.F1Timing.Services;

namespace ClassIsland.F1Timing.Views;

/// <summary>
/// F1 积分榜窗口，显示车手/车队积分排名。
/// </summary>
public partial class F1StandingsWindow : Window
{
    private readonly ErgastStandingsService _standings;
    private bool _showingDrivers = true;
    private bool _isClosing;
    private bool _closingAnimDone;

    public F1StandingsWindow()
    {
        InitializeComponent();
    }

    public F1StandingsWindow(ErgastStandingsService standings, bool topmost = false)
    {
        _standings = standings;
        Opacity = 0;
        InitializeComponent();
        Topmost = topmost;

        Opened += async (_, _) =>
        {
            await RunOpacityAnimation(from: 0.0, to: 1.0, durationMs: 280, easing: new QuarticEaseOut());
            _ = LoadDriversAsync();
        };
        Closing += OnWindowClosing;
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closingAnimDone) return;
        e.Cancel = true;
        if (_isClosing) return;
        _isClosing = true;
        await RunOpacityAnimation(from: 1.0, to: 0.0, durationMs: 180, easing: new QuadraticEaseIn());
        _closingAnimDone = true;
        Close();
    }

    private Task RunOpacityAnimation(double from, double to, int durationMs, Easing easing)
    {
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(durationMs),
            FillMode = FillMode.Forward,
            Easing = easing
        };
        animation.Children.Add(new KeyFrame
        {
            KeyTime = TimeSpan.Zero,
            Setters = { new Setter(OpacityProperty, from) }
        });
        animation.Children.Add(new KeyFrame
        {
            Cue = new Cue(1.0),
            Setters = { new Setter(OpacityProperty, to) }
        });
        return animation.RunAsync(this);
    }

    private async System.Threading.Tasks.Task LoadDriversAsync()
    {
        LoadingText.IsVisible = true;
        DriversList.IsVisible = true;
        ConstructorsList.IsVisible = false;
        _showingDrivers = true;

        try
        {
            var drivers = await _standings.GetDriverStandingsAsync();
            DriversList.ItemsSource = drivers;
            LoadingText.IsVisible = drivers.Count == 0;
            StatusText.Text = $"车手积分 · {drivers.Count} 位 · 缓存 1 小时";
        }
        catch
        {
            LoadingText.Text = "加载失败，请稍后重试";
        }
    }

    private async System.Threading.Tasks.Task LoadConstructorsAsync()
    {
        LoadingText.IsVisible = true;
        DriversList.IsVisible = false;
        ConstructorsList.IsVisible = true;
        _showingDrivers = false;

        try
        {
            var constructors = await _standings.GetConstructorStandingsAsync();
            ConstructorsList.ItemsSource = constructors;
            LoadingText.IsVisible = constructors.Count == 0;
            StatusText.Text = $"车队积分 · {constructors.Count} 支 · 缓存 1 小时";
        }
        catch
        {
            LoadingText.Text = "加载失败，请稍后重试";
        }
    }

    private void TabDrivers_Click(object? sender, RoutedEventArgs e) => _ = LoadDriversAsync();
    private void TabConstructors_Click(object? sender, RoutedEventArgs e) => _ = LoadConstructorsAsync();

    private void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        _standings.InvalidateCache();
        if (_showingDrivers)
            _ = LoadDriversAsync();
        else
            _ = LoadConstructorsAsync();
    }
}
