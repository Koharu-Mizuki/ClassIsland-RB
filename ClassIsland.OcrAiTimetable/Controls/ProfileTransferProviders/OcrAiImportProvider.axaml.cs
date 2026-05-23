using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Helpers.UI;
using ClassIsland.OcrAiTimetable.Models;
using ClassIsland.OcrAiTimetable.Services;
using ClassIsland.Shared;
using ClassIsland.Shared.Helpers;
using ClassIsland.Shared.Models.Profile;

namespace ClassIsland.OcrAiTimetable.Controls.ProfileTransferProviders;

// 可编辑的课程行（用于识别结果列表）
public class EditableClassItem : INotifyPropertyChanged
{
    private string _name = "";
    public int Index { get; set; }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

// 时间段预览行
public class PeriodPreviewItem
{
    public string Label { get; set; } = "";
    public string Time { get; set; } = "";
    public bool IsClass { get; set; }
}

public partial class OcrAiImportProvider : ProfileTransferProviderControlBase, INotifyPropertyChanged
{
    private OcrAiSettingsService SettingsSvc { get; } = IAppHost.GetService<OcrAiSettingsService>();
    private IProfileService ProfileService { get; } = IAppHost.GetService<IProfileService>();
    private OcrAiService OcrService { get; } = new();

    private string _imagePath = "";
    private Bitmap? _previewBitmap;
    private bool _isRecognizing;
    private string _statusMessage = "";
    private bool _hasError;
    private AiTimetableResponse? _result;
    private int _importType;
    private string _newProfileName = "导入的课表";
    private CancellationTokenSource? _cts;
    private int _inputMode;
    private string _manualText = "";

    public string ImagePath
    {
        get => _imagePath;
        set { _imagePath = value; OnPropChanged(); OnPropChanged(nameof(HasImage)); OnPropChanged(nameof(CanRecognize)); LoadPreview(); }
    }

    public Bitmap? PreviewBitmap { get => _previewBitmap; private set { _previewBitmap = value; OnPropChanged(); } }
    public bool HasImage => File.Exists(_imagePath);
    public bool CanRecognize => !_isRecognizing && (IsTextMode ? !string.IsNullOrWhiteSpace(_manualText) : HasImage);

    public bool IsRecognizing
    {
        get => _isRecognizing;
        private set { _isRecognizing = value; OnPropChanged(); OnPropChanged(nameof(CanRecognize)); }
    }

    public string StatusMessage { get => _statusMessage; private set { _statusMessage = value; OnPropChanged(); OnPropChanged(nameof(HasStatusMessage)); } }
    public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);
    public bool HasError { get => _hasError; private set { _hasError = value; OnPropChanged(); } }

    public bool HasResult => _result != null;
    public ObservableCollection<PeriodPreviewItem> ResultPeriods { get; } = [];
    public ObservableCollection<EditableClassItem> EditableClasses { get; } = [];

    public int ImportType { get => _importType; set { _importType = value; OnPropChanged(); } }
    public string NewProfileName { get => _newProfileName; set { _newProfileName = value; OnPropChanged(); } }

    public int InputMode
    {
        get => _inputMode;
        set { _inputMode = value; OnPropChanged(); OnPropChanged(nameof(IsAiMode)); OnPropChanged(nameof(IsTextMode)); OnPropChanged(nameof(CanRecognize)); }
    }

    public bool IsAiMode
    {
        get => _inputMode == 0;
        set { if (value) InputMode = 0; }
    }

    public bool IsTextMode
    {
        get => _inputMode == 1;
        set { if (value) InputMode = 1; }
    }

    public string ManualText
    {
        get => _manualText;
        set { _manualText = value; OnPropChanged(); OnPropChanged(nameof(CanRecognize)); }
    }

    public string ApiSummary
    {
        get
        {
            var s = SettingsSvc.Settings;
            return s.Provider switch
            {
                AiProvider.OpenAI => $"OpenAI · {s.ModelId}",
                AiProvider.Anthropic => $"Anthropic · {s.ModelId}",
                AiProvider.Google => $"Google Gemini · {s.ModelId}",
                AiProvider.DeepSeek => $"DeepSeek · {s.ModelId}（仅文字模式）",
                AiProvider.Kimi => $"Kimi · {s.ModelId}",
                AiProvider.Custom => $"自定义接口 · {s.ModelId}",
                _ => "未配置"
            };
        }
    }

    public OcrAiImportProvider()
    {
        DataContext = this;
        InitializeComponent();
    }

