using Microsoft.Win32;

namespace MICGeoVisionPro.Desktop.Services
{
    /// <summary>WPF implementation of the CSV file picker contract.</summary>
    public sealed class CsvFilePicker : ICsvFilePicker
    {
        public string? PickCsvFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Title = "Import CSV Data"
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
