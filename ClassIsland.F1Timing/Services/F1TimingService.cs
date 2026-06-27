using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Models.Notification;
using ClassIsland.F1Timing.Models;
using ClassIsland.F1Timing.Views;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;

namespace ClassIsland.F1Timing.Services;

/// <summary>
/// 计时数据的后台轮询服务（单例）。顶栏组件与 Timing Tower 窗口通过
/// <see cref="Acquire"/> / <see cref="Release"/> 引用计数共享同一份 <see cref="Snapshot"/>，
/// 仅在有消费者时轮询，避免空闲时持续请求。
/// </summary>
public sealed class F1TimingService
{
    private readonly Dictionary<string, IF1DataProvider> _providers;
    private F1NotificationProvider? _notifier => F1NotificationProvider.Instance;
    private readonly F1CalendarService? _calendar;
    private readonly ILogger<F1TimingService>? _logger;
    private readonly object _gate = new();

    private int _consumers;
    private CancellationTokenSource? _cts;
    private string _runningProviderId = "";

    /// <summary>UI 绑定的长期快照实例。</summary>
    public F1SessionSnapshot Snapshot { get; } = new();

    /// <summary>每次成功更新后在 UI 线程触发。</summary>
    public event EventHandler? Updated;

    /// <summary>最快圈刷新时在 UI 线程触发，参数为 (tla, lapTime)。供 F1TimingComponent 在组件行内播放紫色 flood。</summary>
    public event Action<string, string>? FastestLapFlood;

    /// <summary>轮询间隔，默认 5 秒。</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>当前数据源标识。修改后下一轮生效。</summary>
    public string ActiveProviderId { get; set; } = "openf1";

    /// <summary>「仅在比赛时显示」。由组件设置同步，决定灵动岛弹出条件（仅直播 / 含上一场 replay）。</summary>
    public bool OnlyWhenLive { get; private set; }

    private string _highlightDriver = "";
    /// <summary>关注车手的三字母缩写。由组件设置同步。切换车手时重置事件追踪，避免误报。</summary>
    public string HighlightDriver
    {
        get => _highlightDriver;
        set
        {
            var v = value ?? "";
            if (_highlightDriver == v)
                return;
            _highlightDriver = v;
            // 换关注对象后，旧车手的位次/进站基准不再适用，清零以免新车手首帧误弹通知
            _prevHighlightPos = 0;
            _prevHighlightPits = 0;
        }
    }

    /// <summary>
    /// 灵动岛是否应当弹出（有可显示数据）。供隐藏规则
    /// <c>classisland.f1-timing.island-hidden</c> 取反使用：此值为 false 时规则成立 → 整行灵动收起。
    /// </summary>
    public bool IslandVisible { get; private set; }

    private IRulesetService? _ruleset;
    private IRulesetService? Ruleset => _ruleset ??= IAppHost.TryGetService<IRulesetService>();

    /// <summary>当前是否处于模拟演示模式。</summary>
    public bool IsSimulating { get; private set; }

    private bool _demoHideIsland;

    private string _providerBeforeSim = "openf1";

    /// <summary>上一帧的旗帜状态，用于检测变化并触发通知。</summary>
    private TrackFlag _prevFlag = TrackFlag.None;
    /// <summary>上一帧关注车手的位次，用于检测超车。</summary>
    private int _prevHighlightPos;
    /// <summary>上一帧关注车手的进站次数。</summary>
    private int _prevHighlightPits;
    /// <summary>上一帧全场最快圈持有者。</summary>
    private string _prevFastestTla = "";

