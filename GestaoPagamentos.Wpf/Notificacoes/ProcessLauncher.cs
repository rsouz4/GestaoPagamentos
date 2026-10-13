using System.Diagnostics;
using GestaoPagamentos.Application.Notificacoes;

namespace GestaoPagamentos.Wpf.Notificacoes;

public sealed class ProcessLauncher : ILauncher
{
    public void Abrir(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
