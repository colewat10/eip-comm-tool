using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EtherNetIPTool.Converters;

/// <summary>
/// Converts non-empty string to Visible, empty/null string to Collapsed
/// Used for showing validation error messages (REQ-3.5.3-007)
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Convert string to Visibility
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && !string.IsNullOrEmpty(str))
        {
            return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    /// <summary>
    /// ConvertBack not implemented (one-way binding only)
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("StringToVisibilityConverter is one-way only");
    }
}
