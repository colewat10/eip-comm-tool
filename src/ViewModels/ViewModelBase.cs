using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EtherNetIPTool.ViewModels;

/// <summary>
/// Base class for all ViewModels implementing INotifyPropertyChanged
/// Provides common functionality for property change notification
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <summary>
    /// Occurs when a property value changes
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event for the specified property
    /// </summary>
    /// <param name="propertyName">Name of the property that changed (automatically provided by CallerMemberName)</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets the property value and raises PropertyChanged if the value has changed
    /// </summary>
    /// <typeparam name="T">Type of the property</typeparam>
    /// <param name="field">Reference to the backing field</param>
    /// <param name="value">New value to set</param>
    /// <param name="propertyName">Name of the property (automatically provided)</param>
    /// <returns>True if the value was changed, false if it was the same</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Sets the property value, raises PropertyChanged, and executes an action if the value has changed
    /// </summary>
    /// <typeparam name="T">Type of the property</typeparam>
    /// <param name="field">Reference to the backing field</param>
    /// <param name="value">New value to set</param>
    /// <param name="onChanged">Action to execute when value changes</param>
    /// <param name="propertyName">Name of the property (automatically provided)</param>
    /// <returns>True if the value was changed, false if it was the same</returns>
    protected bool SetProperty<T>(ref T field, T value, Action onChanged, [CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            onChanged?.Invoke();
            return true;
        }
        return false;
    }
}
