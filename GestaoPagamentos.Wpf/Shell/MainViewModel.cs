using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using GestaoPagamentos.Application.Notificacoes;
using GestaoPagamentos.Wpf.Common;

namespace GestaoPagamentos.Wpf.Shell;

public sealed partial class MainViewModel : ViewModelBase
{
    public const string ChaveNotificacoes = "notificacoes";

    private readonly INavigator _navigator;
    private readonly UseCaseRunner _useCases;
    private CancellationTokenSource? _toastCts;

    public ObservableCollection<NavItem> Itens { get; }

    [ObservableProperty]
    private NavItem? _itemSelecionado;

    [ObservableProperty]
    private ViewModelBase? _conteudo;

    [ObservableProperty]
    private string? _toastMensagem;

    [ObservableProperty]
    private bool _toastVisivel;

    public MainViewModel(
        IEnumerable<NavItem> itens,
        INavigator navigator,
        AppEvents eventos,
        UseCaseRunner useCases)
    {
        _navigator = navigator;
        _useCases = useCases;
        Itens = new ObservableCollection<NavItem>(itens);

        _navigator.Navegou += () => Conteudo = _navigator.Atual;
        eventos.NotificacoesAlteradas += () => _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(AtualizarBadgeAsync);
        eventos.ToastSolicitado += mensagem => _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => MostrarToastAsync(mensagem));
    }

    public override async Task InicializarAsync()
    {
        await AtualizarBadgeAsync();
        ItemSelecionado = Itens.FirstOrDefault();
    }

    partial void OnItemSelecionadoChanged(NavItem? value)
    {
        if (value is not null) _ = _navigator.NavegarAsync(value.ViewModelType);
    }

    private async Task AtualizarBadgeAsync()
    {
        try
        {
            var total = await _useCases.ExecutarAsync((ContarNotificacoesPendentesUseCase u) => u.ExecuteAsync());
            var item = Itens.FirstOrDefault(i => i.Chave == ChaveNotificacoes);
            if (item is not null) item.Badge = total;
        }
        catch
        {
            // O selo é apenas informativo; falhas de leitura não devem interromper o app.
        }
    }

    private async Task MostrarToastAsync(string mensagem)
    {
        _toastCts?.Cancel();
        _toastCts = new CancellationTokenSource();
        var token = _toastCts.Token;

        ToastMensagem = mensagem;
        ToastVisivel = true;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            ToastVisivel = false;
        }
        catch (OperationCanceledException)
        {
        }
    }
}
