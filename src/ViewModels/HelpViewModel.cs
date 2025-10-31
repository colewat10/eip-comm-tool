using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace EtherNetIPTool.ViewModels;

/// <summary>
/// ViewModel for Help window
/// Displays embedded HTML help content
/// </summary>
public class HelpViewModel : ViewModelBase
{
    private string _htmlContent = string.Empty;
    private string _title = "Help";

    public HelpViewModel(string helpFile)
    {
        CloseCommand = new RelayCommand(ExecuteClose);
        LoadHelpContent(helpFile);
    }

    #region Properties

    /// <summary>
    /// HTML content to display
    /// </summary>
    public string HtmlContent
    {
        get => _htmlContent;
        set => SetProperty(ref _htmlContent, value);
    }

    /// <summary>
    /// Window title
    /// </summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    #endregion

    #region Commands

    public ICommand CloseCommand { get; }

    #endregion

    #region Command Implementations

    private void ExecuteClose(object? parameter)
    {
        if (parameter is Window window)
        {
            window.Close();
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Load help content from embedded resource or file
    /// </summary>
    private void LoadHelpContent(string helpFile)
    {
        try
        {
            // Try to load from Resources/Help directory
            var helpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Help", helpFile);

            if (File.Exists(helpPath))
            {
                HtmlContent = File.ReadAllText(helpPath);
                SetTitleFromFileName(helpFile);
            }
            else
            {
                // Fallback: Show error message
                HtmlContent = GenerateErrorHtml(helpFile);
                Title = "Help - File Not Found";
            }
        }
        catch (Exception ex)
        {
            HtmlContent = GenerateErrorHtml(helpFile, ex.Message);
            Title = "Help - Error";
        }
    }

    /// <summary>
    /// Set window title based on help file name
    /// </summary>
    private void SetTitleFromFileName(string fileName)
    {
        Title = fileName switch
        {
            "UserManual.html" => "User Manual",
            "CIPProtocolReference.html" => "CIP Protocol Reference",
            "BootPReference.html" => "BootP/DHCP Reference",
            "TroubleshootingGuide.md" => "Troubleshooting Guide",
            _ => "Help"
        };
    }

    /// <summary>
    /// Generate error HTML when help file not found
    /// </summary>
    private string GenerateErrorHtml(string fileName, string? errorMessage = null)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{
            font-family: Segoe UI, Tahoma, Geneva, Verdana, sans-serif;
            padding: 40px;
            background-color: #f8f8f8;
        }}
        .error {{
            background-color: #ffe6e6;
            border-left: 4px solid #cc0000;
            padding: 20px;
            border-radius: 4px;
        }}
        h1 {{
            color: #cc0000;
            margin-top: 0;
        }}
        code {{
            background-color: #e8e8e8;
            padding: 2px 6px;
            border-radius: 3px;
            font-family: Consolas, monospace;
        }}
    </style>
</head>
<body>
    <div class='error'>
        <h1>Help File Not Found</h1>
        <p>The requested help file <code>{fileName}</code> could not be loaded.</p>
        {(errorMessage != null ? $"<p><strong>Error:</strong> {errorMessage}</p>" : "")}
        <p>Please ensure the application is installed correctly with all help resources.</p>
        <p>Help files should be located in: <code>Resources/Help/</code></p>
    </div>
</body>
</html>";
    }

    #endregion
}
