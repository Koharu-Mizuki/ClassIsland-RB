using System;
using Avalonia.Media;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.F1Timing.Models;
using ClassIsland.F1Timing.Models.Settings;
using ClassIsland.F1Timing.Services;

namespace ClassIsland.F1Timing.Controls;

/// <summary>
/// 顶栏 F1 排位条：左侧 F1 logo + 横向跑马灯滚动显示当前排位（位次 + 车队色点 + 车手缩写）。
/// 信息常驻顶栏、一眼可见，无需点击；完整 Timing Tower 通过「打开 F1 计时板」动作或设置页按钮打开。
/// </summary>
[ComponentInfo("E7C98531-5172-4A16-9463-3FBBDBDD9981", "F1 实时计时", "",
    "在顶栏横向滚动显示 F1 当前排位与车队。完整 Timing Tower 通过「打开 F1 计时板」动作或组件设置打开。")]
public partial class F1TimingComponent : ComponentBase<F1TimingComponentSettings>
{
    private readonly F1TimingService _service;
    private readonly DispatcherTimer _scrollTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly TranslateTransform _trans = new();

    /// <summary>供 XAML 绑定的共享快照。</summary>
    public F1SessionSnapshot Snapshot => _service.Snapshot;

    public F1TimingComponent(F1TimingService service)
    {
        _service = service;
        InitializeComponent();
        Track.RenderTransform = _trans;
        _scrollTimer.Tick += OnScrollTick;

        AttachedToVisualTree += (_, _) =>
        {
            ApplySettings();
            _service.Acquire();
            _scrollTimer.Start();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _scrollTimer.Stop();
            _service.Release();
        };
    }

    private void ApplySettings()
    {
        _service.ActiveProviderId = Settings.DataSource;
        _service.PollInterval = TimeSpan.FromSeconds(Settings.PollIntervalSeconds);
        _service.TowerTopmost = Settings.TowerTopmost;
    }

    // 跑马灯：内容宽于可视区时向左滚动，滚过一份宽度即回绕（两份内容首尾相接，视觉无缝）。
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
