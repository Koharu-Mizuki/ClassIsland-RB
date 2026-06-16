using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Helpers.UI;
using ClassIsland.Models.OcrAiTimetable;
using ClassIsland.Services;
using ClassIsland.Shared;
using ClassIsland.Shared.Helpers;
using ClassIsland.Shared.Models.Profile;
using ClassIsland.Platforms.Abstraction;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Controls.ProfileTransferProviders;

/// <summary>
/// AI 识图导入课表提供方：选取课表图片，调用视觉模型识别，再导入为档案。
/// </summary>
public partial class OcrAiImportProvider : ProfileTransferProviderControlBase
{
    private readonly OcrAiTimetableService _ocrService = IAppHost.GetService<OcrAiTimetableService>();
    private readonly SettingsService _settingsService = IAppHost.GetService<SettingsService>();

    /// <summary>
    /// 上一次成功的识别结果，由 <see cref="InvokeTransfer"/> 提交导入。
    /// </summary>
    private OcrTimetableResult? _lastResult;

    public OcrAiImportViewModel ViewModel { get; } = new();

    public OcrAiImportProvider()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private static readonly FilePickerFileType ImageFileType = new("图片文件")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"]
    };

    private static string GetMimeType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        _ => "image/png"
    };

    private async void ButtonPickImage_OnClick(object? sender, RoutedEventArgs e)
    {
        PopupHelper.DisableAllPopups();
        var files = await PlatformServices.FilePickerService.OpenFilesPickerAsync(new FilePickerOpenOptions
        {
            FileTypeFilter = [ImageFileType],
            AllowMultiple = false,
            SuggestedFileName = ViewModel.SourceImagePath
        }, TopLevel.GetTopLevel(this) ?? AppBase.Current.GetRootWindow());
        PopupHelper.RestoreAllPopups();

        if (files.Count <= 0)
        {
            return;
        }

        ViewModel.SourceImagePath = files[0];
        // 选了新图片，旧的识别结果作废
        _lastResult = null;
        ViewModel.HasResult = false;
        ViewModel.ResultSummary = "";

        try
        {
            ViewModel.ImagePreview = new Bitmap(files[0]);
        }
        catch (Exception exception)
        {
            IAppHost.GetService<ILogger<OcrAiImportProvider>>().LogWarning(exception, "无法加载图片预览");
            ViewModel.ImagePreview = null;
        }
    }

    private async void ButtonRecognize_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanRecognize)
        {
            return;
        }

        ViewModel.IsRecognizing = true;
        try
        {
            // 持久化用户填写的服务商配置（含密钥），下次进来仍在
            _settingsService.SaveSettings("更新 AI 识图导入配置");

            var bytes = await File.ReadAllBytesAsync(ViewModel.SourceImagePath);
            var result = await _ocrService.RecognizeAsync(bytes, GetMimeType(ViewModel.SourceImagePath),
                _settingsService.Settings.OcrAiTimetable);

            if (result.Schedules.Count == 0)
            {
                _lastResult = null;
                ViewModel.HasResult = false;
                ViewModel.ResultSummary = "未能从图片中识别出课表，请换一张更清晰的图片再试。";
                this.ShowErrorToast("未识别到课表内容。");
                return;
            }

            _lastResult = result;
            ViewModel.HasResult = true;
            ViewModel.ResultSummary = BuildSummary(result);
            this.ShowSuccessToast("识别完成，确认无误后点击「导入」。");
        }
        catch (Exception exception)
        {
            _lastResult = null;
            ViewModel.HasResult = false;
            IAppHost.GetService<ILogger<OcrAiImportProvider>>().LogError(exception, "AI 识图识别课表失败");
            this.ShowErrorToast("识别失败", exception);
        }
        finally
        {
            ViewModel.IsRecognizing = false;
        }
    }

    private static string BuildSummary(OcrTimetableResult result)
    {
        var classCount = 0;
        foreach (var s in result.Schedules)
        {
            classCount += s.Classes.Count;
        }

        return $"识别到 {result.Subjects.Count} 个科目、{result.Schedules.Count} 天课表，共 {classCount} 节课。";
    }

    /// <inheritdoc />
    public override async Task<bool> InvokeTransfer()
    {
        if (_lastResult == null)
        {
            this.ShowErrorToast("请先识别课表图片，再进行导入。");
            return false;
        }

        try
        {
            Profile profile;
            if (ViewModel.ImportType == 0)
            {
                profile = _lastResult.ToProfile(IAppHost.GetService<IProfileService>().Profile);
            }
            else
            {
                var templateProfileJson = await new StreamReader(
                    AssetLoader.Open(new Uri("avares://ClassIsland/Assets/default-subjects.json"))).ReadToEndAsync();
                var templateProfile = JsonSerializer.Deserialize<Profile>(templateProfileJson);
                profile = _lastResult.ToProfile(templateProfile);

                var path = Path.Combine(ProfileService.ProfilePath, ViewModel.NewProfileName + ".json");
                if (File.Exists(path))
                {
                    throw new InvalidOperationException($"无法导入课表：{path} 已存在。");
                }
                ConfigureFileHelper.SaveConfig(path, profile);
            }

            this.ShowSuccessToast("导入成功。");
            return true;
        }
        catch (Exception exception)
        {
            IAppHost.GetService<ILogger<OcrAiImportProvider>>().LogError(exception, "导入 AI 识别课表失败");
            this.ShowErrorToast("导入失败", exception);
            return false;
        }
    }
}
