using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.F1Timing.Controls;
using ClassIsland.F1Timing.Services;
using ClassIsland.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassIsland.F1Timing;

[PluginEntrance]
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 数据源与后台轮询服务（单例，组件与窗口共享）
        services.AddSingleton<OpenF1DataProvider>();
        services.AddSingleton<LiveTimingDataProvider>();
        services.AddSingleton<SimulatedF1DataProvider>();
        services.AddSingleton<F1TimingService>();
        // 随主机启动立即开始轮询，无需等待 UI 组件
        services.AddHostedService<F1TimingBackgroundService>();

        // F1 赛况通知提供方
        services.AddNotificationProvider<F1NotificationProvider, F1NotificationProviderSettingsControl>();

        // 积分榜服务
        services.AddSingleton<ErgastStandingsService>();

        // 赛程服务
        services.AddSingleton<F1CalendarService>();

        // 主界面组件 + 设置界面
        services.AddComponent<F1TimingComponent, F1TimingComponentSettingsControl>();

        // 「打开 F1 计时板」动作（顶栏鼠标穿透，故用动作配合托盘菜单/快捷键等触发器打开 Tower）
        services.AddAction<OpenF1TowerAction>();

        // 「F1 模拟演示开关」动作：随时一键调出/收起模拟比赛，便于现场展示（可绑托盘菜单/快捷键）
        services.AddAction<SimulateRaceAction>();

        // 「打开 F1 积分榜」动作
        services.AddAction<OpenStandingsAction>();

        // 灵动岛隐藏规则：当前无可显示数据时成立。把此规则配到 F1 组件的隐藏规则上
        // （HideOnRule=true），即可让独占行在没数据时灵动收起、有数据时灵动弹出。
        services.AddRule("classisland.f1-timing.island-hidden", "F1 灵动岛：当前无可显示数据", "",
            _ =>
            {
                var svc = IAppHost.TryGetService<F1TimingService>();
                return svc is null || !svc.IslandVisible;
            });
    }
}
