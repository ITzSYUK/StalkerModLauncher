using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace StalkerModLauncher.Infrastructure;

public static class SmoothScrollBehavior
{
    private const double PixelsPerWheelStep = 76;
    private static readonly ConditionalWeakTable<FrameworkElement, SmoothScrollState> States = new();

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(SmoothScrollBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        element.PreviewMouseWheel -= Element_OnPreviewMouseWheel;
        element.Unloaded -= Element_OnUnloaded;

        if (e.NewValue is true)
        {
            element.PreviewMouseWheel += Element_OnPreviewMouseWheel;
            element.Unloaded += Element_OnUnloaded;
        }
        else if (States.TryGetValue(element, out var state))
        {
            state.Stop();
        }
    }

    private static void Element_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not FrameworkElement element || FindVisualChild<ScrollViewer>(element) is not { } scrollViewer)
        {
            return;
        }

        var wheelSteps = e.Delta / 120d;
        if (wheelSteps == 0 || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        var state = States.GetValue(element, static _ => new SmoothScrollState());
        if (state.Scroll(scrollViewer, -wheelSteps * PixelsPerWheelStep))
        {
            e.Handled = true;
        }
    }

    private static void Element_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && States.TryGetValue(element, out var state))
        {
            state.Stop();
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            if (FindVisualChild<T>(child) is { } nestedMatch)
            {
                return nestedMatch;
            }
        }

        return null;
    }

    private sealed class SmoothScrollState
    {
        private readonly DispatcherTimer _timer;
        private WeakReference<ScrollViewer>? _scrollViewer;
        private double _targetOffset;

        public SmoothScrollState()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(15)
            };
            _timer.Tick += Timer_OnTick;
        }

        public bool Scroll(ScrollViewer scrollViewer, double offsetDelta)
        {
            if (!_timer.IsEnabled || _scrollViewer is null || !_scrollViewer.TryGetTarget(out var currentViewer) ||
                !ReferenceEquals(currentViewer, scrollViewer))
            {
                _targetOffset = scrollViewer.VerticalOffset;
            }

            _scrollViewer = new WeakReference<ScrollViewer>(scrollViewer);
            var nextTarget = Math.Clamp(_targetOffset + offsetDelta, 0, scrollViewer.ScrollableHeight);
            if (Math.Abs(nextTarget - scrollViewer.VerticalOffset) < 0.5 && !_timer.IsEnabled)
            {
                return false;
            }

            _targetOffset = nextTarget;
            _timer.Start();
            return true;
        }

        public void Stop() => _timer.Stop();

        private void Timer_OnTick(object? sender, EventArgs e)
        {
            if (_scrollViewer is null || !_scrollViewer.TryGetTarget(out var scrollViewer))
            {
                _timer.Stop();
                return;
            }

            _targetOffset = Math.Clamp(_targetOffset, 0, scrollViewer.ScrollableHeight);
            var difference = _targetOffset - scrollViewer.VerticalOffset;
            if (Math.Abs(difference) < 0.35)
            {
                scrollViewer.ScrollToVerticalOffset(_targetOffset);
                _timer.Stop();
                return;
            }

            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + difference * 0.26);
        }
    }
}
