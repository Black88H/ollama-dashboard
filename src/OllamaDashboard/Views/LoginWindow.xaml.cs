using System.Windows;
using OllamaDashboard.ViewModels;

namespace OllamaDashboard.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LoginViewModel oldVm)
            oldVm.AuthSucceeded -= OnAuthSucceeded;
        if (e.NewValue is LoginViewModel newVm)
            newVm.AuthSucceeded += OnAuthSucceeded;
    }

    private void OnAuthSucceeded(object? sender, string key)
    {
        // The key is read by the caller via DialogResult + stored in the ViewModel.
        // Signal success so App.OnStartup can proceed.
        DialogResult = true;
        Close();
    }

    // PasswordBox doesn't support data binding — sync manually on each change.
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        PwdBox.PasswordChanged        += (_, _) => SyncPasswords();
        PwdConfirmBox.PasswordChanged += (_, _) => SyncPasswords();
    }

    private void SyncPasswords()
    {
        if (DataContext is not LoginViewModel vm) return;
        vm.Password        = PwdBox.Password;
        vm.PasswordConfirm = PwdConfirmBox.Password;
    }
}
