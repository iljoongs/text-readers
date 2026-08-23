using System.Globalization;
using System.Windows.Data;

namespace TextReaders.Views;

public sealed class PauseResumeLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "재개" : "일시정지";

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
