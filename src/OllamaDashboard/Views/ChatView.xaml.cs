using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OllamaDashboard.ViewModels;

namespace OllamaDashboard.Views;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ChatViewModel oldVm)
            oldVm.Messages.CollectionChanged -= Messages_CollectionChanged;
        if (e.NewValue is ChatViewModel newVm)
            newVm.Messages.CollectionChanged += Messages_CollectionChanged;
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(() => MessageScroller.ScrollToEnd());

    // ---------- Drag & Drop ----------

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = IsPdfDrag(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (!IsPdfDrag(e)) return;
        if (DataContext is not ChatViewModel vm) return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var pdf = files.FirstOrDefault(f =>
            Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase));
        if (pdf is not null)
        {
            await vm.LoadPdfAsync(pdf);
        }
    }

    private static bool IsPdfDrag(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        return files.Any(f =>
            Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase));
    }

    // ---------- Enter-to-send ----------

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            if (DataContext is ChatViewModel vm && vm.SendCommand.CanExecute(null))
                vm.SendCommand.Execute(null);
        }
    }
}
