using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using System.Reactive.Linq;

namespace ClassIsland.F1Timing.Controls;

/// <summary>
/// 斜切蒙版控件 —— 将宽度分为5块平行四边形，从两侧向中央依次填入/收起（Open/Close）。
/// 复制自 ClassIsland.Controls.SlantedMaskControl，并添加可等待的 OpenAsync / CloseAsync。
/// </summary>
public class SlantedMaskControl : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<SlantedMaskControl, IBrush?>(nameof(Fill), Brushes.Black);

    public static readonly StyledProperty<double> SlantedHeightProperty =
        AvaloniaProperty.Register<SlantedMaskControl, double>(nameof(SlantedHeight), 80.0);

    public static readonly StyledProperty<int> RegionDurationMsProperty =
        AvaloniaProperty.Register<SlantedMaskControl, int>(nameof(RegionDurationMs), 300);

    public static readonly StyledProperty<int> StageStaggerMsProperty =
        AvaloniaProperty.Register<SlantedMaskControl, int>(nameof(StageStaggerMs), 120);

    public static readonly StyledProperty<double> Region0ProgressProperty =
        AvaloniaProperty.Register<SlantedMaskControl, double>(nameof(Region0Progress), 0.0);

    public static readonly StyledProperty<double> Region1ProgressProperty =
        AvaloniaProperty.Register<SlantedMaskControl, double>(nameof(Region1Progress), 0.0);

    public static readonly StyledProperty<double> Region2ProgressProperty =
        AvaloniaProperty.Register<SlantedMaskControl, double>(nameof(Region2Progress), 0.0);

    public static readonly StyledProperty<double> Region3ProgressProperty =
        AvaloniaProperty.Register<SlantedMaskControl, double>(nameof(Region3Progress), 0.0);

    public static readonly StyledProperty<double> Region4ProgressProperty =
        AvaloniaProperty.Register<SlantedMaskControl, double>(nameof(Region4Progress), 0.0);

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public double SlantedHeight { get => GetValue(SlantedHeightProperty); set => SetValue(SlantedHeightProperty, value); }
    public int RegionDurationMs { get => GetValue(RegionDurationMsProperty); set => SetValue(RegionDurationMsProperty, value); }
    public int StageStaggerMs { get => GetValue(StageStaggerMsProperty); set => SetValue(StageStaggerMsProperty, value); }
    public double Region0Progress { get => GetValue(Region0ProgressProperty); set => SetValue(Region0ProgressProperty, value); }
    public double Region1Progress { get => GetValue(Region1ProgressProperty); set => SetValue(Region1ProgressProperty, value); }
    public double Region2Progress { get => GetValue(Region2ProgressProperty); set => SetValue(Region2ProgressProperty, value); }
    public double Region3Progress { get => GetValue(Region3ProgressProperty); set => SetValue(Region3ProgressProperty, value); }
    public double Region4Progress { get => GetValue(Region4ProgressProperty); set => SetValue(Region4ProgressProperty, value); }

    private CancellationTokenSource? _cts;

    public SlantedMaskControl()
    {
        Region0Progress = Region1Progress = Region2Progress = Region3Progress = Region4Progress = 0.0;

        this.GetObservable(Region0ProgressProperty).Subscribe(_ => InvalidateVisual());
        this.GetObservable(Region1ProgressProperty).Subscribe(_ => InvalidateVisual());
        this.GetObservable(Region2ProgressProperty).Subscribe(_ => InvalidateVisual());
        this.GetObservable(Region3ProgressProperty).Subscribe(_ => InvalidateVisual());
        this.GetObservable(Region4ProgressProperty).Subscribe(_ => InvalidateVisual());
    }

    public Task OpenAsync() => StartSequence(open: true);
    public Task CloseAsync() => StartSequence(open: false);

    private async Task StartSequence(bool open)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        int dur = Math.Max(1, RegionDurationMs);
        int stag = Math.Max(0, StageStaggerMs);

        try
        {
            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(stag + dur),
                FillMode = FillMode.Forward,
                Easing = open ? new QuarticEaseOut() : new QuadraticEaseIn()
            };

            if (open)
            {
                Region0Progress = Region1Progress = Region2Progress = Region3Progress = Region4Progress = 0;
                animation.Children.AddRange(BuildKeyframes(0, 0, dur, true));
                animation.Children.AddRange(BuildKeyframes(4, 0, dur, true));
                animation.Children.AddRange(BuildKeyframes(1, (int)(stag * 0.75), dur, true));
                animation.Children.AddRange(BuildKeyframes(3, (int)(stag * 0.75), dur, true));
                animation.Children.AddRange(BuildKeyframes(2, stag, dur, true));
            }
            else
            {
                Region0Progress = Region1Progress = Region2Progress = Region3Progress = Region4Progress = 1;
                animation.Children.AddRange(BuildKeyframes(2, 0, dur, false));
                animation.Children.AddRange(BuildKeyframes(1, (int)(stag * 0.75), dur, false));
                animation.Children.AddRange(BuildKeyframes(3, (int)(stag * 0.75), dur, false));
                animation.Children.AddRange(BuildKeyframes(0, stag, dur, false));
                animation.Children.AddRange(BuildKeyframes(4, stag, dur, false));
            }

            await animation.RunAsync(this, token);
        }
        catch (TaskCanceledException) { }
        catch (OperationCanceledException) { }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                double finish = open ? 1.0 : 0.0;
                Region0Progress = Region1Progress = Region2Progress = Region3Progress = Region4Progress = finish;
            }
        }
    }

    private static IList<KeyFrame> BuildKeyframes(int regionIndex, int delayMs, int durationMs, bool forward)
    {
        var prop = GetRegionProperty(regionIndex);
        double from = forward ? 0.0 : 1.0;
        double to = forward ? 1.0 : 0.0;
        return
        [
            new KeyFrame { KeyTime = TimeSpan.FromMilliseconds(delayMs), Setters = { new Setter(prop, from) } },
            new KeyFrame { KeyTime = TimeSpan.FromMilliseconds(delayMs + durationMs), Setters = { new Setter(prop, to) } },
        ];
    }

    private static AvaloniaProperty<double> GetRegionProperty(int idx) => idx switch
    {
        0 => Region0ProgressProperty,
        1 => Region1ProgressProperty,
        2 => Region2ProgressProperty,
        3 => Region3ProgressProperty,
        4 => Region4ProgressProperty,
        _ => throw new ArgumentOutOfRangeException(nameof(idx))
    };

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var size = Bounds;
        if (size.Width <= 0 || size.Height <= 0) return;

        double h = SlantedHeight;
        if (h <= 0 || h > size.Height) h = size.Height;
        double offset = Math.Tan(Math.PI / 6.0) * h;
        double totalW = size.Width + offset;

        double[] p =
        [
            Math.Clamp(Region0Progress, 0.0, 1.0),
            Math.Clamp(Region1Progress, 0.0, 1.0),
            Math.Clamp(Region2Progress, 0.0, 1.0),
            Math.Clamp(Region3Progress, 0.0, 1.0),
            Math.Clamp(Region4Progress, 0.0, 1.0),
        ];
        double[] weight = [0.0, 0.1, 0.2, 0.4, 0.2, 0.1];
        double[] startX = new double[6];
        startX[0] = 0;
        for (int i = 1; i <= 5; i++)
            startX[i] = startX[i - 1] + totalW * weight[i];

        var brush = Fill ?? Brushes.Black;

        for (int i = 0; i < 5; i++)
        {
            double prog = p[i];
            if (prog < 0.0001) continue;

            double rx0 = startX[i];
            double rx1 = startX[i + 1];
            double rCenter = (rx0 + rx1) / 2.0;
            double fullW = totalW * weight[i + 1] + 0.5;
            double currentW = fullW * prog;
            if (currentW < 0.0001) continue;

            var p1 = new Point(rCenter - currentW / 2.0, 0);
            var p2 = new Point(rCenter + currentW / 2.0, 0);
            var p3 = new Point(rCenter + currentW / 2.0 - offset, h);
            var p4 = new Point(rCenter - currentW / 2.0 - offset, h);

            var geom = new StreamGeometry();
            using (var g = geom.Open())
            {
                g.BeginFigure(p1, isFilled: true);
                g.LineTo(p2);
                g.LineTo(p3);
                g.LineTo(p4);
                g.EndFigure(isClosed: true);
            }
            context.DrawGeometry(brush, null, geom);
        }
    }
}
