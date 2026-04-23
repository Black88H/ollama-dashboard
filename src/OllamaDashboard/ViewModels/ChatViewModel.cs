using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using OllamaDashboard.Models;
using OllamaDashboard.Services;

namespace OllamaDashboard.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly IOllamaService _ollama;
    private readonly IPdfService _pdf;
    private readonly ILogger<ChatViewModel> _logger;
    private CancellationTokenSource? _streamCts;

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private PdfExtractionResult? _loadedPdf;

    [ObservableProperty]
    private string _pdfStatus = "Kein PDF geladen. Ziehe ein PDF hierher oder klicke auf 'PDF auswählen'.";

    [ObservableProperty]
    private bool _isPdfLoading;

    public bool HasPdf => LoadedPdf is not null;

    public ChatViewModel(
        IOllamaService ollama,
        IPdfService pdf,
        ILogger<ChatViewModel> logger)
    {
        _ollama = ollama;
        _pdf = pdf;
        _logger = logger;
    }

    partial void OnLoadedPdfChanged(PdfExtractionResult? value) =>
        OnPropertyChanged(nameof(HasPdf));

    [RelayCommand]
    private async Task PickPdfAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "PDF-Dateien (*.pdf)|*.pdf",
            Title = "PDF auswählen"
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
            PdfStatus = $"Lese {Path.GetFileName(path)}…";
            var result = await _pdf.ExtractTextAsync(path);
            LoadedPdf = result;
            PdfStatus = $"{result.FileName} · {result.PageCount} Seiten · ~{result.EstimatedTokens:N0} Tokens";

            Messages.Clear();
            Messages.Add(new ChatMessage
            {
                Role = ChatRole.System,
                Content = $"PDF geladen: {result.FileName}. Du kannst jetzt Fragen zum Inhalt stellen."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load PDF");
            PdfStatus = $"Fehler: {ex.Message}";
            MessageBox.Show(ex.Message, "Fehler beim Laden des PDFs",
                MessageBoxButton.OK, MessageBoxImage.Error);
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
        Messages.Clear();
        PdfStatus = "Kein PDF geladen. Ziehe ein PDF hierher oder klicke auf 'PDF auswählen'.";
    }

    [RelayCommand]
    private void NewChat()
    {
        Messages.Clear();
        if (LoadedPdf is not null)
        {
            Messages.Add(new ChatMessage
            {
                Role = ChatRole.System,
                Content = $"PDF weiterhin geladen: {LoadedPdf.FileName}."
            });
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = InputText.Trim();
        if (string.IsNullOrWhiteSpace(text) || IsBusy) return;

        InputText = string.Empty;

        Messages.Add(new ChatMessage { Role = ChatRole.User, Content = text });
        var assistantMsg = new ChatMessage { Role = ChatRole.Assistant, IsStreaming = true };
        Messages.Add(assistantMsg);

        IsBusy = true;
        _streamCts = new CancellationTokenSource();

        try
        {
            var conversation = BuildConversation();
            var sb = new System.Text.StringBuilder();
            var chunkCount = 0;
            await foreach (var chunk in _ollama.StreamChatAsync(conversation, ct: _streamCts.Token))
            {
                sb.Append(chunk);
                if (++chunkCount % 10 == 0)
                    assistantMsg.Content = sb.ToString();
            }
            assistantMsg.Content = sb.ToString();
        }
        catch (OperationCanceledException)
        {
            assistantMsg.Content += "\n\n*[Abgebrochen]*";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat streaming failed");
            assistantMsg.Content =
                $"⚠ Fehler: {ex.Message}\n\n" +
                "Prüfe, ob Ollama läuft (Standard: http://localhost:11434) und das gewählte Modell installiert ist.";
        }
        finally
        {
            assistantMsg.IsStreaming = false;
            IsBusy = false;
            _streamCts?.Dispose();
            _streamCts = null;
        }
    }

    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

    partial void OnInputTextChanged(string value) => SendCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value)
    {
        SendCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _streamCts?.Cancel();
    private bool CanCancel() => IsBusy;

    private IEnumerable<ChatMessage> BuildConversation()
    {
        var list = new List<ChatMessage>();

        // System prompt that grounds the answer in the uploaded PDF (basic stuffing approach).
        // For very large PDFs this will exceed the context window — in that case upgrade to RAG.
        var systemText = new System.Text.StringBuilder();
        systemText.AppendLine(
            "Du bist ein hilfreicher Assistent, der Fragen präzise und auf Deutsch beantwortet.");
        if (LoadedPdf is not null)
        {
            systemText.AppendLine();
            systemText.AppendLine("KONTEXT — Inhalt des hochgeladenen PDFs:");
            systemText.AppendLine("---");
            systemText.AppendLine(TruncateForContext(LoadedPdf.FullText));
            systemText.AppendLine("---");
            systemText.AppendLine("Beziehe dich bei Antworten auf diesen Inhalt. " +
                                  "Wenn eine Antwort nicht im PDF steht, sag das ehrlich.");
        }
        list.Add(new ChatMessage { Role = ChatRole.System, Content = systemText.ToString() });

        list.AddRange(Messages.Where(m =>
            m.Role != ChatRole.System &&
            !m.IsStreaming &&
            !string.IsNullOrWhiteSpace(m.Content)));

        // Ollama expects the conversation to end with a user message, which it does
        // because we add the streaming assistant placeholder AFTER calling this method.
        return list;
    }

    /// <summary>
    /// Simple safeguard: truncates the PDF text if it would obviously blow the context window.
    /// Replace with a proper chunker + embedding search for production use.
    /// </summary>
    private static string TruncateForContext(string text, int maxChars = 40_000)
    {
        if (text.Length <= maxChars) return text;
        return text[..maxChars] + "\n\n[…Inhalt gekürzt — PDF zu lang für Kontextfenster…]";
    }
}
