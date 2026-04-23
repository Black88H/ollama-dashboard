using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OllamaDashboard.Models;

namespace OllamaDashboard.Services;

public sealed class UserProgressService : IUserProgressService
{
    private readonly IDatabaseService _db;
    private readonly ILogger<UserProgressService> _logger;

    public UserProgressService(IDatabaseService db, ILogger<UserProgressService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task AwardXpAsync(StudyEventType eventType)
    {
        if (!_db.IsInitialized || _db.CurrentUser is null) return;

        var xp = (int)eventType;
        using var ctx = _db.CreateContext();
        var user = await ctx.Users.FindAsync(_db.CurrentUser.Id);
        if (user is null) return;

        user.XpPoints      += xp;
        user.LastStudyDate  = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        _db.CurrentUser.XpPoints      = user.XpPoints;
        _db.CurrentUser.LastStudyDate = user.LastStudyDate;

        _logger.LogDebug("Awarded {Xp} XP for {Event} (total: {Total})", xp, eventType, user.XpPoints);
    }

    public async Task OnDailyLoginAsync()
    {
        if (!_db.IsInitialized || _db.CurrentUser is null) return;

        using var ctx = _db.CreateContext();
        var user = await ctx.Users.FindAsync(_db.CurrentUser.Id);
        if (user is null) return;

        var today     = DateTime.UtcNow.Date;
        var lastStudy = user.LastStudyDate?.ToUniversalTime().Date;

        if (lastStudy is null || lastStudy < today)
        {
            // Streak logic: +1 if studied yesterday, reset to 1 otherwise
            user.StreakDays = (lastStudy == today.AddDays(-1))
                ? user.StreakDays + 1
                : 1;

            user.XpPoints      += (int)StudyEventType.DailyLogin;
            user.LastStudyDate  = DateTime.UtcNow;
            await ctx.SaveChangesAsync();

            _db.CurrentUser.StreakDays     = user.StreakDays;
            _db.CurrentUser.XpPoints       = user.XpPoints;
            _db.CurrentUser.LastStudyDate  = user.LastStudyDate;

            _logger.LogInformation(
                "Daily login — streak: {Streak} days, XP: {Xp}", user.StreakDays, user.XpPoints);
        }
    }

    public async Task RefreshAsync()
    {
        if (!_db.IsInitialized || _db.CurrentUser is null) return;

        using var ctx = _db.CreateContext();
        var user = await ctx.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == _db.CurrentUser.Id);
        if (user is not null) _db.CurrentUser.XpPoints = user.XpPoints;
    }
}
