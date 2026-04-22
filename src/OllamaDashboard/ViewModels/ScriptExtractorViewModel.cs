using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using OllamaDashboard.Models;
using OllamaDashboard.Services;

namespace OllamaDashboard.ViewModels;

public partial class ScriptExtractorViewModel : ObservableObject
{
    private readonly IOllamaService _ollama;
    private readonly IPdfService _pdf;
    private readonly ILogger<ScriptExtractorViewModel> _logger;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private PdfExtractionResult? _loadedPdf;

    [ObservableProperty]
    private string _status = "Ziehe ein Skriptum hierher oder klicke 'PDF auswählen'.";

    [ObservableProperty]
    private int _detailLevel = 3; // 1..5 — binds to SummaryDetailLevel enum values

    [ObservableProperty]
    private string _resultPreview = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isPdfLoading;

    [ObservableProperty]
    private string? _lastGeneratedPdfPath;

    public bool HasPdf => LoadedPdf is not null;
    public bool HasResult => !string.IsNullOrWhiteSpace(ResultPreview);

    public string DetailLevelLabel => DetailLevel switch
    {
        1 => "Sehr kurz — nur Kernkonzepte (~5 %)",
        2 => "Kurz — wichtigste Prüfungspunkte (~10 %)",
        3 => "Mittel — ausgewogene Zusammenfassung (~20 %)",
        4 => "Detailliert — mit Beispielen (~35 %)",
        5 => "Sehr detailliert — fast vollständig (~50 %)",
        _ => "Mittel"
    };

    public ScriptExtractorViewModel(
        IOllamaService ollama,
        IPdfService pdf,
        ILogger<ScriptExtractorViewModel> logger)
    {
        _ollama = ollama;
        _pdf = pdf;
        _logger = logger;
    }

    partial void OnLoadedPdfChanged(PdfExtractionResult? value)
    {
        OnPropertyChanged(nameof(HasPdf));
        ExtractCommand.NotifyCanExecuteChanged();
    }
    partial void OnResultPreviewChanged(string value)
    {
        OnPropertyChanged(nameof(HasResult));
        ExportPdfCommand.NotifyCanExecuteChanged();
    }
    partial void OnIsBusyChanged(bool value)
    {
        ExtractCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        ExportPdfCommand.NotifyCanExecuteChanged();
    }
    partial void OnDetailLevelChanged(int value) =>
        OnPropertyChanged(nameof(DetailLevelLabel));

