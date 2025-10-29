using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EtherNetIPTool.Controls;

/// <summary>
/// IP address input control with 4 octet boxes (REQ-3.5.3-003)
/// Each octet accepts numeric values 0-255 only
/// </summary>
public partial class IpOctetInput : UserControl
{
    /// <summary>
    /// Dependency property for IP Address binding
    /// </summary>
    public static readonly DependencyProperty IPAddressProperty =
        DependencyProperty.Register(
            nameof(IPAddress),
            typeof(IPAddress),
            typeof(IpOctetInput),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnIPAddressChanged));

    /// <summary>
    /// IP Address value (bindable)
    /// </summary>
    public IPAddress? IPAddress
    {
        get => (IPAddress?)GetValue(IPAddressProperty);
        set => SetValue(IPAddressProperty, value);
    }

    /// <summary>
    /// Event raised when IP address changes
    /// </summary>
    public event EventHandler? IPAddressChanged;

    private bool _isUpdatingFromProperty;

    public IpOctetInput()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Called when IPAddress property changes from binding
    /// </summary>
    private static void OnIPAddressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is IpOctetInput control)
        {
            control.UpdateOctetsFromIPAddress();
        }
    }

    /// <summary>
    /// Update octet text boxes from IPAddress property
    /// </summary>
    private void UpdateOctetsFromIPAddress()
    {
        if (_isUpdatingFromProperty)
            return;

        _isUpdatingFromProperty = true;

        try
        {
            if (IPAddress != null && IPAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var bytes = IPAddress.GetAddressBytes();
                Octet1.Text = bytes[0].ToString();
                Octet2.Text = bytes[1].ToString();
                Octet3.Text = bytes[2].ToString();
                Octet4.Text = bytes[3].ToString();
            }
            else
            {
                Octet1.Text = string.Empty;
                Octet2.Text = string.Empty;
                Octet3.Text = string.Empty;
                Octet4.Text = string.Empty;
            }
        }
        finally
        {
            _isUpdatingFromProperty = false;
        }
    }

    /// <summary>
    /// Update IPAddress property from octet text boxes
    /// </summary>
    private void UpdateIPAddressFromOctets()
    {
        if (_isUpdatingFromProperty)
            return;

        try
        {
            if (TryGetOctets(out byte[] octets))
            {
                IPAddress = new IPAddress(octets);
                IPAddressChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                IPAddress = null;
                IPAddressChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch
        {
            IPAddress = null;
            IPAddressChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Try to parse all 4 octets as bytes
    /// </summary>
    private bool TryGetOctets(out byte[] octets)
    {
        octets = new byte[4];

        if (!byte.TryParse(Octet1.Text, out octets[0]))
            return false;
        if (!byte.TryParse(Octet2.Text, out octets[1]))
            return false;
        if (!byte.TryParse(Octet3.Text, out octets[2]))
            return false;
        if (!byte.TryParse(Octet4.Text, out octets[3]))
            return false;

        return true;
    }

    /// <summary>
    /// Validate numeric input only (REQ-3.5.3-003: 0-255)
    /// </summary>
    private void Octet_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Allow digits only
        if (!int.TryParse(e.Text, out _))
        {
            e.Handled = true;
            return;
        }

        // Check if resulting value would be valid (0-255)
        if (sender is TextBox textBox)
        {
            string newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            if (int.TryParse(newText, out int value))
            {
                if (value > 255)
                {
                    e.Handled = true;
                }
            }
        }
    }

    /// <summary>
    /// Handle text changed - auto-advance to next octet
    /// </summary>
    private void Octet_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && !_isUpdatingFromProperty)
        {
            // Auto-advance to next octet when complete
            if (textBox.Text.Length == 3 ||
                (textBox.Text.Length > 0 && int.TryParse(textBox.Text, out int value) && value > 25))
            {
                MoveFocusToNextOctet(textBox);
            }

            // Update IP address property
            UpdateIPAddressFromOctets();
        }
    }

    /// <summary>
    /// Select all text on focus
    /// </summary>
    private void Octet_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.SelectAll();
        }
    }

    /// <summary>
    /// Handle special keys (backspace, period, arrows)
    /// </summary>
    private void Octet_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        // Period moves to next octet
        if (e.Key == Key.OemPeriod || e.Key == Key.Decimal)
        {
            e.Handled = true;
            MoveFocusToNextOctet(textBox);
            return;
        }

        // Backspace on empty field moves to previous octet
        if (e.Key == Key.Back && textBox.Text.Length == 0)
        {
            e.Handled = true;
            MoveFocusToPreviousOctet(textBox);
            return;
        }

        // Arrow keys move between octets
        if (e.Key == Key.Right && textBox.CaretIndex == textBox.Text.Length)
        {
            MoveFocusToNextOctet(textBox);
            return;
        }

        if (e.Key == Key.Left && textBox.CaretIndex == 0)
        {
            MoveFocusToPreviousOctet(textBox);
            return;
        }
    }

    /// <summary>
    /// Move focus to next octet field
    /// </summary>
    private void MoveFocusToNextOctet(TextBox currentOctet)
    {
        if (currentOctet == Octet1)
            Octet2.Focus();
        else if (currentOctet == Octet2)
            Octet3.Focus();
        else if (currentOctet == Octet3)
            Octet4.Focus();
    }

    /// <summary>
    /// Move focus to previous octet field
    /// </summary>
    private void MoveFocusToPreviousOctet(TextBox currentOctet)
    {
        if (currentOctet == Octet4)
            Octet3.Focus();
        else if (currentOctet == Octet3)
            Octet2.Focus();
        else if (currentOctet == Octet2)
            Octet1.Focus();
    }

    /// <summary>
    /// Check if all octets are filled with valid values
    /// </summary>
    public bool IsValid()
    {
        return TryGetOctets(out _);
    }

    /// <summary>
    /// Clear all octet fields
    /// </summary>
    public void Clear()
    {
        Octet1.Text = string.Empty;
        Octet2.Text = string.Empty;
        Octet3.Text = string.Empty;
        Octet4.Text = string.Empty;
    }
}
