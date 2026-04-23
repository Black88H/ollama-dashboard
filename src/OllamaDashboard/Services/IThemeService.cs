using OllamaDashboard.Models;

namespace OllamaDashboard.Services;

public interface IThemeService
{
    void Apply(AppTheme theme);
    AppTheme ResolveEffective(AppTheme theme);
    event EventHandler? ThemeChanged;
}
