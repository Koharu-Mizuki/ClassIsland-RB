using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Helpers.UI;
using ClassIsland.PortableImport.Models;
using ClassIsland.PortableImport.Services;
using ClassIsland.Shared;
using ClassIsland.Shared.Helpers;
using ClassIsland.Shared.Models.Profile;

namespace ClassIsland.PortableImport.Controls.ProfileTransferProviders;

public class EditableClassItem : INotifyPropertyChanged
{
    private string _name = "";
    public int Index { get; set; }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class PeriodPreviewItem
{
    public string Label { get; set; } = "";
    public string Time { get; set; } = "";
    public bool IsClass { get; set; }
}

public class GroupSelectorItem
{
    public Guid? Id { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsNew => !Id.HasValue;
}

public class DayClassGroup
{
    public string DayName { get; set; } = "";
    public int? WeekDay { get; set; }
    public bool HasDayName => !string.IsNullOrEmpty(DayName);
    public ObservableCollection<EditableClassItem> Classes { get; } = [];
}

public partial class TextImportProvider : ProfileTransferProviderControlBase, INotifyPropertyChanged
{
    private IProfileService ProfileService { get; } = IAppHost.GetService<IProfileService>();

    private const string KaminamiSchedule =
        "神山高校标准课时安排表\n\n" +
        "星期一\n" +
        "08:35 - 08:45 早会\n" +
        "08:50 - 09:40 语文\n" +
        "09:50 - 10:40 数学\n" +
        "10:50 - 11:40 英语\n" +
        "11:50 - 12:40 化学\n" +
        "12:40 - 13:30 午休\n" +
        "13:30 - 14:20 历史\n" +
        "14:30 - 15:20 体育\n" +
        "15:20 - 15:30 清扫时间\n" +
        "15:30 - 15:40 放学随堂会";

    private string _inputText = "";
    private bool _isParsing;
    private string _statusMessage = "";
    private bool _hasError;
    private TimetableImportData? _result;
    private int _importType;
    private string _newProfileName = "导入的课表";
    private CancellationTokenSource? _cts;

    public string InputText
    {
        get => _inputText;
        set
        {
            if (value.Trim() == "神山高校")
                value = KaminamiSchedule;
            _inputText = value;
            OnPropChanged();
            OnPropChanged(nameof(CanParse));
        }
    }

    public bool CanParse => !_isParsing && !string.IsNullOrWhiteSpace(_inputText);

    public bool IsParsing
    {
        get => _isParsing;
        private set { _isParsing = value; OnPropChanged(); OnPropChanged(nameof(CanParse)); }
    }

    public string StatusMessage { get => _statusMessage; private set { _statusMessage = value; OnPropChanged(); OnPropChanged(nameof(HasStatusMessage)); } }
    public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);
    public bool HasError { get => _hasError; private set { _hasError = value; OnPropChanged(); } }

    public bool HasResult => _result != null;
    public ObservableCollection<PeriodPreviewItem> ResultPeriods { get; } = [];
    public ObservableCollection<DayClassGroup> ClassGroups { get; } = [];

    public int ImportType { get => _importType; set { _importType = value; OnPropChanged(); } }
    public string NewProfileName { get => _newProfileName; set { _newProfileName = value; OnPropChanged(); } }

    public ObservableCollection<GroupSelectorItem> AvailableGroups { get; } = [];
    private GroupSelectorItem? _selectedGroupItem;
    public GroupSelectorItem? SelectedGroupItem
    {
        get => _selectedGroupItem;
        set { _selectedGroupItem = value; OnPropChanged(); OnPropChanged(nameof(IsCreatingNewGroup)); }
    }
    public bool IsCreatingNewGroup => _selectedGroupItem?.IsNew ?? true;
    private string _importGroupName = "便携导入";
    public string ImportGroupName { get => _importGroupName; set { _importGroupName = value; OnPropChanged(); } }

    public TextImportProvider()
    {
        DataContext = this;
        InitializeComponent();
        PopulateGroups();
    }

    private void PopulateGroups()
    {
        AvailableGroups.Clear();
        AvailableGroups.Add(new GroupSelectorItem { Id = null, DisplayName = "新建课表群" });
        foreach (var kv in ProfileService.Profile.ClassPlanGroups)
        {
            if (kv.Key == ClassPlanGroup.GlobalGroupGuid) continue;
            AvailableGroups.Add(new GroupSelectorItem { Id = kv.Key, DisplayName = kv.Value.Name });
        }
        SelectedGroupItem = AvailableGroups[0];
    }

    private void ButtonLoadPreset_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        InputText = KaminamiSchedule;
    }

    private async void ButtonParse_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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
            StatusMessage = $"失败：{ex.Message}";
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
        int classIndex = 1;
        foreach (var p in data.TimeLayout.Periods)
        {
            bool isClass = p.Type != "break";
            ResultPeriods.Add(new PeriodPreviewItem
            {
                Label = isClass ? $"第{classIndex++}节" : "课间",
                Time = $"{p.Start} – {p.End}",
                IsClass = isClass
            });
        }

        ClassGroups.Clear();
        foreach (var cp in data.ClassPlans)
        {
            var group = new DayClassGroup { DayName = cp.DayName, WeekDay = cp.WeekDay };
            for (int i = 0; i < cp.Classes.Count; i++)
                group.Classes.Add(new EditableClassItem { Index = i + 1, Name = cp.Classes[i] });
            ClassGroups.Add(group);
        }

        OnPropChanged(nameof(HasResult));
    }

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

        foreach (var group in ClassGroups)
        {
            var classPlan = new ClassPlan
            {
                Name = group.HasDayName ? $"{group.DayName}课表" : "手动输入的课表",
                TimeLayoutId = timeLayoutId,
                AssociatedGroup = groupId
            };
            if (group.WeekDay.HasValue)
                classPlan.TimeRule.WeekDay = group.WeekDay.Value;

            foreach (var item in group.Classes)
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
                        profile.Subjects[subjectId] = new Subject { Name = subjectName };
                }
                classPlan.Classes.Add(new ClassInfo { SubjectId = subjectId });
            }
            profile.ClassPlans[Guid.NewGuid()] = classPlan;
        }
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
