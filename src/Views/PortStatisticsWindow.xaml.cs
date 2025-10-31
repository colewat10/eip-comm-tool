using System.Windows;
using EtherNetIPTool.Models;
using EtherNetIPTool.Services;
using EtherNetIPTool.ViewModels;

namespace EtherNetIPTool.Views;

/// <summary>
/// Port Statistics window for viewing network port metrics
/// Displays real-time statistics including packets, bytes, errors, and collisions
/// </summary>
public partial class PortStatisticsWindow : Window
{
    private readonly PortStatisticsViewModel _viewModel;

    public PortStatisticsWindow(Device device, ActivityLogger logger)
    {
        InitializeComponent();

        _viewModel = new PortStatisticsViewModel(device, logger);
        DataContext = _viewModel;

        // Dispose ViewModel when window closes to stop auto-refresh timer
        Closed += (s, e) => _viewModel.Dispose();
    }
}
