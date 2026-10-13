using GestaoPagamentos.Wpf.Common;
using Microsoft.Extensions.DependencyInjection;

namespace GestaoPagamentos.Wpf.Shell;

public interface INavigator
{
    ViewModelBase? Atual { get; }
    event Action? Navegou;

    Task NavegarAsync(Type viewModelType, Func<ViewModelBase, Task>? inicializar = null);

    Task NavegarAsync<TViewModel>(Func<TViewModel, Task>? inicializar = null) where TViewModel : ViewModelBase;
}

public sealed class Navigator(IServiceProvider services) : INavigator
{
    public ViewModelBase? Atual { get; private set; }
    public event Action? Navegou;

    public async Task NavegarAsync(Type viewModelType, Func<ViewModelBase, Task>? inicializar = null)
    {
        var viewModel = (ViewModelBase)services.GetRequiredService(viewModelType);

        if (Atual is IDisposable descartavel) descartavel.Dispose();

        Atual = viewModel;
        Navegou?.Invoke();

        if (inicializar is not null) await inicializar(viewModel);
        else await viewModel.InicializarAsync();
    }

    public Task NavegarAsync<TViewModel>(Func<TViewModel, Task>? inicializar = null) where TViewModel : ViewModelBase =>
        NavegarAsync(typeof(TViewModel), inicializar is null ? null : vm => inicializar((TViewModel)vm));
}
