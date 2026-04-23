using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace OllamaDashboard.Services;

/// <summary>
/// Handles master-password verification and SQLCipher key derivation.
///
/// auth.meta layout (JSON, not encrypted):
///   authSalt   – 16-byte random salt for password verification
///   dbKeySalt  – 16-byte random salt for DB-key derivation (fixed after first run)
///   authHash   – 32-byte Argon2id(password, authSalt) for verification
///
/// The DB key is NEVER stored — it is re-derived on every login:
///   dbKey = hex( Argon2id(password, dbKeySalt, 32 bytes) )
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly ILogger<AuthService> _logger;

    private static readonly string MetaPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OllamaDashboard", "auth.meta");

    // Argon2id parameters — deliberately expensive
    private const int Parallelism = 2;
    private const int Iterations  = 4;
    private const int MemoryKb    = 65_536; // 64 MB

    public bool IsAuthenticated { get; private set; }
    public bool HasSetup        => File.Exists(MetaPath);

    public AuthService(ILogger<AuthService> logger) => _logger = logger;

    public async Task<string> SetupAsync(string password, string displayName)
    {
        var authSalt  = RandomBytes(16);
        var dbKeySalt = RandomBytes(16);

        var authHash = await Argon2HashAsync(password, authSalt, 32);
        var dbKey    = await Argon2HashAsync(password, dbKeySalt, 32);

        var meta = new AuthMeta
        {
            AuthSalt  = Convert.ToBase64String(authSalt),
            DbKeySalt = Convert.ToBase64String(dbKeySalt),
            AuthHash  = Convert.ToBase64String(authHash),
            DisplayName = displayName
        };

        Directory.CreateDirectory(Path.GetDirectoryName(MetaPath)!);
        await File.WriteAllTextAsync(MetaPath,
            JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));

        IsAuthenticated = true;
        _logger.LogInformation("Auth setup complete for {Name}", displayName);
        return Convert.ToHexString(dbKey).ToLowerInvariant();
    }

    public async Task<string> LoginAsync(string password)
    {
        var json = await File.ReadAllTextAsync(MetaPath);
        var meta = JsonSerializer.Deserialize<AuthMeta>(json)
                   ?? throw new InvalidDataException("auth.meta konnte nicht gelesen werden.");

        var authSalt  = Convert.FromBase64String(meta.AuthSalt);
        var dbKeySalt = Convert.FromBase64String(meta.DbKeySalt);
        var storedHash = Convert.FromBase64String(meta.AuthHash);

        var attemptHash = await Argon2HashAsync(password, authSalt, 32);

        if (!CryptographicOperations.FixedTimeEquals(attemptHash, storedHash))
        {
            _logger.LogWarning("Login failed — wrong master password");
            throw new UnauthorizedAccessException("Falsches Master-Passwort.");
        }

        var dbKey = await Argon2HashAsync(password, dbKeySalt, 32);
        IsAuthenticated = true;
        _logger.LogInformation("Login successful");
        return Convert.ToHexString(dbKey).ToLowerInvariant();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Task<byte[]> Argon2HashAsync(string password, byte[] salt, int length)
    {
        return Task.Run(() =>
        {
            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt                 = salt,
                DegreeOfParallelism  = Parallelism,
                Iterations           = Iterations,
                MemorySize           = MemoryKb
            };
            return argon2.GetBytes(length);
        });
    }

    private static byte[] RandomBytes(int count)
    {
        var b = new byte[count];
        RandomNumberGenerator.Fill(b);
        return b;
    }

    private sealed class AuthMeta
    {
        public string AuthSalt    { get; set; } = string.Empty;
        public string DbKeySalt   { get; set; } = string.Empty;
        public string AuthHash    { get; set; } = string.Empty;
        public string DisplayName { get; set; } = "Lernender";
    }
}
