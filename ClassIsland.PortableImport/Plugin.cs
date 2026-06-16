using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.Profile;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.PortableImport.Controls.ProfileTransferProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassIsland.PortableImport;

[PluginEntrance]
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 图标使用 FluentIcon 字形 （编辑/文字），与导入列表中其他项保持一致风格
        const string icon = "";
        services.AddProfileTransferProvider<TextImportProvider>(
            "classisland.portable-import",
            "便携导入（文字输入）",
            ProfileTransferProviderType.Import,
            icon);
    }
}
