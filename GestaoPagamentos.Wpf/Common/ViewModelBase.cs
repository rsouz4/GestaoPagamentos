using CommunityToolkit.Mvvm.ComponentModel;

namespace GestaoPagamentos.Wpf.Common;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _erro;

    public virtual Task InicializarAsync() => Task.CompletedTask;

    protected async Task ExecutarAsync(Func<Task> acao)
    {
        try
        {
            IsBusy = true;
            Erro = null;
            await acao();
        }
        catch (Exception ex)
        {
            Erro = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
