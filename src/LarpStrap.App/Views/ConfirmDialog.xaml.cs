using System.Windows;

namespace LarpStrap.App.Views;

/// <summary>Confirmation whose buttons name the action. Escape cancels; for dangerous actions Enter cancels too.</summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string confirmText, string cancelText, bool isDanger)
    {
        InitializeComponent();
        Title = title;
        TitleBlock.Text = title;
        MessageBlock.Text = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Content = cancelText;
        ConfirmButton.Style = (Style)FindResource(isDanger ? "DangerButton" : "PrimaryButton");
        ConfirmButton.IsDefault = !isDanger;
        if (isDanger)
        {
            IconBlock.Text = ""; // warning
            IconBlock.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
            IconTile.Background = (System.Windows.Media.Brush)FindResource("ErrorSoftBrush");
        }
        CancelButton.IsDefault = isDanger;
        Loaded += (_, _) => (isDanger ? CancelButton : ConfirmButton).Focus();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
