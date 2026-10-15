using System.Windows.Controls;
using System.Windows;

namespace LarpStrap.App.Views;

public partial class HomeView : UserControl
{
    public HomeView() => InitializeComponent();

    private void OpenJoinMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.PlacementTarget = button;
        menu.IsOpen = true;
    }
}
