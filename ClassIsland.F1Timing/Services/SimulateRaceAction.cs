using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;

namespace ClassIsland.F1Timing.Services;

/// <summary>
/// 「F1 模拟演示开关」动作。每次触发切换模拟比赛的开 / 关，
/// 可绑定到托盘菜单 / 快捷键，方便现场随时调出一场虚拟比赛进行展示。
/// </summary>
[ActionInfo("classisland.f1-timing.simulate", "F1 模拟演示开关", "")]
public class SimulateRaceAction(F1TimingService service) : ActionBase
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        service.ToggleSimulation();
    }
}
