using System.Windows;
using EtherNetIPTool.Models;
using EtherNetIPTool.ViewModels;

namespace EtherNetIPTool.Views;

/// <summary>
/// Configuration dialog for EtherNet/IP device (REQ-3.5, REQ-5.8)
/// Fixed size: 500x400 pixels, centered on parent
/// </summary>
public partial class ConfigurationDialog : Window
{
    private readonly ConfigurationViewModel _viewModel;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="device">Device to configure</param>
    public ConfigurationDialog(Device device)
    {
        InitializeComponent();

        _viewModel = new ConfigurationViewModel(device);
        DataContext = _viewModel;

        // Subscribe to ViewModel dialog result changes
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    /// <summary>
    /// Handle ViewModel property changes
    /// </summary>
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConfigurationViewModel.DialogResult))
        {
            if (_viewModel.DialogResult.HasValue)
            {
                DialogResult = _viewModel.DialogResult;
                Close();
            }
        }
    }

    /// <summary>
    /// Get the configuration result (null if cancelled)
    /// </summary>
    public DeviceConfiguration? GetConfiguration()
    {
        return _viewModel.Configuration;
    }
}
