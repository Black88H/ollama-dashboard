using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OllamaDashboard.Models;
using OllamaDashboard.Services;

namespace OllamaDashboard.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IDatabaseService    _db;
    private readonly ILicenseService     _license;
    private readonly IUserProgressService _progress;
    private readonly ILogger<DashboardViewModel> _logger;

    // ── User / XP ─────────────────────────────────────────────────────────────

    [ObservableProperty] private string _displayName   = "Lernender";
    [ObservableProperty] private int    _xpPoints;
    [ObservableProperty] private int    _currentLevel;
    [ObservableProperty] private double _xpProgress;   // 0–100 %
    [ObservableProperty] private int    _xpToNextLevel;
    [ObservableProperty] private int    _streakDays;
    [ObservableProperty] private string _tierLabel     = "Free";

    // ── Stats ─────────────────────────────────────────────────────────────────

    [ObservableProperty] private int _subjectCount;
    [ObservableProperty] private int _sessionCount;
    [ObservableProperty] private int _flashcardCount;
    [ObservableProperty] private int _dueFlashcardCount;

    // ── State ─────────────────────────────────────────────────────────────────

    [ObservableProperty] private bool _isLoading;

    public DashboardViewModel(
        IDatabaseService db,
        ILicenseService license,
        IUserProgressService progress,
        ILogger<DashboardViewModel> logger)
    {
        _db       = db;
        _license  = license;
        _progress = progress;
        _logger   = logger;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (!_db.IsInitialized) return;

        IsLoading = true;
        try
        {
            await _progress.RefreshAsync();
            LoadUserStats();
            await LoadDbStatsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard refresh failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadUserStats()
    {
        var user = _db.CurrentUser;
        if (user is null) return;

        DisplayName   = user.DisplayName;
        XpPoints      = user.XpPoints;
        CurrentLevel  = user.Level;
        XpToNextLevel = user.XpForNextLevel;
        XpProgress    = user.LevelProgress;
        StreakDays    = user.StreakDays;
        TierLabel     = _license.CurrentTier switch
        {
            LicenseTier.Pro  => "Pro ✓",
            _                => "Free"
        };
    }

    private async Task LoadDbStatsAsync()
    {
        using var ctx = _db.CreateContext();
        var userId = _db.CurrentUser?.Id ?? 0;

        SubjectCount  = await ctx.Subjects.CountAsync(s => s.UserId == userId);
        SessionCount  = await ctx.ChatSessions
                                 .CountAsync(cs => cs.Subject.UserId == userId);
        FlashcardCount = await ctx.Flashcards
                                  .CountAsync(f => f.Subject.UserId == userId);
        DueFlashcardCount = await ctx.Flashcards
                                     .CountAsync(f => f.Subject.UserId == userId
                                                   && (f.NextReviewDate == null
                                                       || f.NextReviewDate <= DateTime.UtcNow));
    }
}
