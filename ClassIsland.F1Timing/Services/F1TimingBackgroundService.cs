using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace ClassIsland.F1Timing.Services;

/// <summary>
/// 随 ClassIsland 主机启动/停止的后台消费者。
/// 调用 <see cref="F1TimingService.Acquire"/> 确保轮询随软件启动立即开始，
/// 无需等待顶栏组件或 Tower 窗口打开。
/// </summary>
public sealed class F1TimingBackgroundService : IHostedService
{
    private readonly F1TimingService _service;

    public F1TimingBackgroundService(F1TimingService service) => _service = service;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _service.Acquire();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _service.Release();
        return Task.CompletedTask;
    }
}
