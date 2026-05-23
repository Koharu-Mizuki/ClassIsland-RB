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
        services.AddProfileTransferProvider<TextImportProvider>(
            id: "classisland.portable-import",
            name: "便携导入（文字输入）",
            type: ProfileTransferProviderType.Import,
            icon: ""
        );
    }
}
