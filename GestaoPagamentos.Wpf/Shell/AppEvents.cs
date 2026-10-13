namespace GestaoPagamentos.Wpf.Shell;

public sealed class AppEvents
{
    public event Action? NotificacoesAlteradas;
    public event Action<string>? ToastSolicitado;

    public void NotificarNotificacoesAlteradas() => NotificacoesAlteradas?.Invoke();

    public void MostrarToast(string mensagem) => ToastSolicitado?.Invoke(mensagem);
}
