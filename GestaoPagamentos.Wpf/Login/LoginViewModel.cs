using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestaoPagamentos.Application.Autenticacao;
using GestaoPagamentos.Wpf.Common;

namespace GestaoPagamentos.Wpf.Login;

public sealed partial class LoginViewModel(IAuthService authService) : ViewModelBase
{
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private bool _manterConectado = true;

    /// <summary>A senha vem do PasswordBox, que não permite vínculo direto por segurança.</summary>
    public string Senha { get; set; } = string.Empty;

    public event Action? LoginConcluido;

    [RelayCommand]
    private Task EntrarAsync() => ExecutarAsync(async () =>
    {
        var r = await authService.EntrarAsync(Email, Senha, ManterConectado);
        if (!r.Sucesso)
        {
            Erro = r.Erro;
            return;
        }

        Senha = string.Empty;
        LoginConcluido?.Invoke();
    });
}
