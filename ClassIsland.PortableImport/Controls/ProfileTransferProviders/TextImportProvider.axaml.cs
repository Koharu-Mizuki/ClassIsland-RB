using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Helpers.UI;
using ClassIsland.PortableImport.Models;
using ClassIsland.PortableImport.Services;
using ClassIsland.Shared;
using ClassIsland.Shared.Helpers;
using ClassIsland.Shared.Models.Profile;

namespace ClassIsland.PortableImport.Controls.ProfileTransferProviders;

/// <summary>
/// 便携导入（文字输入）提供方：把用户粘贴的课表文字解析、预览并导入为档案。
/// </summary>
public partial class TextImportProvider : ProfileTransferProviderControlBase, INotifyPropertyChanged
{
    private const string KaminamiSchedule =
        "神山高校标准课时安排表\n\n星期一\n08:35 - 08:45 早会\n08:50 - 09:40 语文\n09:50 - 10:40 数学\n10:50 - 11:40 英语\n11:50 - 12:40 化学\n12:40 - 13:30 午休\n13:30 - 14:20 历史\n14:30 - 15:20 体育\n15:20 - 15:30 清扫时间\n15:30 - 15:40 放学随堂会";

    private string _inputText = "";
    private bool _isParsing;
    private string _statusMessage = "";
    private bool _hasError;
    private TimetableImportData? _result;
    private int _importType;
    private string _newProfileName = "导入的课表";
    private CancellationTokenSource? _cts;
    private GroupSelectorItem? _selectedGroupItem;
    private string _importGroupName = "便携导入";

    private IProfileService ProfileService { get; } = IAppHost.GetService<IProfileService>();

    public TextImportProvider()
    {
        DataContext = this;
        InitializeComponent();
        PopulateGroups();
    }

    public string InputText
    {
        get => _inputText;
        set
        {
            // 彩蛋：输入「神山高校」自动展开示例课表
            if (value.Trim() == "神山高校")
            {
                value = KaminamiSchedule;
            }
            _inputText = value;
            OnPropChanged();
            OnPropChanged(nameof(CanParse));
        }
    }

    public bool CanParse => !_isParsing && !string.IsNullOrWhiteSpace(_inputText);

