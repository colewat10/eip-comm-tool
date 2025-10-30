using System.Windows;
using EtherNetIPTool.ViewModels;

namespace EtherNetIPTool.Views;

/// <summary>
/// BootP/DHCP configuration dialog
/// Allows user to assign IP configuration to factory-default device
/// REQ-3.6.3: BootP Configuration Dialog
/// </summary>
public partial class BootPConfigurationDialog : Window
{
    /// <summary>
    /// Create new BootP configuration dialog
    /// </summary>
    /// <param name="viewModel">ViewModel containing request data and configuration</param>
    public BootPConfigurationDialog(BootPConfigurationViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;

        // Hook up dialog result from ViewModel
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BootPConfigurationViewModel.DialogResult) &&
                viewModel.DialogResult.HasValue)
            {
                DialogResult = viewModel.DialogResult;
            }
        };
    }
}
