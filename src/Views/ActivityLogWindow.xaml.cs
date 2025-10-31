using System.Windows;
using EtherNetIPTool.ViewModels;
using EtherNetIPTool.Services;

namespace EtherNetIPTool.Views;

/// <summary>
/// Activity Log Viewer window (REQ-3.7-004)
/// Displays application activity log with category filtering and export functionality
/// </summary>
public partial class ActivityLogWindow : Window
{
    public ActivityLogWindow(ActivityLogger logger)
    {
        InitializeComponent();
        DataContext = new ActivityLogViewModel(logger);
    }
}
