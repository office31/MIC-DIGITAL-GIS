using System.Windows;
using MICGeoVisionPro.Desktop.ViewModels;

namespace MICGeoVisionPro.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = new MainWindow
        {
            DataContext = new MainViewModel()
        };

        mainWindow.Show();
    }
}