    /// <summary>
    /// 开关模拟演示。开启时把数据源切到内置模拟引擎并加速轮询，关闭时还原原数据源。
    /// 切换后立即重启轮询循环以即时生效（适合现场演示）。
    /// </summary>
    public void SetSimulation(bool on)
    {
        if (on == IsSimulating)
            return;
        if (on)
        {
            _providerBeforeSim = ActiveProviderId;
            ActiveProviderId = "simulated";
        }
        else
        {
            ActiveProviderId = _providerBeforeSim;
        }
        IsSimulating = on;
        lock (_gate)
        {
            if (_consumers > 0)
            {
                StopLoop();
                StartLoop();
            }
        }
        Updated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>切换模拟演示开关。</summary>
    public void ToggleSimulation() => SetSimulation(!IsSimulating);

    /// <summary>同步组件的「仅在比赛时显示」设置，并立即重算灵动岛可见性。</summary>
    public void SetOnlyWhenLive(bool value)
    {
        if (OnlyWhenLive == value)
            return;
        OnlyWhenLive = value;
        RecomputeIslandVisibility();
    }

    /// <summary>
    /// 重算灵动岛可见性：直播中、或（常驻模式且有车手数据）时弹出。
    /// 状态变化时通知规则服务重新判定，从而驱动整行灵动弹出/收起。
    /// </summary>
    /// <summary>触发最快圈 flood（fire event，由 F1TimingComponent 根据设置决定 overlay/通知/双显）。</summary>
    public void FireFastestLapFlood(string tla, string lapTime)
    {
        Dispatcher.UIThread.Post(() => FastestLapFlood?.Invoke(tla, lapTime));
    }

    /// <summary>发送全局 TwoIconsMask 紫色通知（供组件按设置选择性调用）。</summary>
    public void SendFastestLapNotification(string tla, string lapTime)
    {
        if (_notifier == null) return;
        try
        {
            var text = string.IsNullOrEmpty(lapTime) ? $"FASTEST LAP\n{tla}" : $"FASTEST LAP\n{tla}  {lapTime}";
            _notifier.ShowFastestLapNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask(
                    text, hasRightIcon: false, factory: x =>
                    {
                        x.Duration = TimeSpan.FromSeconds(5);
                        x.IsSpeechEnabled = false;
                        x.Color = new Avalonia.Media.SolidColorBrush(
                            Avalonia.Media.Color.Parse("#B14BE0"));
                    })
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "发送最快圈通知失败");
        }
    }

    /// <summary>演示最快圈 flood 动画。</summary>
    public void DemoFastestLap()
    {
        var tla = Snapshot.FastestLapTla;
        var time = Snapshot.FastestLapTime;
        if (string.IsNullOrEmpty(tla))
            tla = Snapshot.Drivers.FirstOrDefault()?.Tla ?? "VER";
        FireFastestLapFlood(tla, time);
    }

