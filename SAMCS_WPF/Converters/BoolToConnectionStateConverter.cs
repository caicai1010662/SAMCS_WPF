using System;
using System.Globalization;
using System.Windows.Data;

namespace SAMCS_WPF.Converters
{
    /// <summary>
    /// bool → 连接状态文本转换器（true → "连接"，false → "断开"）。
    /// </summary>
    /// <remarks>
    /// TODO: 尚未在 XAML 中引用，待后续界面集成时使用。
    /// </remarks>
    public class BoolToConnectionStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "连接" : "断开";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
