using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClassIsland.F1Timing.Models;
using ClassIsland.F1Timing.Views;
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
    private readonly ILogger<F1TimingService>? _logger;
    private readonly object _gate = new();

    private int _consumers;
    private CancellationTokenSource? _cts;
    private string _runningProviderId = "";

    /// <summary>UI 绑定的长期快照实例。</summary>
    public F1SessionSnapshot Snapshot { get; } = new();

    /// <summary>每次成功更新后在 UI 线程触发。</summary>
    public event EventHandler? Updated;

    /// <summary>轮询间隔，默认 5 秒。</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>当前数据源标识。修改后下一轮生效。</summary>
    public string ActiveProviderId { get; set; } = "openf1";

    public F1TimingService(OpenF1DataProvider openF1, LiveTimingDataProvider liveTiming,
        ILogger<F1TimingService>? logger = null)
    {
        _logger = logger;
        _providers = new Dictionary<string, IF1DataProvider>
        {
            [openF1.Id] = openF1,
            [liveTiming.Id] = liveTiming
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
                await Task.Delay(PollInterval, token);
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
}
