using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ClassIsland.F1Timing.Helpers;
using ClassIsland.F1Timing.Services;

namespace ClassIsland.F1Timing.Views;

/// <summary>
/// 完整 Timing Tower 窗口，绑定到共享的会话快照并随后台轮询实时刷新。
/// </summary>
public partial class F1TimingTowerWindow : Window
{
    private const double RowHeight = 30.0; // 29px height + 1px margin

    private readonly F1TimingService? _service;
    private bool _isClosing;
    private bool _closingAnimDone;

    // 每行的 TranslateTransform 与动画版本号（版本号用于打断旧动画）
    private readonly Dictionary<Control, TranslateTransform> _rowTransforms = new();
    private readonly Dictionary<Control, int> _rowAnimVersions = new();

    // 设计器需要的无参构造
    public F1TimingTowerWindow()
    {
        InitializeComponent();
    }

    public F1TimingTowerWindow(F1TimingService service, bool topmost)
    {
        _service = service;
        Opacity = 0;  // 隐藏直到 Opened 动画开始，避免瞬间闪出
        InitializeComponent();
        Topmost = topmost;
        DataContext = service.Snapshot;

        Opened += OnWindowOpened;
        Closing += OnWindowClosing;
        Closed += (_, _) =>
        {
            service.Snapshot.Drivers.CollectionChanged -= OnDriversCollectionChanged;
            _service?.Release();
        };

        service.Snapshot.Drivers.CollectionChanged += OnDriversCollectionChanged;
    }

    private void OnDriversCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Move) return;

        var oldIdx = e.OldStartingIndex;
        var newIdx = e.NewStartingIndex;
        var offsetY = (oldIdx - newIdx) * RowHeight;

        // 此时 collection 已移位但 layout 还没跑 — 先拿到容器引用
        var container = DriversList.ContainerFromIndex(newIdx);
        if (container == null) return;

        // 等 layout pass 完成后再设 TranslateTransform（Background 优先级低于 layout）
        Dispatcher.UIThread.Post(
            () => AnimateRowSlide(container, offsetY),
            DispatcherPriority.Background);
    }

    private void AnimateRowSlide(Control container, double offsetY)
    {
        if (!_rowTransforms.TryGetValue(container, out var tt))
        {
            tt = new TranslateTransform();
            _rowTransforms[container] = tt;
            container.RenderTransform = tt;
        }

        // 升版本号，让上一个正在跑的 timer 自行停止
        _rowAnimVersions.TryGetValue(container, out var v);
        v++;
        _rowAnimVersions[container] = v;
        var myVersion = v;

        // 从偏移处（视觉上的旧位置）开始，动画到 0（新位置）
        tt.Y = offsetY;
        var start = DateTime.UtcNow;

        DispatcherTimer timer = null!;
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            if (_rowAnimVersions.GetValueOrDefault(container) != myVersion)
            {
                timer.Stop();
                return;
            }
            var t = Math.Min((DateTime.UtcNow - start).TotalMilliseconds / 320.0, 1.0);
            tt.Y = offsetY * (1.0 - AnimationHelper.EaseQuarticOut(t));
            if (t >= 1.0)
            {
                tt.Y = 0;
                timer.Stop();
            }
        };
        timer.Start();
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        _service?.Acquire();
        await RunOpacityAnimation(from: 0.0, to: 1.0,
            durationMs: 280, easing: new QuarticEaseOut());
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closingAnimDone) return;   // 动画已完成，放行真正的 Close()

        e.Cancel = true;
        if (_isClosing) return;         // 动画进行中，多余的关闭请求直接丢弃

        _isClosing = true;
        await RunOpacityAnimation(from: 1.0, to: 0.0,
            durationMs: 180, easing: new QuadraticEaseIn());
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
}
