using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace MICGeoVisionPro.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly MICGeoVisionPro.Core.Services.CsvImportService _csvImportService = new();

        private void ImportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Title = "Import CSV Data"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var rows = _csvImportService.Import(dialog.FileName);
                MessageBox.Show(
                    $"Imported {rows.Count} rows from {Path.GetFileName(dialog.FileName)}.",
                    "MIC GeoVision Pro");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Import error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