    public bool IsParsing
    {
        get => _isParsing;
        private set
        {
            _isParsing = value;
            OnPropChanged();
            OnPropChanged(nameof(CanParse));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            _statusMessage = value;
            OnPropChanged();
            OnPropChanged(nameof(HasStatusMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);

    public bool HasError
    {
        get => _hasError;
        private set
        {
            _hasError = value;
            OnPropChanged();
        }
    }

    public bool HasResult => _result != null;

    public ObservableCollection<PeriodPreviewItem> ResultPeriods { get; } = new();

    public ObservableCollection<DayClassGroup> ClassGroups { get; } = new();

    public int ImportType
    {
        get => _importType;
        set
        {
            _importType = value;
            OnPropChanged();
        }
    }

    public string NewProfileName
    {
        get => _newProfileName;
        set
        {
            _newProfileName = value;
            OnPropChanged();
        }
    }

    public ObservableCollection<GroupSelectorItem> AvailableGroups { get; } = new();

    public GroupSelectorItem? SelectedGroupItem
    {
        get => _selectedGroupItem;
        set
        {
            _selectedGroupItem = value;
            OnPropChanged();
            OnPropChanged(nameof(IsCreatingNewGroup));
        }
    }

    public bool IsCreatingNewGroup => _selectedGroupItem?.IsNew ?? true;

    public string ImportGroupName
    {
        get => _importGroupName;
        set
        {
            _importGroupName = value;
            OnPropChanged();
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    private void PopulateGroups()
    {
        AvailableGroups.Clear();
        AvailableGroups.Add(new GroupSelectorItem { Id = null, DisplayName = "新建课表群" });
        foreach (var group in ProfileService.Profile.ClassPlanGroups)
        {
            if (group.Key != ClassPlanGroup.GlobalGroupGuid)
            {
                AvailableGroups.Add(new GroupSelectorItem { Id = group.Key, DisplayName = group.Value.Name });
            }
        }
        SelectedGroupItem = AvailableGroups[0];
    }

    private void ButtonLoadPreset_OnClick(object? sender, RoutedEventArgs e)
    {
        InputText = KaminamiSchedule;
    }

    private async void ButtonParse_OnClick(object? sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        IsParsing = true;
        HasError = false;
        StatusMessage = "正在解析…";
        _result = null;
        ResultPeriods.Clear();
        ClassGroups.Clear();
        OnPropChanged(nameof(HasResult));
        try
        {
            var response = await Task.Run(() => TextTimetableParser.Parse(_inputText), _cts.Token);
            await Dispatcher.UIThread.InvokeAsync(() => ApplyResult(response));
            StatusMessage = "解析成功！请检查结果后点击「导入课表」。";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "已取消。";
        }
        catch (Exception ex)
        {
            StatusMessage = "失败：" + ex.Message;
            HasError = true;
        }
        finally
        {
            IsParsing = false;
        }
    }

    private void ApplyResult(TimetableImportData data)
    {
        _result = data;
        ResultPeriods.Clear();
        var index = 1;
        foreach (var period in data.TimeLayout.Periods)
        {
            var isClass = period.Type != "break";
            ResultPeriods.Add(new PeriodPreviewItem
            {
                Label = isClass ? $"第{index++}节" : "课间",
                Time = period.Start + " – " + period.End,
                IsClass = isClass
            });
        }

        ClassGroups.Clear();
        foreach (var classPlan in data.ClassPlans)
        {
            var group = new DayClassGroup
            {
                DayName = classPlan.DayName,
                WeekDay = classPlan.WeekDay
            };
            for (var i = 0; i < classPlan.Classes.Count; i++)
            {
                group.Classes.Add(new EditableClassItem
                {
                    Index = i + 1,
                    Name = classPlan.Classes[i]
                });
            }
            ClassGroups.Add(group);
        }
        OnPropChanged(nameof(HasResult));
    }

    /// <inheritdoc />
    public override Task<bool> InvokeTransfer()
    {
        DoImport();
        return Task.FromResult(HasResult);
    }

    private void DoImport()
    {
        if (_result == null)
        {
            return;
        }

        try
        {
            var profile = ImportType == 0 ? ProfileService.Profile : new Profile();
            BuildProfileData(profile);

            if (ImportType == 1)
            {
                var dir = Path.GetDirectoryName(ProfileService.CurrentProfilePath);
                if (string.IsNullOrEmpty(dir))
                {
                    dir = Path.Combine(CommonDirectories.AppRootFolderPath, "Profiles");
                }
                Directory.CreateDirectory(dir);
                if (string.IsNullOrWhiteSpace(NewProfileName))
                {
                    throw new InvalidOperationException("请填写新档案的名称。");
                }
                var path = Path.Combine(dir, NewProfileName.Trim() + ".json");
                if (File.Exists(path))
                {
                    throw new InvalidOperationException("档案已存在：" + path);
                }
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
        if (_result == null)
        {
            return;
        }

        var timeLayout = new TimeLayout { Name = _result.TimeLayout.Name };
        foreach (var period in _result.TimeLayout.Periods)
        {
            if (TryParseTime(period.Start, out var start) && TryParseTime(period.End, out var end))
            {
                timeLayout.Layouts.Add(new TimeLayoutItem
                {
                    StartTime = start,
                    EndTime = end,
                    TimeType = period.Type == "break" ? 1 : 0
                });
            }
        }
        var timeLayoutId = Guid.NewGuid();
        profile.TimeLayouts[timeLayoutId] = timeLayout;

        Guid groupId;
        if (IsCreatingNewGroup || ImportType == 1)
        {
            groupId = Guid.NewGuid();
            var groupName = string.IsNullOrWhiteSpace(ImportGroupName) ? "便携导入" : ImportGroupName;
            profile.ClassPlanGroups[groupId] = new ClassPlanGroup { Name = groupName };
        }
        else
        {
            groupId = SelectedGroupItem!.Id!.Value;
        }

        foreach (var classGroup in ClassGroups)
        {
            var classPlan = new ClassPlan
            {
                Name = classGroup.HasDayName ? classGroup.DayName + "课表" : "手动输入的课表",
                TimeLayoutId = timeLayoutId,
                AssociatedGroup = groupId
            };
            if (classGroup.WeekDay.HasValue)
            {
                classPlan.TimeRule.WeekDay = classGroup.WeekDay.Value;
            }

            foreach (var item in classGroup.Classes)
            {
                var subjectName = item.Name.Trim();
                Guid subjectId;
                if (string.IsNullOrEmpty(subjectName))
                {
                    subjectId = Guid.Empty;
                }
                else
                {
                    var existing = profile.Subjects.FirstOrDefault(kv => kv.Value.Name == subjectName);
                    subjectId = existing.Value != null ? existing.Key : Guid.NewGuid();
                    if (existing.Value == null)
                    {
                        profile.Subjects[subjectId] = new Subject { Name = subjectName };
                    }
                }
                classPlan.Classes.Add(new ClassInfo { SubjectId = subjectId });
            }

            profile.ClassPlans[Guid.NewGuid()] = classPlan;
        }
    }

    private static bool TryParseTime(string text, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        return TimeSpan.TryParseExact(text, @"hh\:mm", null, out result)
               || TimeSpan.TryParseExact(text, @"h\:mm", null, out result);
    }

    private void OnPropChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
