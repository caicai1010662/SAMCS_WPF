using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SAMCS_WPF.Converters
{
    /// <summary>
    /// bool → SolidColorBrush 转换器（true → 绿色 #00B894，false → 灰色 #636E72）。
    /// </summary>
    /// <remarks>
    /// TODO: 尚未在 XAML 中引用，待后续限位指示灯集成时使用。
    /// </remarks>
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value
                ? new SolidColorBrush(Color.FromRgb(0x00, 0xB8, 0x94))
                : new SolidColorBrush(Color.FromRgb(0x63, 0x6E, 0x72));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
