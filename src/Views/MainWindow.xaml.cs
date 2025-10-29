using System.Windows;
using System.Windows.Input;
using EtherNetIPTool.ViewModels;

namespace EtherNetIPTool.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// Fixed size: 1280x768 (REQ-5.1)
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handle double-click on device table row (REQ-3.4-010)
    /// Opens configuration dialog for the selected device
    /// </summary>
    private void DeviceTable_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Get the ViewModel
        if (DataContext is MainWindowViewModel viewModel)
        {
            // Execute configure command if device is selected
            if (viewModel.ConfigureDeviceCommand.CanExecute(null))
            {
                viewModel.ConfigureDeviceCommand.Execute(null);
            }
        }
    }
}
