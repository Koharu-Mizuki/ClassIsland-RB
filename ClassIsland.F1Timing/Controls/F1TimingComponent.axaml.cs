using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.F1Timing.Helpers;
using ClassIsland.F1Timing.Models;
using ClassIsland.F1Timing.Models.Settings;
using ClassIsland.F1Timing.Services;

namespace ClassIsland.F1Timing.Controls;

/// <summary>
/// 顶栏 F1 排位条：横向跑马灯滚动显示当前排位（位次 + 车队色点 + 车手缩写）。
/// 比赛开始/结束时内容平滑淡入淡出，不硬切。
/// </summary>
[ComponentInfo("E7C98531-5172-4A16-9463-3FBBDBDD9981", "F1 实时计时", "",
    "在顶栏横向滚动显示 F1 当前排位与车队。完整 Timing Tower 通过「打开 F1 计时板」动作或组件设置打开。")]
public partial class F1TimingComponent : ComponentBase<F1TimingComponentSettings>
{
    private readonly F1TimingService _service;
    private readonly DispatcherTimer _scrollTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly TranslateTransform _trans = new();       // 水平滚动（作用于 Track）
    private readonly TranslateTransform _slideTrans = new();  // 弹出动画 Y 偏移（作用于 Viewport）

    // 内容可见性状态机
    private bool _contentVisible;
    private bool _contentAnimRunning;
    private bool _contentDirty;

    /// <summary>供 XAML 绑定的共享快照。</summary>
    public F1SessionSnapshot Snapshot => _service.Snapshot;

    private CancellationTokenSource? _floodCts;

    public F1TimingComponent(F1TimingService service)
    {
        _service = service;
        InitializeComponent();
        Track.RenderTransform = _trans;
        Viewport.RenderTransform = _slideTrans;
        _scrollTimer.Tick += OnScrollTick;
        _service.FastestLapFlood += OnFastestLapFlood;

        AttachedToVisualTree += (_, _) =>
        {
            ApplySettings();
            Snapshot.PropertyChanged += OnSnapshotPropertyChanged;
            if (Settings is INotifyPropertyChanged npc)
                npc.PropertyChanged += OnSettingsPropertyChanged;
            _service.Acquire();
            _scrollTimer.Start();
            RefreshContentVisibility(animate: false);
        };
        DetachedFromVisualTree += (_, _) =>
        {
            Snapshot.PropertyChanged -= OnSnapshotPropertyChanged;
            if (Settings is INotifyPropertyChanged npc)
                npc.PropertyChanged -= OnSettingsPropertyChanged;
            _scrollTimer.Stop();
            _service.Release();
        };
    }

    private void OnFastestLapFlood(string tla, string lapTime)
    {
        // 已在 UI 线程（FireFastestLapFlood 用 Dispatcher.UIThread.Post 发出）
        var mode = Settings?.FastestLapMode ?? 2;
        if (mode is 0 or 2) _ = PlayFastestLapFloodAsync(tla, lapTime);
        if (mode is 1 or 2) _service.SendFastestLapNotification(tla, lapTime);
    }

    private async Task PlayFastestLapFloodAsync(string tla, string lapTime)
    {
        // 取消上一次还在播的 flood
        _floodCts?.Cancel();
        _floodCts = new CancellationTokenSource();
        var cts = _floodCts;

        FastestLapTlaLabel.Text = tla;
        FastestLapTimeLabel.Text = lapTime;
        FastestLapContent.Opacity = 0;
        FastestLapOverlay.Width = 0;
        FastestLapOverlay.IsVisible = true;

        var targetWidth = Viewport.IsVisible ? Viewport.Bounds.Width : 300.0;
        if (targetWidth < 80) targetWidth = 300.0;

        try
        {
            // 1. 紫条从右往左填满
            await new Animation
            {
                Duration = TimeSpan.FromMilliseconds(250),
                Easing = new QuarticEaseOut(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0.0), Setters = { new Setter(Layoutable.WidthProperty, 0.0) } },
                    new KeyFrame { Cue = new Cue(1.0), Setters = { new Setter(Layoutable.WidthProperty, targetWidth) } },
                }
            }.RunAsync(FastestLapOverlay, cts.Token);
            FastestLapOverlay.Width = targetWidth;

