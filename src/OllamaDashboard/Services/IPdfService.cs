namespace OllamaDashboard.Services;

public interface IPdfService
{
    /// <summary>
    /// Extracts plain text from a PDF file. Returns page-separated text blocks.
    /// Throws if the file is unreadable or corrupted.
    /// </summary>
    Task<PdfExtractionResult> ExtractTextAsync(string pdfPath, CancellationToken ct = default);

    /// <summary>
    /// Generates a new PDF document containing the given markdown-style text.
    /// Suitable for export of AI-generated summaries.
    /// </summary>
    Task GenerateSummaryPdfAsync(
        string outputPath,
        string title,
        string bodyText,
        string? sourceFileName = null,
        CancellationToken ct = default);
}

public sealed class PdfExtractionResult
{
    public required string FullText { get; init; }
    public required IReadOnlyList<string> PageTexts { get; init; }
    public int PageCount => PageTexts.Count;
    public required string FileName { get; init; }
    public int CharacterCount => FullText.Length;
    public int EstimatedTokens => FullText.Length / 4;
}
