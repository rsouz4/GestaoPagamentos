using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Wpf.Common;

public sealed class StatusTextoConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        StatusParcela.Paga => "Paga",
        StatusParcela.PagaParcial => "Paga parcial",
        StatusParcela.Atrasada => "Atrasada",
        StatusParcela.Pendente => "Pendente",
        StatusDivida.Ativa => "Ativa",
        StatusDivida.Quitada => "Quitada",
        StatusDivida.Cancelada => "Cancelada",
        TipoDivida.Avulsa => "Avulsa",
        TipoDivida.Parcelada => "Parcelada",
        TipoNotificacao.Lembrete => "Lembrete",
        TipoNotificacao.Atraso => "Atraso",
        TipoNotificacao.PagamentoRecebido => "Pagamento recebido",
        StatusNotificacao.Pendente => "Pendente",
        StatusNotificacao.Enviada => "Enviada",
        StatusNotificacao.Ignorada => "Ignorada",
        StatusNotificacao.Descartada => "Descartada",
        _ => value?.ToString() ?? string.Empty
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StatusBrushConverter : IValueConverter
{
    private static readonly (Brush Fg, Brush Bg) Verde = (Criar("#166534"), Criar("#DCFCE7"));
    private static readonly (Brush Fg, Brush Bg) Vermelho = (Criar("#991B1B"), Criar("#FEE2E2"));
    private static readonly (Brush Fg, Brush Bg) Laranja = (Criar("#92400E"), Criar("#FEF3C7"));
    private static readonly (Brush Fg, Brush Bg) Azul = (Criar("#1E40AF"), Criar("#DBEAFE"));
    private static readonly (Brush Fg, Brush Bg) Cinza = (Criar("#374151"), Criar("#E5E7EB"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var (fg, bg) = value switch
        {
            StatusParcela.Paga or StatusDivida.Quitada or StatusNotificacao.Enviada => Verde,
            StatusParcela.Atrasada or TipoNotificacao.Atraso => Vermelho,
            StatusParcela.PagaParcial or TipoNotificacao.Lembrete => Laranja,
            StatusParcela.Pendente or StatusDivida.Ativa or StatusNotificacao.Pendente => Azul,
            _ => Cinza
        };

        return string.Equals(parameter as string, "bg", StringComparison.OrdinalIgnoreCase) ? bg : fg;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Criar(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
