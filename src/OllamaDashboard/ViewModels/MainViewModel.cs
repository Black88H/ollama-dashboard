using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OllamaDashboard.Models;
using OllamaDashboard.Services;

namespace OllamaDashboard.ViewModels;

public enum AppView
{
    Dashboard,
    Chat,
    ScriptExtractor,
    Settings
}

public partial class MainViewModel : ObservableObject
{
    private readonly IModelRegistry _models;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboardActive))]
    [NotifyPropertyChangedFor(nameof(IsChatActive))]
    [NotifyPropertyChangedFor(nameof(IsScriptExtractorActive))]
    [NotifyPropertyChangedFor(nameof(IsSettingsActive))]
    private AppView _currentView = AppView.Dashboard;

    [ObservableProperty]
    private bool _isSidebarExpanded = true;

    public DashboardViewModel   Dashboard      { get; }
    public ChatViewModel        Chat           { get; }
    public ScriptExtractorViewModel ScriptExtractor { get; }
    public SettingsViewModel    Settings       { get; }

    public ReadOnlyObservableCollection<OllamaModelInfo> AvailableModels => _models.AvailableModels;

    [ObservableProperty] private string _activeModel    = string.Empty;
    [ObservableProperty] private string _modelStatus    = "Lade Modelle…";
    [ObservableProperty] private bool   _isModelPopupOpen;

    public MainViewModel(
        DashboardViewModel dashboard,
        ChatViewModel chat,
        ScriptExtractorViewModel scriptExtractor,
        SettingsViewModel settings,
        IModelRegistry models)
    {
        Dashboard       = dashboard;
        Chat            = chat;
        ScriptExtractor = scriptExtractor;
        Settings        = settings;
        _models         = models;

        ActiveModel = _models.ActiveModel;
        ModelStatus = _models.StatusMessage;

        _models.ModelsRefreshed    += (_, _) => ModelStatus = _models.StatusMessage;
        _models.ActiveModelChanged += (_, _) => ActiveModel = _models.ActiveModel;
    }

    [RelayCommand]
    private void NavigateTo(string view)
    {
        CurrentView = view switch
        {
            "Dashboard"       => AppView.Dashboard,
            "Chat"            => AppView.Chat,
            "ScriptExtractor" => AppView.ScriptExtractor,
            "Settings"        => AppView.Settings,
            _ => CurrentView
        };

        // Auto-refresh dashboard on navigate
        if (CurrentView == AppView.Dashboard)
            _ = Dashboard.RefreshAsync();
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    [RelayCommand]
    private void ToggleModelPopup() => IsModelPopupOpen = !IsModelPopupOpen;

    [RelayCommand]
    private async Task SelectModelAsync(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return;
        await _models.SetActiveModelAsync(modelName);
        IsModelPopupOpen = false;
    }

    [RelayCommand]
    private Task RefreshModelsAsync() => _models.RefreshAsync();

    public bool IsDashboardActive        => CurrentView == AppView.Dashboard;
    public bool IsChatActive             => CurrentView == AppView.Chat;
    public bool IsScriptExtractorActive  => CurrentView == AppView.ScriptExtractor;
    public bool IsSettingsActive         => CurrentView == AppView.Settings;
}
