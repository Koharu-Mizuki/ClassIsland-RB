using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.Profile;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.OcrAiTimetable.Controls.ProfileTransferProviders;
using ClassIsland.OcrAiTimetable.Services;
using ClassIsland.OcrAiTimetable.Views.SettingsPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassIsland.OcrAiTimetable;

[PluginEntrance]
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        var configFolder = PluginConfigFolder;
        services.AddSingleton(_ => new OcrAiSettingsService(configFolder));
        services.AddSettingsPage<OcrAiSettingsPage>();
        services.AddProfileTransferProvider<OcrAiImportProvider>(
            id: "classisland.ocr-ai-import",
            name: "从图片导入（AI 识别）",
            type: ProfileTransferProviderType.Import,
            icon: ""
        );
    }
}
