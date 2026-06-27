using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.F1Timing.Models.Settings;
using ClassIsland.F1Timing.Services;
using ClassIsland.F1Timing.Views;

namespace ClassIsland.F1Timing.Controls;

/// <summary>
/// F1 计时组件的设置界面。设置页是普通窗口，可作为打开 Timing Tower 的零配置入口。
/// </summary>
public partial class F1TimingComponentSettingsControl : ComponentBase<F1TimingComponentSettings>
{
    private readonly F1TimingService _service;
    private readonly ErgastStandingsService _standings;
    private F1StandingsWindow? _standingsWindow;

    public F1TimingComponentSettingsControl(F1TimingService service, ErgastStandingsService standings)
    {
        _service = service;
        _standings = standings;
        InitializeComponent();
        UpdateSimButton();
    }

    private void DemoIslandBounce_Click(object? sender, RoutedEventArgs e) =>
        _service.DemoIslandBounce();

    private void DemoOvertake_Click(object? sender, RoutedEventArgs e) =>
        _service.DemoOvertake();

    private void DemoFastestLap_Click(object? sender, RoutedEventArgs e) =>
        _service.DemoFastestLap();

    private void OpenTower_Click(object? sender, RoutedEventArgs e) =>
        _service.ShowTower(Settings.TowerTopmost);

    private void OpenStandings_Click(object? sender, RoutedEventArgs e)
    {
        if (_standingsWindow != null)
        {
            _standingsWindow.Activate();
            return;
        }
        _standingsWindow = new F1StandingsWindow(_standings, Settings.TowerTopmost);
        _standingsWindow.Closed += (_, _) => _standingsWindow = null;
        _standingsWindow.Show();
    }

    private void ToggleSim_Click(object? sender, RoutedEventArgs e)
    {
        _service.ToggleSimulation();
        UpdateSimButton();
    }

    private void UpdateSimButton()
    {
        if (SimButton != null)
            SimButton.Content = _service.IsSimulating ? "■ 停止模拟" : "▶ 开始模拟";
    }
}
