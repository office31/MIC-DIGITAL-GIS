using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace MICGeoVisionPro.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly MICGeoVisionPro.Core.Services.CsvImportService _csvImportService = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ImportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Title = "Import CSV Data"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var rows = _csvImportService.Import(dialog.FileName);
                    var fileContent = string.Join(Environment.NewLine, rows.Count > 0 ? rows.Select(r => string.Join(" | ", r.Values)) : new[] { "No data loaded" });

                    MessageBox.Show($"Imported {rows.Count} rows from {Path.GetFileName(dialog.FileName)}.", "MIC GeoVision Pro");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Import error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
