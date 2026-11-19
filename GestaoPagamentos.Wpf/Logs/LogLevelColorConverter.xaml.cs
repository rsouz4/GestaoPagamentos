using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace GestaoPagamentos.Wpf.Logs;

public sealed class LogLevelColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not LogLevel level)
            return Brushes.Black;

        return level switch
        {
            LogLevel.Trace => new SolidColorBrush(Color.FromRgb(150, 150, 150)),      // Cinza
            LogLevel.Debug => new SolidColorBrush(Color.FromRgb(0, 150, 255)),        // Azul
            LogLevel.Information => new SolidColorBrush(Color.FromRgb(39, 174, 96)),  // Verde
            LogLevel.Warning => new SolidColorBrush(Color.FromRgb(241, 196, 15)),     // Amarelo/Ouro
            LogLevel.Error => new SolidColorBrush(Color.FromRgb(231, 76, 60)),        // Vermelho
            LogLevel.Critical => new SolidColorBrush(Color.FromRgb(192, 0, 0)),       // Vermelho escuro
            _ => Brushes.Black
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
