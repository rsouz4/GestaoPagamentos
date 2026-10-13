using System.Windows;

namespace GestaoPagamentos.Wpf.Common;

public interface IDialogService
{
    bool Confirmar(string mensagem, string titulo = "Confirmar");
    void Aviso(string mensagem, string titulo = "Aviso");
}

public sealed class WpfDialogService : IDialogService
{
    public bool Confirmar(string mensagem, string titulo = "Confirmar") =>
        MessageBox.Show(mensagem, titulo, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void Aviso(string mensagem, string titulo = "Aviso") =>
        MessageBox.Show(mensagem, titulo, MessageBoxButton.OK, MessageBoxImage.Information);
}
