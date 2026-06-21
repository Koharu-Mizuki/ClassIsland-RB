using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.F1Timing.Controls;
using ClassIsland.F1Timing.Services;
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
        services.AddSingleton<F1TimingService>();

        // 主界面组件 + 设置界面
        services.AddComponent<F1TimingComponent, F1TimingComponentSettingsControl>();

        // 「打开 F1 计时板」动作（顶栏鼠标穿透，故用动作配合托盘菜单/快捷键等触发器打开 Tower）
        services.AddAction<OpenF1TowerAction>();
    }
}
