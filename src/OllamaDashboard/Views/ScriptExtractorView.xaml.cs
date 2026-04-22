using System.IO;
using System.Windows;
using System.Windows.Controls;
using OllamaDashboard.ViewModels;

namespace OllamaDashboard.Views;

public partial class ScriptExtractorView : UserControl
{
    public ScriptExtractorView() => InitializeComponent();

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = IsPdfDrag(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (!IsPdfDrag(e)) return;
        if (DataContext is not ScriptExtractorViewModel vm) return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var pdf = files.FirstOrDefault(f =>
            Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase));
        if (pdf is not null) await vm.LoadPdfAsync(pdf);
    }

    private static bool IsPdfDrag(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        return files.Any(f =>
            Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase));
    }
}
