using System.IO;
using System.Text;
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
    private readonly IModelRegistry _models;
    private readonly IScriptAnalysisService _analysis;
    private readonly ILogger<ScriptExtractorViewModel> _logger;
    private CancellationTokenSource? _cts;

    // --- Main script -----------------------------------------------------------

    [ObservableProperty]
    private PdfExtractionResult? _loadedPdf;

    [ObservableProperty]
    private string _status = "Ziehe ein Skriptum hierher oder klicke 'PDF auswählen'.";

    [ObservableProperty]
    private int _detailLevel = 3; // 1..5 maps to SummaryDetailLevel enum values

    [ObservableProperty]
    private string _resultPreview = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isPdfLoading;

    [ObservableProperty]
    private string? _lastGeneratedPdfPath;

    // --- Feature 1: Targeted extraction ----------------------------------------

    [ObservableProperty]
    private string _focusTopics = string.Empty;

    // --- Feature 2: Style & Structure Mimicry ----------------------------------

    [ObservableProperty]
    private string? _referencePdfName;

    [ObservableProperty]
    private bool _isReferencePdfLoading;

    private StyleGuideline? _styleGuideline;

    // --- Feature 3: Auto-Model-Selection ---------------------------------------

    [ObservableProperty]
    private string _modelRecommendation = string.Empty;

    [ObservableProperty]
    private bool _hasModelRecommendation;

    /// <summary>Model name passed as override to StreamChatAsync; null = use registry active model.</summary>
    private string? _recommendedModel;

    // --- Computed props ---------------------------------------------------------

    public bool HasPdf => LoadedPdf is not null;
    public bool HasResult => !string.IsNullOrWhiteSpace(ResultPreview);
    public bool HasReferencePdf => ReferencePdfName is not null;
    public bool HasFocusTopics => !string.IsNullOrWhiteSpace(FocusTopics);

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
        IModelRegistry models,
        IScriptAnalysisService analysis,
        ILogger<ScriptExtractorViewModel> logger)
    {
        _ollama   = ollama;
        _pdf      = pdf;
        _models   = models;
        _analysis = analysis;
        _logger   = logger;
    }

    // --- Property change hooks -------------------------------------------------

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

    partial void OnReferencePdfNameChanged(string? value) =>
        OnPropertyChanged(nameof(HasReferencePdf));

    partial void OnFocusTopicsChanged(string value) =>
        OnPropertyChanged(nameof(HasFocusTopics));

    // --- Main PDF loading ------------------------------------------------------

    [RelayCommand]
    private async Task PickPdfAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "PDF-Dateien (*.pdf)|*.pdf",
            Title  = "Skriptum auswählen"
        };
        if (dlg.ShowDialog() == true)
            await LoadPdfAsync(dlg.FileName);
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
            ResultPreview          = string.Empty;
            LastGeneratedPdfPath   = null;
            HasModelRecommendation = false;
            ModelRecommendation    = string.Empty;
            _recommendedModel      = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load script PDF");
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
        LoadedPdf              = null;
        ResultPreview          = string.Empty;
        LastGeneratedPdfPath   = null;
        HasModelRecommendation = false;
        ModelRecommendation    = string.Empty;
        _recommendedModel      = null;
        Status = "Ziehe ein Skriptum hierher oder klicke 'PDF auswählen'.";
    }

    // --- Reference PDF (Style Mimicry) -----------------------------------------

    [RelayCommand]
    private async Task PickReferencePdfAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "PDF-Dateien (*.pdf)|*.pdf",
            Title  = "Referenz-Dokument für Stil-Analyse auswählen"
        };
        if (dlg.ShowDialog() == true)
            await LoadReferencePdfAsync(dlg.FileName);
    }

    public async Task LoadReferencePdfAsync(string path)
    {
        if (IsReferencePdfLoading) return;

        try
        {
            IsReferencePdfLoading = true;
            var extracted   = await _pdf.ExtractTextAsync(path);
            _styleGuideline = _analysis.AnalyzeReferenceStyle(extracted);
            ReferencePdfName = extracted.FileName;
            _logger.LogInformation("Reference style loaded from {File}", extracted.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load reference PDF");
            MessageBox.Show(ex.Message, "Fehler beim Laden des Referenz-Dokuments",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsReferencePdfLoading = false;
        }
    }

    [RelayCommand]
    private void ClearReferencePdf()
    {
        _styleGuideline  = null;
        ReferencePdfName = null;
    }

    // --- Extraction (runs pre-flight first, then streams) ----------------------

    [RelayCommand(CanExecute = nameof(CanExtract))]
    private async Task ExtractAsync()
    {
        if (LoadedPdf is null) return;

        ResultPreview = string.Empty;
        IsBusy        = true;
        _cts          = new CancellationTokenSource();

        try
        {
            // Feature 3: run pre-flight scan synchronously, then yield to let UI
            // render the recommendation banner before the long streaming starts.
            RunPreFlightScan();
            await Task.Yield();

            var messages = new[]
            {
                new ChatMessage { Role = ChatRole.System, Content = BuildSystemPrompt() },
                new ChatMessage { Role = ChatRole.User,   Content = BuildExtractionPrompt() }
            };

            // Pass recommended model as override; null falls back to the registry's active model.
            await foreach (var chunk in _ollama.StreamChatAsync(messages, _recommendedModel, _cts.Token))
                ResultPreview += chunk;
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

    // --- Pre-flight scan (Feature 3) -------------------------------------------

    private void RunPreFlightScan()
    {
        if (LoadedPdf is null) return;

        var modelNames = _models.AvailableModels.Select(m => m.Name).ToList();
        var result     = _analysis.RunPreFlightScan(LoadedPdf.FullText, modelNames, _models.ActiveModel);

        _recommendedModel = result.RecommendedModel;

        var profileIcon = result.Profile switch
        {
            ContentProfile.FormulaHeavy => "∑",
            ContentProfile.ProseHeavy   => "T",
            _                           => "✓"
        };

        var isSwitching = !result.RecommendedModel.Equals(_models.ActiveModel, StringComparison.OrdinalIgnoreCase);
        ModelRecommendation = isSwitching
            ? $"{profileIcon} Modell-Wechsel: {result.RecommendedModel} — {result.Reasoning}"
            : $"{profileIcon} {result.Reasoning}";

        HasModelRecommendation = true;
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();
    private bool CanCancel() => IsBusy;

    // --- PDF Export ------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportPdfAsync()
    {
        if (!HasResult || LoadedPdf is null) return;

        var dlg = new SaveFileDialog
        {
            Filter   = "PDF-Dateien (*.pdf)|*.pdf",
            FileName = Path.GetFileNameWithoutExtension(LoadedPdf.FileName) + "_Zusammenfassung.pdf",
            Title    = "Zusammenfassung speichern"
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
                FileName       = LastGeneratedPdfPath,
                UseShellExecute = true
            });
        }
    }

    private bool CanOpenLastPdf() =>
        !string.IsNullOrEmpty(LastGeneratedPdfPath) && File.Exists(LastGeneratedPdfPath);

    partial void OnLastGeneratedPdfPathChanged(string? value) =>
        OpenLastPdfCommand.NotifyCanExecuteChanged();

    // =========================================================================
    // Prompt engineering
    // =========================================================================

    /// <summary>
    /// System prompt, extended with style guideline when a reference PDF is loaded.
    /// </summary>
    private string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.Append(
            "Du bist ein erfahrener Tutor, der Studierenden hilft, prüfungsrelevanten Stoff aus " +
            "umfangreichen Skripten herauszufiltern. Antworte strukturiert in Markdown: " +
            "Benutze `# Überschrift`, `## Unterüberschrift`, und `- Aufzählungspunkte`. " +
            "Formuliere klar, faktentreu und ohne Füllwörter.");

        // Feature 2: inject reference-style guideline
        if (_styleGuideline is not null)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.Append("STIL-VORGABE (aus Referenz-Dokument abgeleitet — halte dich genau daran): ");
            sb.Append(_styleGuideline.PromptInstruction);
        }

        return sb.ToString();
    }

    /// <summary>
    /// User prompt with detail-level target length, weighted focus topics (Feature 1),
    /// and the full PDF text (hard-truncated for context window safety).
    /// </summary>
    private string BuildExtractionPrompt()
    {
        var hasFocus = !string.IsNullOrWhiteSpace(FocusTopics);
        var (totalStr, focusStr, restStr) = ComputeLengthTargets(hasFocus);

        var sb = new StringBuilder();
        sb.AppendLine("Extrahiere den prüfungsrelevanten Stoff aus dem folgenden Skriptum.");
        sb.AppendLine();

        if (hasFocus)
        {
            // Feature 1: weighted focus-topic distribution
            var topics = FocusTopics
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            sb.AppendLine($"Ziel-Länge GESAMT: {totalStr}.");
            sb.AppendLine(
                $"Davon entfallen ca. {focusStr} auf die FOKUS-THEMEN " +
                $"und nur ca. {restStr} auf sonstigen Inhalt.");
            sb.AppendLine();
            sb.AppendLine("FOKUS-THEMEN (vollständig und prioritär behandeln, " +
                          "Semantic Search über den Text anwenden):");
            foreach (var topic in topics)
                sb.AppendLine($"  - {topic}");
            sb.AppendLine();
            sb.AppendLine("Themen außerhalb der Fokus-Liste: nur kurz erwähnen oder weglassen.");
        }
        else
        {
            sb.AppendLine($"Ziel-Länge: {totalStr}.");
        }

        sb.AppendLine();
        sb.AppendLine("Struktur:");
        sb.AppendLine("1. Eine kurze Einleitung, die das Thema in 2-3 Sätzen umreißt.");
        sb.AppendLine("2. Die wichtigsten Konzepte, jeweils als Unterabschnitt mit `## Überschrift`.");
        sb.AppendLine("3. Kernaussagen als Bullet-Points, Fachbegriffe in **fett**.");
        sb.AppendLine("4. Wenn passend: typische Prüfungsfragen am Ende als Aufzählung.");
        sb.AppendLine();
        sb.AppendLine("Konzentriere dich auf das, was für eine Klausur wirklich relevant ist.");
        sb.AppendLine("Lass historische Ausschweifungen, Marketing-Phrasen und Wiederholungen weg.");
        sb.AppendLine();
        sb.AppendLine("=== SKRIPTUM START ===");
        sb.AppendLine(TruncateForContext(LoadedPdf!.FullText));
        sb.Append("=== SKRIPTUM ENDE ===");

        return sb.ToString();
    }

    /// <summary>
    /// Computes human-readable length targets.
    /// When focus topics are active, focus content gets ~DetailLevel × 1.4× and
    /// non-focus gets ~DetailLevel × 0.35× of the base percentage.
    /// </summary>
    private (string total, string focus, string rest) ComputeLengthTargets(bool hasFocus)
    {
        var (basePct, focusFactor, restFactor) = DetailLevel switch
        {
            1 => (5,  1.5, 0.30),
            2 => (10, 1.4, 0.30),
            3 => (20, 1.3, 0.35),
            4 => (35, 1.2, 0.40),
            5 => (50, 1.1, 0.45),
            _ => (20, 1.3, 0.35)
        };

        var totalStr = DetailLevel switch
        {
            1 => "ca. 5 % der Originallänge — nur die absolute Essenz",
            2 => "ca. 10 % der Originallänge — die wichtigsten Prüfungspunkte",
            3 => "ca. 20 % der Originallänge — eine ausgewogene Zusammenfassung",
            4 => "ca. 35 % der Originallänge — mit Beispielen und wichtigen Details",
            5 => "ca. 50 % der Originallänge — nahezu vollständig, nur redundanzfrei",
            _ => "eine mittellange Zusammenfassung"
        };

        if (!hasFocus) return (totalStr, string.Empty, string.Empty);

        var focusPct = (int)Math.Min(50, basePct * focusFactor);
        var restPct  = (int)Math.Max(1,  basePct * restFactor);
        return (totalStr, $"{focusPct} %", $"{restPct} %");
    }

    private static string TruncateForContext(string text, int maxChars = 60_000)
    {
        if (text.Length <= maxChars) return text;
        return text[..maxChars] + "\n\n[…Inhalt gekürzt — Skriptum zu lang für Kontextfenster…]";
    }
}
