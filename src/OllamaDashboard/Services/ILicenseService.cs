using OllamaDashboard.Models;

namespace OllamaDashboard.Services;

public interface ILicenseService
{
    LicenseTier CurrentTier { get; }
    bool IsFeatureEnabled(Feature feature);
    Task<bool> ActivateAsync(string licenseKey);
}
