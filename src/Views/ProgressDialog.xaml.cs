using System.ComponentModel;
using System.Windows;

namespace EtherNetIPTool.Views;

/// <summary>
/// Progress dialog for configuration write operations (REQ-3.5.5-006)
/// Shows "Sending configuration... (X/Y)" during write operations
/// </summary>
public partial class ProgressDialog : Window
{
    private bool _canClose = false;

    public ProgressDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Update progress (REQ-3.5.5-006)
    /// </summary>
    /// <param name="current">Current step (1-based)</param>
    /// <param name="total">Total steps</param>
    /// <param name="operationName">Name of current operation</param>
    public void UpdateProgress(int current, int total, string operationName)
    {
        // Update on UI thread
        Dispatcher.Invoke(() =>
        {
            // REQ-3.5.5-006: "Sending configuration... (X/Y)"
            ProgressText.Text = $"Sending configuration... ({current}/{total})";
            CurrentOperationText.Text = $"Writing: {operationName}";

            // Update progress bar
            double percentage = (double)current / total * 100;
            ProgressBar.Value = percentage;
        });
    }

    /// <summary>
    /// Mark operation as complete and allow closing
    /// </summary>
    public void Complete()
    {
        Dispatcher.Invoke(() =>
        {
            _canClose = true;
            Close();
        });
    }

    /// <summary>
    /// Prevent user from closing dialog during write operation
    /// </summary>
    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_canClose)
        {
            e.Cancel = true;
        }
    }
}
