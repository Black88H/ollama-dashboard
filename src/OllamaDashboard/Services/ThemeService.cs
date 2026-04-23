using System.Windows;
using Microsoft.Win32;
using OllamaDashboard.Models;

namespace OllamaDashboard.Services;

public sealed class ThemeService : IThemeService, IDisposable
{
    private const string LightUri = "pack://application:,,,/OllamaDashboard;component/Themes/Colors.xaml";
    private const string DarkUri  = "pack://application:,,,/OllamaDashboard;component/Themes/DarkColors.xaml";

    private AppTheme _current = AppTheme.System;

    public event EventHandler? ThemeChanged;

    public ThemeService()
    {
        SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
    }

    public void Apply(AppTheme theme)
    {
        _current = theme;
        var effective = ResolveEffective(theme);
        var uri = new Uri(effective == AppTheme.Dark ? DarkUri : LightUri, UriKind.Absolute);

        Application.Current.Dispatcher.Invoke(() =>
        {
            var dicts = Application.Current.Resources.MergedDictionaries;
            var existing = dicts.FirstOrDefault(d =>
                d.Source?.OriginalString.EndsWith("Colors.xaml", StringComparison.OrdinalIgnoreCase) == true);

            var newDict = new ResourceDictionary { Source = uri };

            if (existing is not null)
            {
                var idx = dicts.IndexOf(existing);
                dicts[idx] = newDict;
            }
            else
            {
                dicts.Insert(0, newDict);
            }
        });

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public AppTheme ResolveEffective(AppTheme theme) =>
        theme == AppTheme.System ? GetSystemTheme() : theme;

    private static AppTheme GetSystemTheme()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return value is int v && v == 0 ? AppTheme.Dark : AppTheme.Light;
        }
        catch
        {
            return AppTheme.Light;
        }
    }

    private void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && _current == AppTheme.System)
            Apply(AppTheme.System);
    }

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
}
