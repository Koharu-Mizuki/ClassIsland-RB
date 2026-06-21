using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;

namespace ClassIsland.F1Timing.Services;

/// <summary>
/// 「打开 F1 计时板」动作。可绑定到托盘菜单 / 信号 / 快捷键等触发器，
/// 绕开顶栏鼠标穿透限制来唤出完整 Timing Tower。
/// </summary>
[ActionInfo("classisland.f1-timing.open-tower", "打开 F1 计时板", "")]
public class OpenF1TowerAction(F1TimingService service) : ActionBase
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        service.ShowTower(service.TowerTopmost);
    }
}
