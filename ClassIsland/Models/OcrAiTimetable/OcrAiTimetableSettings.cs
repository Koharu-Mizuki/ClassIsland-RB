using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Models.OcrAiTimetable;

/// <summary>
/// AI 识图导入课表的配置。作为 <see cref="ClassIsland.Models.Settings"/> 的子对象持久化。
/// </summary>
public partial class OcrAiTimetableSettings : ObservableObject
{
    /// <summary>
    /// 当前选用的服务商。
    /// </summary>
    [ObservableProperty] private OcrAiProvider _provider = OcrAiProvider.Glm;

    /// <summary>
    /// API 密钥。
    /// </summary>
    [ObservableProperty] private string _apiKey = "";

    /// <summary>
    /// 自定义接口地址（仅 <see cref="OcrAiProvider.OpenAiCompatible"/> 时使用）。
    /// 应为兼容 OpenAI 的基础地址，例如 <c>https://example.com/v1</c>。
    /// </summary>
    [ObservableProperty] private string _customBaseUrl = "";

    /// <summary>
    /// 自定义模型名（留空时使用所选服务商的默认模型）。
    /// </summary>
    [ObservableProperty] private string _customModel = "";

    /// <summary>
    /// 获取当前服务商实际生效的接口基础地址。
    /// </summary>
    public string ResolveBaseUrl() => Provider switch
    {
        OcrAiProvider.Glm => "https://open.bigmodel.cn/api/paas/v4",
        OcrAiProvider.DeepSeek => "https://api.deepseek.com/v1",
        OcrAiProvider.OpenAiCompatible => CustomBaseUrl.TrimEnd('/'),
        _ => CustomBaseUrl.TrimEnd('/')
    };

    /// <summary>
    /// 获取当前实际生效的模型名（用户填写优先，否则用服务商默认值）。
    /// </summary>
    public string ResolveModel()
    {
        if (!string.IsNullOrWhiteSpace(CustomModel))
        {
            return CustomModel.Trim();
        }

        return Provider switch
        {
            OcrAiProvider.Glm => "glm-4v-flash",
            OcrAiProvider.DeepSeek => "deepseek-vl2",
            OcrAiProvider.OpenAiCompatible => "",
            _ => ""
        };
    }
}
