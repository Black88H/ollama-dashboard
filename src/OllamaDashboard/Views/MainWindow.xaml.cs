using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using OllamaDashboard.ViewModels;

namespace OllamaDashboard.Views;

public partial class MainWindow : Window
{
    private const double ExpandedWidth  = 240;
    private const double CollapsedWidth = 64;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        if (e.NewValue is MainViewModel newVm)
            newVm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsSidebarExpanded)) return;
        if (sender is not MainViewModel vm) return;
        AnimateSidebar(vm.IsSidebarExpanded);
    }

    private void AnimateSidebar(bool expand)
    {
        var from = expand ? CollapsedWidth : ExpandedWidth;
        var to   = expand ? ExpandedWidth  : CollapsedWidth;

        var anim = new DoubleAnimation(from, to, new Duration(TimeSpan.FromMilliseconds(200)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        Sidebar.BeginAnimation(FrameworkElement.WidthProperty, anim);
    }
}