            // 2. 内容淡入
            await new Animation
            {
                Duration = TimeSpan.FromMilliseconds(160),
                Easing = new QuarticEaseOut(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0.0), Setters = { new Setter(Visual.OpacityProperty, 0.0) } },
                    new KeyFrame { Cue = new Cue(1.0), Setters = { new Setter(Visual.OpacityProperty, 1.0) } },
                }
            }.RunAsync(FastestLapContent, cts.Token);
            FastestLapContent.Opacity = 1;

            await Task.Delay(3500, cts.Token);

            // 3. 内容淡出
            await new Animation
            {
                Duration = TimeSpan.FromMilliseconds(120),
                Easing = new QuadraticEaseIn(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0.0), Setters = { new Setter(Visual.OpacityProperty, 1.0) } },
                    new KeyFrame { Cue = new Cue(1.0), Setters = { new Setter(Visual.OpacityProperty, 0.0) } },
                }
            }.RunAsync(FastestLapContent, cts.Token);
            FastestLapContent.Opacity = 0;

            // 4. 紫条向右收起
            await new Animation
            {
                Duration = TimeSpan.FromMilliseconds(200),
                Easing = new QuarticEaseIn(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0.0), Setters = { new Setter(Layoutable.WidthProperty, targetWidth) } },
                    new KeyFrame { Cue = new Cue(1.0), Setters = { new Setter(Layoutable.WidthProperty, 0.0) } },
                }
            }.RunAsync(FastestLapOverlay, cts.Token);
        }
        catch (TaskCanceledException) { }
        finally
        {
            if (!cts.IsCancellationRequested)
            {
                FastestLapOverlay.IsVisible = false;
                FastestLapOverlay.Width = 0;
            }
            FastestLapContent.Opacity = 0;
        }
    }

    private void OnSnapshotPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(F1SessionSnapshot.IsLive) or nameof(F1SessionSnapshot.HasDrivers))
            RefreshContentVisibility(animate: true);
    }

    private bool ShouldShowContent() =>
        Snapshot.IsLive || (!Settings.OnlyWhenLive && Snapshot.HasDrivers);

    /// <summary>
    /// 根据当前数据状态决定是否显示内容，并在需要时播放淡入/淡出动画。
    /// 采用 dirty-flag 循环确保动画期间产生的状态变化不会丢失。
    /// </summary>
    private async void RefreshContentVisibility(bool animate)
    {
        _contentDirty = true;
        if (_contentAnimRunning) return;
        _contentAnimRunning = true;

        try
        {
            while (_contentDirty)
            {
                _contentDirty = false;
                var show = ShouldShowContent();
                if (show == _contentVisible) continue;
                _contentVisible = show;

                if (show)
                {
                    // 先设好起始状态再设 IsVisible=true，避免单帧闪烁
                    Viewport.Opacity = 0;
                    Viewport.MaxWidth = 0;
                    _slideTrans.Y = 5;
                    Viewport.IsVisible = true;
                    if (animate)
                    {
                        await Task.WhenAll(
                            new Animation
                            {
                                Duration = TimeSpan.FromMilliseconds(260),
                                Easing = new QuarticEaseOut(),
                                FillMode = FillMode.Forward,
                                Children =
                                {
                                    new KeyFrame { Cue = new Cue(0.0), Setters = { new Setter(Visual.OpacityProperty, 0.0), new Setter(Layoutable.MaxWidthProperty, 0.0) } },
                                    new KeyFrame { Cue = new Cue(1.0), Setters = { new Setter(Visual.OpacityProperty, 1.0), new Setter(Layoutable.MaxWidthProperty, 440.0) } },
                                }
                            }.RunAsync(Viewport),
                            AnimationHelper.InterpolateAsync(v => _slideTrans.Y = v, 5.0, 0.0, 260, AnimationHelper.EaseQuarticOut)
                        );
                        Viewport.Opacity = 1;
                        Viewport.MaxWidth = 440;
                    }
                    else
                    {
                        Viewport.Opacity = 1;
                        Viewport.MaxWidth = 440;
                        _slideTrans.Y = 0;
                    }
                }
                else
                {
                    if (animate)
                    {
                        var startWidth = Viewport.MaxWidth;
                        await new Animation
                        {
                            Duration = TimeSpan.FromMilliseconds(180),
                            Easing = new QuadraticEaseIn(),
                            FillMode = FillMode.Forward,
                            Children =
                            {
                                new KeyFrame { Cue = new Cue(0.0), Setters = { new Setter(Visual.OpacityProperty, 1.0), new Setter(Layoutable.MaxWidthProperty, startWidth) } },
                                new KeyFrame { Cue = new Cue(1.0), Setters = { new Setter(Visual.OpacityProperty, 0.0), new Setter(Layoutable.MaxWidthProperty, 0.0) } },
                            }
                        }.RunAsync(Viewport);
                    }
                    Viewport.IsVisible = false;
                    Viewport.Opacity = 0;
                    Viewport.MaxWidth = 440;
                    _slideTrans.Y = 0;
                }
                animate = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[F1TimingComponent] RefreshContentVisibility: {ex}");
        }
        finally
        {
            _contentAnimRunning = false;
        }
    }

    // ── 设置应用 ─────────────────────────────────────────────────────────────────

    private void ApplySettings()
    {
        _service.ActiveProviderId = Settings.DataSource;
        _service.PollInterval = TimeSpan.FromSeconds(Settings.PollIntervalSeconds);
        _service.TowerTopmost = Settings.TowerTopmost;
        _service.SetOnlyWhenLive(Settings.OnlyWhenLive);
        _service.HighlightDriver = Settings.HighlightDriver;
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(F1TimingComponentSettings.OnlyWhenLive):
                _service.SetOnlyWhenLive(Settings.OnlyWhenLive);
                RefreshContentVisibility(animate: true);
                break;
            case nameof(F1TimingComponentSettings.HighlightDriver):
                _service.HighlightDriver = Settings.HighlightDriver;
                break;
            case nameof(F1TimingComponentSettings.PollIntervalSeconds):
                _service.PollInterval = TimeSpan.FromSeconds(Settings.PollIntervalSeconds);
                break;
            case nameof(F1TimingComponentSettings.TowerTopmost):
                _service.TowerTopmost = Settings.TowerTopmost;
                break;
            case nameof(F1TimingComponentSettings.DataSource):
                if (!_service.IsSimulating)
                    _service.ActiveProviderId = Settings.DataSource;
                break;
        }
    }

    // ── 跑马灯滚动 ───────────────────────────────────────────────────────────────

    // 每帧左移 0.6px；内容宽度超出可视区时滚动，否则重置到原点；两份内容首尾相接实现无缝回绕。
    private void OnScrollTick(object? sender, EventArgs e)
    {
        var single = List1.Bounds.Width;
        var view = Viewport.Bounds.Width;
        if (single <= 1 || single <= view)
        {
            if (_trans.X != 0) _trans.X = 0;
            return;
        }

        var x = _trans.X - 0.6;
        if (-x >= single) x += single;
        _trans.X = x;
    }
}
