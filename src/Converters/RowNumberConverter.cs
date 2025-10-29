using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace EtherNetIPTool.Converters;

/// <summary>
/// Converts DataGridRow to its row number (1-based index)
/// Used for displaying row numbers in device table (REQ-3.4-001, REQ-5.5)
/// </summary>
public class RowNumberConverter : IValueConverter
{
    /// <summary>
    /// Convert DataGridRow to row number
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DataGridRow row)
        {
            var dataGrid = ItemsControl.ItemsControlFromItemContainer(row) as DataGrid;
            if (dataGrid != null)
            {
                // Get 1-based index
                int index = dataGrid.ItemContainerGenerator.IndexFromContainer(row);
                return index >= 0 ? (index + 1).ToString() : string.Empty;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// ConvertBack not implemented (one-way binding only)
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("RowNumberConverter is one-way only");
    }
}
