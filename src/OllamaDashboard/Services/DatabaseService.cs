using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OllamaDashboard.Data;
using OllamaDashboard.Models;

namespace OllamaDashboard.Services;

public sealed class DatabaseService : IDatabaseService
{
    private readonly ILogger<DatabaseService> _logger;
    private string? _connectionString;

    public bool  IsInitialized { get; private set; }
    public User? CurrentUser   { get; private set; }

    private string DbPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OllamaDashboard", "studycoach.db");

    public DatabaseService(ILogger<DatabaseService> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync(string hexKey)
    {
        // SQLCipher password in connection string — no KDF bypass needed for this threat model.
        _connectionString = $"Data Source={DbPath};Password={hexKey};";

        using var ctx = CreateContext();

        // EnsureCreated creates the schema if the file is new / newly decrypted.
        await ctx.Database.EnsureCreatedAsync();

        IsInitialized = true;
        _logger.LogInformation("StudyCoach DB initialized at {Path}", DbPath);
    }

    public StudyCoachDbContext CreateContext()
    {
        if (_connectionString is null)
            throw new InvalidOperationException("DB not initialized — call InitializeAsync first.");
        return new StudyCoachDbContext(_connectionString);
    }

    public async Task<User> GetOrCreateUserAsync(string displayName = "Lernender")
    {
        using var ctx = CreateContext();

        var user = await ctx.Users
            .Include(u => u.Subjects)
            .FirstOrDefaultAsync();

        if (user is null)
        {
            user = new User { DisplayName = displayName, CreatedAt = DateTime.UtcNow };
            ctx.Users.Add(user);
            await ctx.SaveChangesAsync();
            _logger.LogInformation("Created first-run User (Id={Id})", user.Id);
        }

        CurrentUser = user;
        return user;
    }
}
