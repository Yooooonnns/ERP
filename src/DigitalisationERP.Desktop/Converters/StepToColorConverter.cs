using System.Globalization;
using System.Windows.Media;
using System.Windows.Data;

namespace DigitalisationERP.Desktop.Converters;

public class StepToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int currentStep && parameter is string stepStr && int.TryParse(stepStr, out int stepNum))
        {
            // Si l'étape actuelle est >= au numéro de barre, colorier en bleu
            return currentStep >= stepNum ? new SolidColorBrush(Color.FromArgb(255, 0, 102, 204)) : new SolidColorBrush(Color.FromArgb(255, 221, 221, 221));
        }
        return new SolidColorBrush(Color.FromArgb(255, 221, 221, 221));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
