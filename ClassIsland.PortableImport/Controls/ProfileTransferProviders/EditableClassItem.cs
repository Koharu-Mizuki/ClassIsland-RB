using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClassIsland.PortableImport.Controls.ProfileTransferProviders;

/// <summary>
/// 课程列表中可编辑的一节课。
/// </summary>
public class EditableClassItem : INotifyPropertyChanged
{
    private string _name = "";

    public int Index { get; set; }

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
