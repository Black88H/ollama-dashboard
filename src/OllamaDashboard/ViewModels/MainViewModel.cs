using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OllamaDashboard.Models;
using OllamaDashboard.Services;

namespace OllamaDashboard.ViewModels;

public enum AppView
{
    Chat,
    ScriptExtractor,
    Settings
}

public partial class MainViewModel : ObservableObject
{
    private readonly IModelRegistry _models;

    [ObservableProperty]
    private AppView _currentView = AppView.Chat;

    public ChatViewModel Chat { get; }
    public ScriptExtractorViewModel ScriptExtractor { get; }
    public SettingsViewModel Settings { get; }

    /// <summary>Installed models, bound directly to the sidebar popup.</summary>
    public ReadOnlyObservableCollection<OllamaModelInfo> AvailableModels => _models.AvailableModels;

    /// <summary>The currently active model (surfaces in sidebar pill + Chat + Extractor).</summary>
    [ObservableProperty]
    private string _activeModel = string.Empty;

    [ObservableProperty]
    private string _modelStatus = "Lade Modelle…";

    [ObservableProperty]
    private bool _isModelPopupOpen;

    public MainViewModel(
        ChatViewModel chat,
        ScriptExtractorViewModel scriptExtractor,
        SettingsViewModel settings,
        IModelRegistry models)
    {
        Chat = chat;
        ScriptExtractor = scriptExtractor;
        Settings = settings;
        _models = models;

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
            "Chat"            => AppView.Chat,
            "ScriptExtractor" => AppView.ScriptExtractor,
            "Settings"        => AppView.Settings,
            _ => CurrentView
        };
    }

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

    public bool IsChatActive            => CurrentView == AppView.Chat;
    public bool IsScriptExtractorActive => CurrentView == AppView.ScriptExtractor;
    public bool IsSettingsActive        => CurrentView == AppView.Settings;

    partial void OnCurrentViewChanged(AppView value)
    {
        OnPropertyChanged(nameof(IsChatActive));
        OnPropertyChanged(nameof(IsScriptExtractorActive));
        OnPropertyChanged(nameof(IsSettingsActive));
    }
}
