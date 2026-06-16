using Avalonia.Media.Imaging;
using ClassIsland.Models.OcrAiTimetable;
using ClassIsland.Services;
using ClassIsland.Shared;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Controls.ProfileTransferProviders;

/// <summary>
/// <see cref="OcrAiImportProvider"/> 的视图模型，承载界面临时状态。
/// AI 服务商等持久化配置直接绑定到 <see cref="Services.SettingsService"/> 上的设置对象。
/// </summary>
public partial class OcrAiImportViewModel : ObservableObject
{
    /// <summary>
    /// 设置服务，供 XAML 双向绑定 <c>Settings.OcrAiTimetable.*</c> 配置项。
    /// </summary>
    public SettingsService SettingsService { get; } = IAppHost.GetService<SettingsService>();

    /// <summary>
    /// 选中的课表图片路径。
    /// </summary>
    [ObservableProperty] private string _sourceImagePath = "";

    /// <summary>
    /// 图片预览。
    /// </summary>
    [ObservableProperty] private Bitmap? _imagePreview;

    /// <summary>
    /// 是否正在识别中。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRecognize))]
    private bool _isRecognizing;

    /// <summary>
    /// 是否已有可导入的识别结果。
    /// </summary>
    [ObservableProperty] private bool _hasResult;

    /// <summary>
    /// 识别结果摘要文本。
    /// </summary>
    [ObservableProperty] private string _resultSummary = "";

    /// <summary>
    /// 导入目标：0 = 合并到当前档案，1 = 导入到新档案。
    /// </summary>
    [ObservableProperty] private int _importType;

    /// <summary>
    /// 新档案名称（<see cref="ImportType"/> 为 1 时使用）。
    /// </summary>
    [ObservableProperty] private string _newProfileName = "";

    /// <summary>
    /// 服务商下拉选择的索引，映射到 <see cref="OcrAiProvider"/>。
    /// </summary>
    public int ProviderIndex
    {
        get => (int)SettingsService.Settings.OcrAiTimetable.Provider;
        set
        {
            if (value < 0)
            {
                return;
            }
            SettingsService.Settings.OcrAiTimetable.Provider = (OcrAiProvider)value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCustomEndpoint));
        }
    }

    /// <summary>
    /// 当前是否为「OpenAI 兼容」自定义端点（用于控制自定义地址输入框可见性）。
    /// </summary>
    public bool IsCustomEndpoint => SettingsService.Settings.OcrAiTimetable.Provider == OcrAiProvider.OpenAiCompatible;

    /// <summary>
    /// 是否可以发起识别（已选图片且未在识别中）。
    /// </summary>
    public bool CanRecognize => !IsRecognizing && !string.IsNullOrWhiteSpace(SourceImagePath);

    partial void OnSourceImagePathChanged(string value) => OnPropertyChanged(nameof(CanRecognize));
}
