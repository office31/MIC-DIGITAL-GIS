using System.Windows;
using MICGeoVisionPro.Desktop.ViewModels;

namespace MICGeoVisionPro.Desktop
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new ProjectViewModel();
        }
    }
}
