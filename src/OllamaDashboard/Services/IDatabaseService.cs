using OllamaDashboard.Data;
using OllamaDashboard.Models;

namespace OllamaDashboard.Services;

public interface IDatabaseService
{
    bool IsInitialized { get; }
    User? CurrentUser { get; }

    /// <summary>Opens the encrypted DB with the derived key and seeds the schema.</summary>
    Task InitializeAsync(string hexKey);

    /// <summary>Creates a new short-lived DbContext for one unit of work.</summary>
    StudyCoachDbContext CreateContext();

    /// <summary>Loads/refreshes the current user from DB.</summary>
    Task<User> GetOrCreateUserAsync(string displayName = "Lernender");
}
