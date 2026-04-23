using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using OllamaDashboard.Models;

namespace OllamaDashboard.Services;

/// <summary>
/// Simple HMAC-SHA256 license validator.
///
/// Key format (base64url, no padding):
///   tier(1 byte) | expiry_unix_days(4 bytes, big-endian) | hmac_truncated(16 bytes)
///   → 21 bytes → 28-char base64url string
///
/// HMAC secret is embedded — appropriate for an indie desktop app.
/// For higher assurance, replace with RSA/Ed25519 signature verification.
/// </summary>
public sealed class LicenseService : ILicenseService
{
    // Change this secret before shipping; bake a different one per release if desired.
    private static readonly byte[] HmacSecret =
        Encoding.UTF8.GetBytes("SC-AI-2025-License-Secret-K7x9mQ");

    private readonly IDatabaseService _db;
    private readonly ILogger<LicenseService> _logger;

    public LicenseTier CurrentTier { get; private set; } = LicenseTier.Free;

    public LicenseService(IDatabaseService db, ILogger<LicenseService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public bool IsFeatureEnabled(Feature feature)
        => FeatureLimits.IsAvailable(feature, CurrentTier);

    /// <summary>Loads the tier from the current DB user if available.</summary>
    public void SyncFromUser()
    {
        if (_db.CurrentUser is { } user)
            CurrentTier = user.Tier;
    }

    public async Task<bool> ActivateAsync(string licenseKey)
    {
        if (!Validate(licenseKey, out var tier))
        {
            _logger.LogWarning("License validation failed for key {Key}", licenseKey[..6] + "…");
            return false;
        }

        CurrentTier = tier;

        if (_db.IsInitialized && _db.CurrentUser is { } user)
        {
            using var ctx = _db.CreateContext();
            var u = await ctx.Users.FindAsync(user.Id);
            if (u is not null)
            {
                u.Tier       = tier;
                u.LicenseKey = licenseKey;
                await ctx.SaveChangesAsync();
                _db.CurrentUser.Tier       = tier;
                _db.CurrentUser.LicenseKey = licenseKey;
            }
        }

        _logger.LogInformation("License activated — tier: {Tier}", tier);
        return true;
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private static bool Validate(string key, out LicenseTier tier)
    {
        tier = LicenseTier.Free;
        try
        {
            // Restore base64url padding
            var padded = key.PadRight(key.Length + (4 - key.Length % 4) % 4, '=')
                            .Replace('-', '+').Replace('_', '/');
            var bytes = Convert.FromBase64String(padded);
            if (bytes.Length != 21) return false;

            var tierByte    = bytes[0];
            var expiryDays  = (bytes[1] << 24) | (bytes[2] << 16) | (bytes[3] << 8) | bytes[4];
            var storedHmac  = bytes[6..]; // 16 bytes (index 5 reserved)

            // Expiry check (days since Unix epoch)
            var nowDays = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400);
            if (expiryDays > 0 && nowDays > expiryDays) return false;

            // HMAC verification
            var payload = bytes[..5];
            using var hmac = new HMACSHA256(HmacSecret);
            var expected = hmac.ComputeHash(payload)[..16];
            if (!CryptographicOperations.FixedTimeEquals(storedHmac, expected)) return false;

            tier = tierByte == 1 ? LicenseTier.Pro : LicenseTier.Free;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
