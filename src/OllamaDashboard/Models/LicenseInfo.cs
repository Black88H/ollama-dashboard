namespace OllamaDashboard.Models;

public enum Feature
{
    GroqIntegration,
    UnlimitedSubjects,
    UnlimitedFlashcards,
    PdfExport,
    AdvancedExtraction,
    XpSystem
}

public static class FeatureLimits
{
    // Free tier limits
    public const int FreeMaxSubjects   = 3;
    public const int FreeMaxFlashcards = 50;

    public static bool IsAvailable(Feature feature, LicenseTier tier) => tier switch
    {
        LicenseTier.Pro  => true,
        LicenseTier.Free => feature switch
        {
            Feature.GroqIntegration    => true, // API key is user's own
            Feature.XpSystem           => true,
            Feature.PdfExport          => false,
            Feature.UnlimitedSubjects  => false,
            Feature.UnlimitedFlashcards => false,
            Feature.AdvancedExtraction => false,
            _ => true
        },
        _ => false
    };
}
