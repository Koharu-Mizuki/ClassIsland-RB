using System.Threading;
using System.Threading.Tasks;

namespace ClassIsland.F1Timing.Services;

/// <summary>
/// F1 官方 LiveTiming（SignalR）数据源占位。接口已预留，待后续实现协议解析。
/// 当前选择该源时返回提示信息，引导用户切回 OpenF1。
/// </summary>
public sealed class LiveTimingDataProvider : IF1DataProvider
{
    public string Id => "livetiming";

    public string DisplayName => "F1 官方 LiveTiming（开发中）";

    public void Reset()
    {
    }

    public Task<F1FetchResult> FetchAsync(CancellationToken ct) =>
        Task.FromResult(new F1FetchResult
        {
            Ok = false,
            IsLive = false,
            StatusMessage = "官方 LiveTiming 数据源尚在开发中，请在设置中切换为 OpenF1。"
        });
}
