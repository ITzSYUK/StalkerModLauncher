using System.Windows;
using System.Windows.Controls;

namespace StalkerModLauncher.Views.Controls;

public partial class ProfileSummaryView : UserControl
{
    public static readonly DependencyProperty UseCompactLayoutProperty = DependencyProperty.Register(
        nameof(UseCompactLayout),
        typeof(bool),
        typeof(ProfileSummaryView),
        new PropertyMetadata(false));

    public ProfileSummaryView()
    {
        InitializeComponent();
    }

    public bool UseCompactLayout
    {
        get => (bool)GetValue(UseCompactLayoutProperty);
        set => SetValue(UseCompactLayoutProperty, value);
    }
}
