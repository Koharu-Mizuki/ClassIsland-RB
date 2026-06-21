using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClassIsland.F1Timing.Models;

/// <summary>
/// 轻量可观察模型基类，提供 <see cref="INotifyPropertyChanged"/> 样板。
/// 与便携导入插件保持一致，手写实现而不引入外部 MVVM 包。
/// </summary>
public abstract class ObservableModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>设置字段并在值变化时触发通知，返回是否发生了变化。</summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
