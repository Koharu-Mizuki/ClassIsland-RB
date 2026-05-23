using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.OcrAiTimetable.Models;
using ClassIsland.OcrAiTimetable.Services;
using ClassIsland.Shared;

namespace ClassIsland.OcrAiTimetable.Views.SettingsPages;

// 供 AXAML x:Static 绑定使用的枚举引用辅助类
public static class AiProviderRef
{
    public static AiProvider OpenAI => AiProvider.OpenAI;
    public static AiProvider Anthropic => AiProvider.Anthropic;
    public static AiProvider Google => AiProvider.Google;
    public static AiProvider DeepSeek => AiProvider.DeepSeek;
    public static AiProvider Kimi => AiProvider.Kimi;
    public static AiProvider Custom => AiProvider.Custom;
}

[SettingsPageInfo("classisland.ocr-ai-import.settings", "OCR 课表导入")]
public partial class OcrAiSettingsPage : SettingsPageBase
{
    private OcrAiSettingsService SettingsService { get; } =
        IAppHost.GetService<OcrAiSettingsService>();

    public OcrAiSettings Settings => SettingsService.Settings;

    public OcrAiSettingsPage()
    {
        DataContext = this;
        InitializeComponent();
    }

    private void ButtonSave_OnClick(object? sender, RoutedEventArgs e)
    {
        SettingsService.Save();
    }
}
