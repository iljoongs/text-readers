using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TextReaders.Views;

// 책 제목 문자열을 해시해 일정한(항상 같은 제목 -> 같은 색) 파스텔 계열 색으로 바꾼다.
// 실제 표지 이미지가 없는 .txt/.md 책의 "가짜 표지" 배경으로 쓴다.
public sealed class BookCardColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var title = value as string ?? string.Empty;
        var hash = 0;
        foreach (var ch in title)
        {
            hash = (hash * 31 + ch) & 0x7FFFFFFF;
        }

        var hue = hash % 360;
        var color = HslToRgb(hue, 0.45, 0.72);
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Color HslToRgb(double hue, double saturation, double lightness)
    {
        var c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = lightness - c / 2;

        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return Color.FromRgb(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255));
    }
}
