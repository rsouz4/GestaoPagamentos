using CommunityToolkit.Mvvm.ComponentModel;

namespace GestaoPagamentos.Wpf.Shell;

public sealed partial class NavItem(string chave, string titulo, string icone, Type viewModelType) : ObservableObject
{
    public string Chave { get; } = chave;
    public string Titulo { get; } = titulo;
    public string Icone { get; } = icone;
    public Type ViewModelType { get; } = viewModelType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeVisivel))]
    private int _badge;

    public bool BadgeVisivel => Badge > 0;
}
