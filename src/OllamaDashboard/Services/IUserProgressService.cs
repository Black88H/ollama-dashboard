using OllamaDashboard.Models;

namespace OllamaDashboard.Services;

public interface IUserProgressService
{
    /// <summary>Awards XP for a study event. Handles streak updates automatically.</summary>
    Task AwardXpAsync(StudyEventType eventType);

    /// <summary>Should be called once at startup after DB is initialized.</summary>
    Task OnDailyLoginAsync();

    /// <summary>Refreshes the in-memory user snapshot from DB.</summary>
    Task RefreshAsync();
}
