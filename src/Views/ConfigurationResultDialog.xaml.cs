using System.Windows;
using EtherNetIPTool.Services;

namespace EtherNetIPTool.Views;

/// <summary>
/// Result dialog for configuration write operations (REQ-3.5.5-008)
/// Displays success/failure status with detailed results
/// </summary>
public partial class ConfigurationResultDialog : Window
{
    private readonly ActivityLogger _logger;
    private readonly ConfigurationWriteResult _result;

    public ConfigurationResultDialog(ConfigurationWriteResult result, ActivityLogger logger)
    {
        InitializeComponent();

        if (result == null)
            throw new ArgumentNullException(nameof(result));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _result = result;

        // Log result dialog display (REQ-3.7-003)
        if (result.Success)
        {
            _logger.LogConfig($"Configuration write successful: {result.SuccessCount}/{result.TotalWrites} attributes written");
        }
        else
        {
            _logger.LogError($"Configuration write failed: {result.SuccessCount}/{result.TotalWrites} attributes written, {result.FailureCount} failed");
            _logger.LogError($"First error: {result.GetFirstErrorMessage()}");
        }

        DisplayResult(result);
    }

    /// <summary>
    /// Display configuration write results (REQ-3.5.5-008)
    /// </summary>
    private void DisplayResult(ConfigurationWriteResult result)
    {
        if (result.Success)
        {
            // Success state
            IconText.Text = "✓";
            IconText.Foreground = System.Windows.Media.Brushes.Green;
            TitleText.Text = "Configuration Successful";

            SummaryText.Text = $"All {result.SuccessCount} configuration attribute(s) written successfully.";
        }
        else
        {
            // Failure state
            IconText.Text = "✗";
            IconText.Foreground = System.Windows.Media.Brushes.Red;
            TitleText.Text = "Configuration Failed";

            // Show summary of what succeeded and what failed
            if (result.SuccessCount > 0)
            {
                SummaryText.Text = $"{result.SuccessCount} of {result.TotalWrites} attribute(s) written. " +
                                  $"Operation stopped at first failure.";
            }
            else if (!string.IsNullOrEmpty(result.GeneralError))
            {
                SummaryText.Text = result.GeneralError;
            }
            else
            {
                SummaryText.Text = $"Failed to write configuration: {result.GetFirstErrorMessage()}";
            }
        }

        // Display detailed results (REQ-3.5.5-008)
        DetailsText.Text = result.GetDetailedSummary();
    }

    /// <summary>
    /// Close dialog when OK button is clicked
    /// </summary>
    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogConfig($"User acknowledged configuration result ({(_result.Success ? "Success" : "Failure")})");
        DialogResult = true;
        Close();
    }
}
