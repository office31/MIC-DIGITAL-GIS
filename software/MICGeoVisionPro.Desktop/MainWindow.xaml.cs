using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MICGeoVisionPro.Desktop.Services;
using MICGeoVisionPro.Desktop.ViewModels;

namespace MICGeoVisionPro.Desktop
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new ProjectViewModel(new CsvFilePicker());
        }

        private void ExportPng_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG image (*.png)|*.png",
                DefaultExt = ".png",
                FileName = "mic-geovision-view.png"
            };

            if (dialog.ShowDialog(this) != true) return;

            var width = (int)Math.Ceiling(ViewHost.ActualWidth);
            var height = (int)Math.Ceiling(ViewHost.ActualHeight);
            if (width <= 0 || height <= 0)
            {
                MessageBox.Show(this, "The current view is not ready to export.", "MIC GeoVision Pro",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(ViewHost);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);
        }
    }
}
