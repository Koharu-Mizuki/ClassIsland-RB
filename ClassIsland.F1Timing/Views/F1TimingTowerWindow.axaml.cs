using Avalonia.Controls;
using ClassIsland.F1Timing.Services;

namespace ClassIsland.F1Timing.Views;

/// <summary>
/// 完整 Timing Tower 窗口，绑定到共享的会话快照并随后台轮询实时刷新。
/// </summary>
public partial class F1TimingTowerWindow : Window
{
    private readonly F1TimingService? _service;

    // 设计器需要的无参构造
    public F1TimingTowerWindow()
    {
        InitializeComponent();
    }

    public F1TimingTowerWindow(F1TimingService service, bool topmost)
    {
        _service = service;
        InitializeComponent();
        Topmost = topmost;
        DataContext = service.Snapshot;

        Opened += (_, _) => _service?.Acquire();
        Closed += (_, _) => _service?.Release();
    }
}
