using System.Windows;

namespace StalkerModLauncher.Infrastructure;

public enum UiSoundKind
{
    Default,
    ProfileActionsToggle
}

public static class UiSound
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.RegisterAttached(
        "Kind",
        typeof(UiSoundKind),
        typeof(UiSound),
        new PropertyMetadata(UiSoundKind.Default));

    public static void SetKind(DependencyObject element, UiSoundKind value)
    {
        element.SetValue(KindProperty, value);
    }

    public static UiSoundKind GetKind(DependencyObject element)
    {
        return (UiSoundKind)element.GetValue(KindProperty);
    }
}
