using System.Windows;

namespace GestaoPagamentos.Wpf.Login;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.LoginConcluido += () => DialogResult = true;
        Loaded += (_, _) => EmailBox.Focus();
    }

    private void SenhaBox_PasswordChanged(object sender, RoutedEventArgs e) =>
        _viewModel.Senha = SenhaBox.Password;
}
