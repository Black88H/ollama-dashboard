namespace OllamaDashboard.Services;

public interface IAuthService
{
    /// <summary>True after a successful <see cref="SetupAsync"/> or <see cref="LoginAsync"/>.</summary>
    bool IsAuthenticated { get; }

    /// <summary>True when auth.meta exists (not first run).</summary>
    bool HasSetup { get; }

    /// <summary>First-run: hashes and stores the master password, returns the DB key.</summary>
    Task<string> SetupAsync(string password, string displayName);

    /// <summary>Returns the hex DB key on success, throws <see cref="UnauthorizedAccessException"/> on wrong password.</summary>
    Task<string> LoginAsync(string password);
}
