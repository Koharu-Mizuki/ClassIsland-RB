using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClassIsland.F1Timing.Models;

namespace ClassIsland.F1Timing.Services;

/// <summary>
/// F1 计时数据源抽象。不同实现（OpenF1 / 官方 LiveTiming）对外提供一致的拉取接口，
/// 由 <see cref="F1TimingService"/> 统一调度。
/// </summary>
public interface IF1DataProvider
{
    /// <summary>数据源唯一标识，用于设置中选择。</summary>
    string Id { get; }

    /// <summary>用于设置界面展示的名称。</summary>
    string DisplayName { get; }

    /// <summary>拉取一帧最新数据。实现可在内部维护增量状态。</summary>
    Task<F1FetchResult> FetchAsync(CancellationToken ct);

    /// <summary>重置内部累积状态（数据源切换或会话变更时调用）。</summary>
    void Reset();
}

/// <summary>
/// 单次拉取的结果。<see cref="Drivers"/> 中的对象仅作数据载体，
/// 由服务层合并进 UI 绑定的快照集合。
/// </summary>
public sealed class F1FetchResult
{
    /// <summary>本次拉取是否成功（失败时通常沿用上一帧 UI）。</summary>
    public bool Ok { get; set; } = true;

    public bool IsLive { get; set; }

    public string StatusMessage { get; set; } = "";

    public string SessionName { get; set; } = "";

    public SessionType SessionType { get; set; } = SessionType.Unknown;

    public string CircuitName { get; set; } = "";

    public string CountryName { get; set; } = "";

    public int CurrentLap { get; set; }

    public int TotalLaps { get; set; }

    public TrackFlag Flag { get; set; } = TrackFlag.None;

    /// <summary>按位次升序排列的车手行。</summary>
    public List<F1DriverTiming> Drivers { get; set; } = new();
}