    /// <summary>临时隐藏灵动岛并在 delayMs 后重新弹出，用于演示弹出动画。</summary>
    public void DemoIslandBounce(int delayMs = 600)
    {
        _demoHideIsland = true;
        RecomputeIslandVisibility();
        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(delayMs);
            _demoHideIsland = false;
            RecomputeIslandVisibility();
        });
    }

    /// <summary>交换 P1 和 P2 位置，在 Timing Tower 中触发行滑动动画。</summary>
    public void DemoOvertake()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Snapshot.Drivers.Count < 2) return;
            var d0 = Snapshot.Drivers[0];
            var d1 = Snapshot.Drivers[1];
            (d0.Position, d1.Position) = (d1.Position, d0.Position);
            Snapshot.Drivers.Move(1, 0);
        });
    }

    private void RecomputeIslandVisibility()
    {
        var v = !_demoHideIsland && (Snapshot.IsLive || (!OnlyWhenLive && Snapshot.HasDrivers));
        if (v == IslandVisible)
            return;
        IslandVisible = v;
        Ruleset?.NotifyStatusChanged();
    }

    public F1TimingService(OpenF1DataProvider openF1, LiveTimingDataProvider liveTiming,
        SimulatedF1DataProvider simulated, F1CalendarService? calendar = null,
        ILogger<F1TimingService>? logger = null)
    {
        _logger = logger;
        _calendar = calendar;
        _providers = new Dictionary<string, IF1DataProvider>
        {
            [openF1.Id] = openF1,
            [liveTiming.Id] = liveTiming,
            [simulated.Id] = simulated
        };
    }

    public IReadOnlyCollection<IF1DataProvider> Providers => _providers.Values;

    private IF1DataProvider ActiveProvider =>
        _providers.TryGetValue(ActiveProviderId, out var p) ? p : _providers["openf1"];

    private F1TimingTowerWindow? _tower;

    /// <summary>无组件场景（Action 等）打开 Tower 时使用的默认置顶设置；组件挂载时会同步用户设置。</summary>
    public bool TowerTopmost { get; set; } = true;

    /// <summary>打开（或激活已存在的）Timing Tower 窗口。多入口共享单一窗口实例。</summary>
    public void ShowTower(bool topmost)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (_tower != null)
                {
                    _tower.Activate();
                    return;
                }
                _tower = new F1TimingTowerWindow(this, topmost);
                _tower.Closed += (_, _) => _tower = null;
                _tower.Show();
                _logger?.LogInformation("F1 Timing Tower 已打开");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "打开 F1 Timing Tower 失败");
            }
        });
    }

    /// <summary>登记一个消费者，必要时启动轮询。</summary>
    public void Acquire()
    {
        lock (_gate)
        {
            _consumers++;
            if (_consumers == 1)
                StartLoop();
        }
    }

    /// <summary>注销一个消费者，无消费者时停止轮询。</summary>
    public void Release()
    {
        lock (_gate)
        {
            _consumers = Math.Max(0, _consumers - 1);
            if (_consumers == 0)
                StopLoop();
        }
    }

    private void StartLoop()
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(() => LoopAsync(token), token);
    }

    private void StopLoop()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        catch
        {
            // 忽略停止过程中的异常
        }
        finally
        {
            _cts = null;
        }
    }

    private async Task LoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var provider = ActiveProvider;
                if (provider.Id != _runningProviderId)
                {
                    provider.Reset();
                    _runningProviderId = provider.Id;
                }

                var result = await provider.FetchAsync(token);
                await Dispatcher.UIThread.InvokeAsync(() => Apply(result));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "F1 计时数据轮询失败");
            }

            try
            {
                await Task.Delay(IsSimulating ? TimeSpan.FromSeconds(1.2) : PollInterval, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>在 UI 线程把拉取结果合并进快照。</summary>
    private void Apply(F1FetchResult result)
    {
        Snapshot.SessionName = result.SessionName;
        Snapshot.SessionType = result.SessionType;
        Snapshot.CircuitName = result.CircuitName;
        Snapshot.CountryName = result.CountryName;
        Snapshot.CurrentLap = result.CurrentLap;
        Snapshot.TotalLaps = result.TotalLaps;
        Snapshot.Flag = result.Flag;
        Snapshot.IsLive = result.IsLive;
        Snapshot.StatusMessage = result.StatusMessage;
        Snapshot.LastUpdated = DateTime.Now;

        MergeDrivers(result.Drivers);
        Snapshot.HasData = Snapshot.Drivers.Count > 0;
        Snapshot.HasDrivers = Snapshot.Drivers.Count > 0;

        Snapshot.LeaderTla = Snapshot.Drivers.FirstOrDefault(d => d.Position == 1)?.Tla
                             ?? Snapshot.Drivers.FirstOrDefault()?.Tla ?? "";

        if (result.HasWeather && result.Weather != null)
        {
            Snapshot.Weather.CopyFrom(result.Weather);
            Snapshot.HasWeather = true;
        }

        Snapshot.FastestLapTla = result.FastestLapTla;
        Snapshot.FastestLapTime = result.FastestLapTime;

        // P6: 赛程倒计时
        if (_calendar != null)
        {
            var (name, ts, label) = _calendar.GetNextSessionCountdown();
            Snapshot.NextRaceName = name;
            Snapshot.NextSessionLabel = label;
            Snapshot.CountdownText = F1CalendarService.FormatCountdown(ts);
            Snapshot.HasCountdown = !string.IsNullOrEmpty(name);
        }

        foreach (var m in result.NewMessages)
            Snapshot.RaceControl.Insert(0, m);
        const int raceControlCap = 60;
        while (Snapshot.RaceControl.Count > raceControlCap)
            Snapshot.RaceControl.RemoveAt(Snapshot.RaceControl.Count - 1);

        RecomputeIslandVisibility();
        DetectAndNotify(result);
        Updated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>按车手号对齐，原地更新 / 新增 / 重排 / 移除，避免整表重建。</summary>
    private void MergeDrivers(List<F1DriverTiming> target)
    {
        for (var i = 0; i < target.Count; i++)
        {
            var row = target[i];
            var cur = Snapshot.Drivers.FirstOrDefault(d => d.DriverNumber == row.DriverNumber);
            if (cur == null)
            {
                cur = new F1DriverTiming
                {
                    DriverNumber = row.DriverNumber,
                    Tla = row.Tla,
                    FullName = row.FullName,
                    TeamName = row.TeamName,
                    TeamColour = row.TeamColour
                };
                cur.UpdateDynamicFrom(row);
                Snapshot.Drivers.Insert(Math.Min(i, Snapshot.Drivers.Count), cur);
            }
            else
            {
                cur.UpdateDynamicFrom(row);
                var idx = Snapshot.Drivers.IndexOf(cur);
                if (idx != i && i < Snapshot.Drivers.Count)
                    Snapshot.Drivers.Move(idx, i);
            }
        }

        var keep = target.Select(r => r.DriverNumber).ToHashSet();
        for (var i = Snapshot.Drivers.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(Snapshot.Drivers[i].DriverNumber))
                Snapshot.Drivers.RemoveAt(i);
        }
    }

    /// <summary>
    /// 检测旗帜跳变、关注车手事件，并通过通知提供方发送提醒。
    /// P2 + P7 的核心逻辑。
    /// </summary>
    private void DetectAndNotify(F1FetchResult result)
    {
        if (_notifier == null) return;

        // P2: 旗帜/安全车状态变化通知
        var curFlag = Snapshot.Flag;
        if (curFlag != _prevFlag && _prevFlag != TrackFlag.None)
        {
            var (title, msg) = curFlag switch
            {
                TrackFlag.SafetyCar => ("安全车出动", "🟡 SAFETY CAR — 注意减速"),
                TrackFlag.VirtualSafetyCar => ("虚拟安全车", "🟡 VSC — 注意减速"),
                TrackFlag.Red => ("红旗", "🔴 红旗！比赛暂停"),
                TrackFlag.Yellow => ("黄旗", "⚠️ 黄旗 — 注意前方"),
                TrackFlag.DoubleYellow => ("双黄旗", "⚠️ 双黄旗 — 准备减速"),
                TrackFlag.Green when _prevFlag is TrackFlag.SafetyCar or TrackFlag.VirtualSafetyCar or TrackFlag.Red
                    => ("比赛恢复", "🟢 绿旗 — 比赛恢复！"),
                TrackFlag.Chequered => ("方格旗", "🏁 方格旗！"),
                _ => ("", "")
            };
            if (!string.IsNullOrEmpty(title))
            {
                var catColor = curFlag switch
                {
                    TrackFlag.SafetyCar or TrackFlag.VirtualSafetyCar => "#FF8C00",
                    TrackFlag.Red => "#E60000",
                    TrackFlag.Yellow or TrackFlag.DoubleYellow => "#FFD12E",
                    TrackFlag.Green => "#2ECC40",
                    TrackFlag.Chequered => "#BBBBBB",
                    _ => "#888888"
                };
                try
                {
                    _notifier.ShowFlagNotification(new NotificationRequest
                    {
                        MaskContent = NotificationContent.CreateTwoIconsMask(
                            msg, hasRightIcon: false, factory: x =>
                            {
                                x.Duration = TimeSpan.FromSeconds(4);
                                x.IsSpeechEnabled = true;
                                x.SpeechContent = title;
                                x.Color = new Avalonia.Media.SolidColorBrush(
                                    Avalonia.Media.Color.Parse(catColor));
                            })
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "发送 F1 旗帜通知失败");
                }
            }
        }
        _prevFlag = curFlag;

        // 全场最快圈变化 → 紫色蒙版通知（不限关注车手）
        if (!string.IsNullOrEmpty(result.FastestLapTla) &&
            !string.Equals(result.FastestLapTla, _prevFastestTla, StringComparison.OrdinalIgnoreCase))
        {
            FireFastestLapFlood(result.FastestLapTla, result.FastestLapTime);
        }

        // P7: 关注车手事件检测
        var hl = HighlightDriver;
        if (!string.IsNullOrEmpty(hl))
        {
            var driver = Snapshot.Drivers.FirstOrDefault(d =>
                string.Equals(d.Tla, hl, StringComparison.OrdinalIgnoreCase));
            if (driver != null)
            {
                // 超车检测
                if (_prevHighlightPos > 0 && driver.Position != _prevHighlightPos)
                {
                    var dir = driver.Position < _prevHighlightPos ? "上升" : "下降";
                    var arrow = driver.Position < _prevHighlightPos ? "⬆️" : "⬇️";
                    SendEventNotification($"{arrow} {hl} {dir}",
                        $"{hl} 从 P{_prevHighlightPos} → P{driver.Position}");
                }
                // 进站检测
                if (_prevHighlightPits > 0 && driver.PitStops > _prevHighlightPits)
                {
                    SendEventNotification($"🔧 {hl} 进站",
                        $"{hl} 完成第 {driver.PitStops} 次进站");
                }
                _prevHighlightPos = driver.Position;
                _prevHighlightPits = driver.PitStops;
            }
        }
        _prevFastestTla = result.FastestLapTla;
    }

    private void SendEventNotification(string title, string message)
    {
        try
        {
            _notifier?.ShowEventNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask(
                    message, hasRightIcon: false, factory: x =>
                    {
                        x.Duration = TimeSpan.FromSeconds(3);
                        x.IsSpeechEnabled = false;
                    })
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "发送 F1 事件通知失败: {Title}", title);
        }
    }
}
