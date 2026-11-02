using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MICGeoVisionPro.Core.Models;
using MICGeoVisionPro.Desktop.ViewModels;

namespace MICGeoVisionPro.Desktop
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var vm = new ProjectViewModel();
            DataContext = vm;

            Loaded += (_, _) =>
            {
                var projectNameBox = FindName("ProjectNameBox") as TextBox;
                if (projectNameBox != null)
                {
                    projectNameBox.Text = vm.ProjectName;
                }

                var boreholeList = FindName("BoreholeList") as ListBox;
                if (boreholeList != null)
                {
                    boreholeList.ItemsSource = vm.Boreholes;
                    boreholeList.DisplayMemberPath = "Name";
                }
            };

            var addButton = FindName("AddBoreholeButton") as Button;
            if (addButton != null)
            {
                addButton.Click += (_, _) =>
                {
                    vm.AddBorehole("B-0" + (vm.Boreholes.Count + 1), 500 + vm.Boreholes.Count, 600 + vm.Boreholes.Count, 120, 40 + vm.Boreholes.Count * 10);

                    var boreholeList = FindName("BoreholeList") as ListBox;
                    if (boreholeList != null)
                    {
                        boreholeList.ItemsSource = vm.Boreholes;
                    }
                };
            }

            var saveButton = FindName("SaveProjectButton") as Button;
            if (saveButton != null)
            {
                saveButton.Click += (_, _) =>
                {
                    vm.Save();
                    MessageBox.Show("Project saved locally.", "MIC GeoVision Pro");
                };
            }
        }
    }
}
