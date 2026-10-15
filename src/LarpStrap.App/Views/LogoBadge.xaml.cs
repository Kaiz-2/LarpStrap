using System.Windows;
using System.Windows.Controls;

namespace LarpStrap.App.Views;

public partial class LogoBadge : UserControl
{
    public static readonly DependencyProperty OnBrandProperty = DependencyProperty.Register(
        nameof(OnBrand), typeof(bool), typeof(LogoBadge), new PropertyMetadata(false, (d, _) => ((LogoBadge)d).Apply()));

    public LogoBadge() => InitializeComponent();

    /// <summary>True when the badge sits on the brand gradient: the tile becomes translucent white with a ring.</summary>
    public bool OnBrand { get => (bool)GetValue(OnBrandProperty); set => SetValue(OnBrandProperty, value); }

    private void Apply()
    {
        Tile.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, OnBrand ? "OnBrandFaintBrush" : "BrandGradientBrush");
        Ring.Visibility = OnBrand ? Visibility.Visible : Visibility.Collapsed;
    }
}
