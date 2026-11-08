using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MICGeoVisionPro.Core.Services;
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

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ProjectViewModel viewModel) return;
            OpenExternalTarget(viewModel.DataDirectory);
        }

        private void OpenStorageSite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink link && link.Tag is string target)
            {
                OpenExternalTarget(target);
            }
        }

        private void OpenExternalTarget(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Unable to open \"{target}\": {ex.Message}", "MIC GeoVision Pro",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackupProject_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ProjectViewModel viewModel) return;

            var dialog = new SaveFileDialog
            {
                Title = "Back up project",
                Filter = "Project backup (*.db)|*.db|All files (*.*)|*.*",
                DefaultExt = ".db",
                AddExtension = true,
                FileName = $"MICGeoVisionPro-backup-{DateTime.Now:yyyyMMdd-HHmm}.db"
            };

            if (dialog.ShowDialog(this) != true) return;

            try
            {
                if (!viewModel.BackupTo(dialog.FileName)) return;

                MessageBox.Show(this,
                    $"Backup written to:\n{dialog.FileName}\n\nUpload it to Google Drive, MediaFire, OneDrive or Dropbox to keep an off-site copy.",
                    "MIC GeoVision Pro", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"The backup could not be written: {ex.Message}", "MIC GeoVision Pro",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestoreProject_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ProjectViewModel viewModel) return;

            var dialog = new OpenFileDialog
            {
                Title = "Restore project",
                Filter = "Project backup (*.db)|*.db|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != true) return;

            if (!ProjectRepository.IsProjectDatabase(dialog.FileName))
            {
                MessageBox.Show(this, "This file is not a MIC GeoVision Pro project database.", "MIC GeoVision Pro",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(viewModel.DatabasePath),
                    StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "This is already the current project database.", "MIC GeoVision Pro",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirmation = MessageBox.Show(this,
                "Replace the current project with this backup? Unsaved changes will be lost.",
                "MIC GeoVision Pro", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (confirmation != MessageBoxResult.OK) return;

            try
            {
                viewModel.RestoreFrom(dialog.FileName);
                DataContext = new ProjectViewModel(new CsvFilePicker());
                MessageBox.Show(this, "Project restored from the backup.", "MIC GeoVision Pro",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"The project could not be restored: {ex.Message}", "MIC GeoVision Pro",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CloudSignIn_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ProjectViewModel viewModel) return;

            if (string.IsNullOrEmpty(CloudPasswordBox.Password))
            {
                MessageBox.Show(this, "Enter the password of your account.", "MIC GeoVision Pro",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var signedIn = await viewModel.SignInAsync(CloudPasswordBox.Password);
            CloudPasswordBox.Clear();

            if (!signedIn)
            {
                MessageBox.Show(this, viewModel.CloudStatus, "MIC GeoVision Pro",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void CloudSignOut_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ProjectViewModel viewModel) return;
            await viewModel.SignOutAsync();
        }

        private async void CloudRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ProjectViewModel viewModel) return;
            await viewModel.RefreshCloudProjectsAsync();
        }

        private async void CloudUpload_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ProjectViewModel viewModel) return;

            if (await viewModel.UploadCurrentProjectAsync() == CloudActionResult.Failed)
            {
                MessageBox.Show(this, viewModel.CloudStatus, "MIC GeoVision Pro",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void CloudDownload_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ProjectViewModel viewModel) return;

            if (await viewModel.DownloadSelectedProjectAsync() == CloudActionResult.Failed)
            {
                MessageBox.Show(this, viewModel.CloudStatus, "MIC GeoVision Pro",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
