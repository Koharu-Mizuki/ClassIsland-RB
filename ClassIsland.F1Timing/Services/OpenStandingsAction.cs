using System.Threading.Tasks;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.F1Timing.Views;
using ClassIsland.Shared;

namespace ClassIsland.F1Timing.Services;

/// <summary>
/// 自动化动作：打开 F1 积分榜窗口。
/// </summary>
[ActionInfo("classisland.f1-timing.open-standings", "打开 F1 积分榜", "")]
public class OpenStandingsAction : ActionBase
{
    private F1StandingsWindow? _window;

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (_window != null)
                {
                    _window.Activate();
                    return;
                }
                var standings = IAppHost.TryGetService<ErgastStandingsService>();
                if (standings == null) return;
                var svc = IAppHost.TryGetService<F1TimingService>();
                _window = new F1StandingsWindow(standings, svc?.TowerTopmost ?? false);
                _window.Closed += (_, _) => _window = null;
                _window.Show();
            }
            catch
            {
                // 忽略打开失败
            }
        });
    }
}
