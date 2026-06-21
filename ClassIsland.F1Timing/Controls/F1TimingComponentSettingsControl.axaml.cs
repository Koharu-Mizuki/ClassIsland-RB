using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.F1Timing.Models.Settings;
using ClassIsland.F1Timing.Services;

namespace ClassIsland.F1Timing.Controls;

/// <summary>
/// F1 计时组件的设置界面。设置页是普通窗口，可作为打开 Timing Tower 的零配置入口。
/// </summary>
public partial class F1TimingComponentSettingsControl : ComponentBase<F1TimingComponentSettings>
{
    private readonly F1TimingService _service;

    public F1TimingComponentSettingsControl(F1TimingService service)
    {
        _service = service;
        InitializeComponent();
    }

    private void OpenTower_Click(object? sender, RoutedEventArgs e) =>
        _service.ShowTower(Settings.TowerTopmost);
}