    private async void ButtonPickImage_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择课表图片",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("图片文件") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif"] },
                new FilePickerFileType("所有文件") { Patterns = ["*.*"] }
            ]
        });

        if (files.Count > 0)
            ImagePath = files[0].Path.LocalPath;
    }

    private void LoadPreview()
    {
        PreviewBitmap?.Dispose();
        PreviewBitmap = null;
        if (!HasImage) return;
        try { PreviewBitmap = new Bitmap(_imagePath); }
        catch { /* 不支持的格式，跳过预览 */ }
    }

    private async void ButtonRecognize_OnClick(object? sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        IsRecognizing = true;
        HasError = false;
        StatusMessage = IsTextMode ? "正在解析…" : "正在识别，请稍候…";
        _result = null;
        ResultPeriods.Clear();
        EditableClasses.Clear();
        OnPropChanged(nameof(HasResult));

        try
        {
            AiTimetableResponse response;
            if (IsTextMode)
            {
                response = await Task.Run(() => TextTimetableParser.Parse(_manualText), _cts.Token);
            }
            else
            {
                response = await OcrService.RecognizeAsync(_imagePath, SettingsSvc.Settings, _cts.Token);
            }
            await Dispatcher.UIThread.InvokeAsync(() => ApplyResult(response));
            StatusMessage = "解析成功！请检查结果后点击「导入课表」。";
            HasError = false;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "已取消。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"失败：{ex.Message}";
            HasError = true;
        }
        finally
        {
            IsRecognizing = false;
        }
    }

    private void ApplyResult(AiTimetableResponse response)
    {
        _result = response;

        ResultPeriods.Clear();
        int classIndex = 1;
        foreach (var p in response.TimeLayout.Periods)
        {
            bool isClass = p.Type != "break";
            ResultPeriods.Add(new PeriodPreviewItem
            {
                Label = isClass ? $"第{classIndex++}节" : "课间",
                Time = $"{p.Start} – {p.End}",
                IsClass = isClass
            });
        }

        EditableClasses.Clear();
        var classes = response.ClassPlan.Classes;
        for (int i = 0; i < classes.Count; i++)
        {
            EditableClasses.Add(new EditableClassItem { Index = i + 1, Name = classes[i] });
        }

        OnPropChanged(nameof(HasResult));
    }

    // Called by the outer ProfileSettingsWindow "导入" button
    public override Task<bool> InvokeTransfer()
    {
        DoImport();
        return Task.FromResult(HasResult);
    }

    private void DoImport()
    {
        if (_result == null) return;
        try
        {
            var profile = ImportType == 0 ? ProfileService.Profile : new Profile();
            BuildProfileData(profile);

            if (ImportType == 1)
            {
                var profilesDir = Path.GetDirectoryName(ProfileService.CurrentProfilePath);
                if (string.IsNullOrEmpty(profilesDir))
                    profilesDir = Path.Combine(ClassIsland.Core.CommonDirectories.AppRootFolderPath, "Profiles");
                Directory.CreateDirectory(profilesDir);
                if (string.IsNullOrWhiteSpace(NewProfileName))
                    throw new InvalidOperationException("请填写新档案的名称。");
                var path = Path.Combine(profilesDir, NewProfileName.Trim() + ".json");
                if (File.Exists(path))
                    throw new InvalidOperationException($"档案已存在：{path}");
                ConfigureFileHelper.SaveConfig(path, profile);
            }
            else
            {
                ProfileService.SaveProfile();
            }

            this.ShowSuccessToast("课表导入成功！");
        }
        catch (Exception ex)
        {
            this.ShowErrorToast("导入失败", ex);
        }
    }

    private void BuildProfileData(Profile profile)
    {
        if (_result == null) return;

        var timeLayout = new TimeLayout { Name = _result.TimeLayout.Name };

        foreach (var p in _result.TimeLayout.Periods)
        {
            if (!TryParseTime(p.Start, out var start) || !TryParseTime(p.End, out var end)) continue;
            timeLayout.Layouts.Add(new TimeLayoutItem
            {
                StartTime = start,
                EndTime = end,
                TimeType = p.Type == "break" ? 1 : 0
            });
        }

        var timeLayoutId = Guid.NewGuid();
        profile.TimeLayouts[timeLayoutId] = timeLayout;

        // ClassPlan.RefreshClassesList() is internal, so we populate Classes directly
        var classPlan = new ClassPlan
        {
            Name = _result.ClassPlan.Name,
            TimeLayoutId = timeLayoutId
        };

        var editList = EditableClasses.ToList();
        for (int i = 0; i < editList.Count; i++)
        {
            var subjectName = editList[i].Name.Trim();

            Guid subjectId;
            if (string.IsNullOrEmpty(subjectName))
            {
                subjectId = Guid.Empty;
            }
            else
            {
                var existing = profile.Subjects.FirstOrDefault(kv => kv.Value.Name == subjectName);
                if (existing.Value != null)
                {
                    subjectId = existing.Key;
                }
                else
                {
                    subjectId = Guid.NewGuid();
                    profile.Subjects[subjectId] = new Subject { Name = subjectName };
                }
            }

            classPlan.Classes.Add(new ClassInfo { SubjectId = subjectId });
        }

        profile.ClassPlans[Guid.NewGuid()] = classPlan;
    }

    private static bool TryParseTime(string text, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        if (TimeSpan.TryParseExact(text, @"hh\:mm", null, out result)) return true;
        if (TimeSpan.TryParseExact(text, @"h\:mm", null, out result)) return true;
        return false;
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
