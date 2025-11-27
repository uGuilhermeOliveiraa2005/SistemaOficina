using System;
using System.Globalization;
using System.Windows.Data;

namespace SistemaOficina
{
    public class RelativeDateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime date)
            {
                DateTime today = DateTime.Today;
                DateTime tomorrow = today.AddDays(1);

                if (date.Date == today)
                {
                    return $"Hoje, às {date:HH:mm}";
                }
                if (date.Date == tomorrow)
                {
                    return $"Amanhã, às {date:HH:mm}";
                }
                return date.ToString("dd/MM/yyyy 'às' HH:mm");
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}