    [RelayCommand]
    private async Task PickPdfAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "PDF-Dateien (*.pdf)|*.pdf",
            Title = "Skriptum auswählen"
        };
        if (dlg.ShowDialog() == true)
        {
            await LoadPdfAsync(dlg.FileName);
        }
    }

    public async Task LoadPdfAsync(string path)
    {
        if (IsPdfLoading) return;

        try
        {
            IsPdfLoading = true;
            Status = $"Lese {Path.GetFileName(path)}…";
            LoadedPdf = await _pdf.ExtractTextAsync(path);
            Status = $"{LoadedPdf.FileName} · {LoadedPdf.PageCount} Seiten · ~{LoadedPdf.EstimatedTokens:N0} Tokens";
            ResultPreview = string.Empty;
            LastGeneratedPdfPath = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load script");
            Status = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsPdfLoading = false;
        }
    }

    [RelayCommand]
    private void ClearPdf()
    {
        LoadedPdf = null;
        ResultPreview = string.Empty;
        LastGeneratedPdfPath = null;
        Status = "Ziehe ein Skriptum hierher oder klicke 'PDF auswählen'.";
    }

    [RelayCommand(CanExecute = nameof(CanExtract))]
    private async Task ExtractAsync()
    {
        if (LoadedPdf is null) return;

        ResultPreview = string.Empty;
        IsBusy = true;
        _cts = new CancellationTokenSource();

        try
        {
            var prompt = BuildExtractionPrompt();
            var messages = new[]
            {
                new ChatMessage { Role = ChatRole.System, Content = SystemPrompt },
                new ChatMessage { Role = ChatRole.User,   Content = prompt }
            };

            await foreach (var chunk in _ollama.StreamChatAsync(messages, ct: _cts.Token))
            {
                ResultPreview += chunk;
            }
        }
        catch (OperationCanceledException)
        {
            ResultPreview += "\n\n*[Abgebrochen]*";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extraction failed");
            MessageBox.Show(ex.Message, "Fehler bei der Extraktion",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool CanExtract() => HasPdf && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();
    private bool CanCancel() => IsBusy;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportPdfAsync()
    {
        if (!HasResult || LoadedPdf is null) return;

        var dlg = new SaveFileDialog
        {
            Filter = "PDF-Dateien (*.pdf)|*.pdf",
            FileName = Path.GetFileNameWithoutExtension(LoadedPdf.FileName) + "_Zusammenfassung.pdf",
            Title = "Zusammenfassung speichern"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            await _pdf.GenerateSummaryPdfAsync(
                dlg.FileName,
                $"Zusammenfassung — {Path.GetFileNameWithoutExtension(LoadedPdf.FileName)}",
                ResultPreview,
                LoadedPdf.FileName);

            LastGeneratedPdfPath = dlg.FileName;
            MessageBox.Show($"PDF gespeichert:\n{dlg.FileName}", "Erfolgreich",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF export failed");
            MessageBox.Show(ex.Message, "Fehler beim Speichern",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanExport() => HasResult && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanOpenLastPdf))]
    private void OpenLastPdf()
    {
        if (!string.IsNullOrEmpty(LastGeneratedPdfPath) && File.Exists(LastGeneratedPdfPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = LastGeneratedPdfPath,
                UseShellExecute = true
            });
        }
    }
    private bool CanOpenLastPdf() =>
        !string.IsNullOrEmpty(LastGeneratedPdfPath) && File.Exists(LastGeneratedPdfPath);

    partial void OnLastGeneratedPdfPathChanged(string? value) =>
        OpenLastPdfCommand.NotifyCanExecuteChanged();

    // ---------------------------------------------------------------------

    private const string SystemPrompt =
        "Du bist ein erfahrener Tutor, der Studierenden hilft, prüfungsrelevanten Stoff aus " +
        "umfangreichen Skripten herauszufiltern. Antworte strukturiert in Markdown: " +
        "Benutze `# Überschrift`, `## Unterüberschrift`, und `- Aufzählungspunkte`. " +
        "Formuliere klar, faktentreu und ohne Füllwörter.";

    private string BuildExtractionPrompt()
    {
        var targetLength = DetailLevel switch
        {
            1 => "ca. 5 % der Originallänge — nur die absolute Essenz",
            2 => "ca. 10 % der Originallänge — die wichtigsten Prüfungspunkte",
            3 => "ca. 20 % der Originallänge — eine ausgewogene Zusammenfassung",
            4 => "ca. 35 % der Originallänge — mit Beispielen und wichtigen Details",
            5 => "ca. 50 % der Originallänge — nahezu vollständig, nur redundanzfrei",
            _ => "eine mittellange Zusammenfassung"
        };

        return $$"""
        Extrahiere den prüfungsrelevanten Stoff aus dem folgenden Skriptum.

        Ziel-Länge: {{targetLength}}.

        Struktur:
        1. Eine kurze Einleitung, die das Thema in 2-3 Sätzen umreißt.
        2. Die wichtigsten Konzepte, jeweils als Unterabschnitt mit `## Überschrift`.
        3. Kernaussagen als Bullet-Points, Fachbegriffe in **fett**.
        4. Wenn passend: typische Prüfungsfragen am Ende als Aufzählung.

        Konzentriere dich auf das, was für eine Klausur wirklich relevant ist.
        Lass historische Ausschweifungen, Marketing-Phrasen und Wiederholungen weg.

        === SKRIPTUM START ===
        {{TruncateForContext(LoadedPdf!.FullText)}}
        === SKRIPTUM ENDE ===
        """;
    }

    private static string TruncateForContext(string text, int maxChars = 60_000)
    {
        if (text.Length <= maxChars) return text;
        return text[..maxChars] + "\n\n[…Inhalt gekürzt — Skriptum zu lang für Kontextfenster…]";
    }
}
