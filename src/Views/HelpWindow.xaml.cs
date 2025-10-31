using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using EtherNetIPTool.ViewModels;

namespace EtherNetIPTool.Views;

/// <summary>
/// Help viewer window displaying embedded HTML help content
/// </summary>
public partial class HelpWindow : Window
{
    public HelpWindow(string helpFile)
    {
        InitializeComponent();

        var viewModel = new HelpViewModel(helpFile);
        DataContext = viewModel;

        // Navigate to HTML content when loaded
        Loaded += (s, e) =>
        {
            HelpBrowser.NavigateToString(viewModel.HtmlContent);
        };
    }
}
