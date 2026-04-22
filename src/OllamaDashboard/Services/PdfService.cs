using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace OllamaDashboard.Services;

public sealed class PdfService : IPdfService
{
    private readonly ILogger<PdfService> _logger;

    public PdfService(ILogger<PdfService> logger)
    {
        _logger = logger;
        // QuestPDF community license (free for small business & individuals).
        // Switch to LicenseType.Professional if you hold a commercial license.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<PdfExtractionResult> ExtractTextAsync(string pdfPath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (!File.Exists(pdfPath))
                throw new FileNotFoundException("PDF not found", pdfPath);

            using var doc = PdfDocument.Open(pdfPath);
            var pages = new List<string>(doc.NumberOfPages);
            var full = new System.Text.StringBuilder();

            foreach (Page page in doc.GetPages())
            {
                ct.ThrowIfCancellationRequested();

                // ContentOrderTextExtractor preserves reading order better than page.Text
                var text = ContentOrderTextExtractor.GetText(page) ?? string.Empty;
                pages.Add(text);

                full.Append(text);
                full.Append("\n\n");
            }

            return new PdfExtractionResult
            {
                FullText = full.ToString().Trim(),
                PageTexts = pages,
                FileName = Path.GetFileName(pdfPath)
            };
        }, ct);
    }

    public Task GenerateSummaryPdfAsync(
        string outputPath,
        string title,
        string bodyText,
        string? sourceFileName = null,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Calibri));

                    page.Header().Column(col =>
                    {
                        col.Item().Text(title).FontSize(20).SemiBold();
                        if (!string.IsNullOrWhiteSpace(sourceFileName))
                        {
                            col.Item().PaddingTop(2).Text($"Quelle: {sourceFileName}")
                                .FontSize(9).FontColor(Colors.Grey.Medium);
                        }
                        col.Item().PaddingTop(2).Text($"Erstellt: {DateTime.Now:dd.MM.yyyy HH:mm}")
                            .FontSize(9).FontColor(Colors.Grey.Medium);
                        col.Item().PaddingTop(10)
                            .LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().PaddingVertical(12).Column(col =>
                    {
                        foreach (var paragraph in SplitIntoParagraphs(bodyText))
                        {
                            RenderParagraph(col, paragraph);
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Seite ").FontSize(9).FontColor(Colors.Grey.Medium);
                        x.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Medium);
                        x.Span(" / ").FontSize(9).FontColor(Colors.Grey.Medium);
                        x.TotalPages().FontSize(9).FontColor(Colors.Grey.Medium);
                    });
                });
            }).GeneratePdf(outputPath);

            _logger.LogInformation("Summary PDF written: {Path}", outputPath);
        }, ct);
    }

    private static IEnumerable<string> SplitIntoParagraphs(string text) =>
        text.Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0);

    private static void RenderParagraph(ColumnDescriptor col, string paragraph)
    {
        // Very lightweight markdown: # H1, ## H2, - bullet
        if (paragraph.StartsWith("# "))
        {
            col.Item().PaddingTop(8).Text(paragraph[2..].Trim()).FontSize(16).SemiBold();
            return;
        }
        if (paragraph.StartsWith("## "))
        {
            col.Item().PaddingTop(6).Text(paragraph[3..].Trim()).FontSize(13).SemiBold();
            return;
        }
        if (paragraph.StartsWith("- ") || paragraph.StartsWith("* "))
        {
            foreach (var line in paragraph.Split('\n'))
            {
                var cleaned = line.TrimStart('-', '*', ' ').Trim();
                if (cleaned.Length == 0) continue;
                col.Item().Row(row =>
                {
                    row.ConstantItem(14).Text("•");
                    row.RelativeItem().Text(cleaned);
                });
            }
            return;
        }
        col.Item().PaddingTop(4).Text(paragraph);
    }
}
