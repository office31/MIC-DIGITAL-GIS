using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MICGeoVisionPro.Desktop.ViewModels;

namespace MICGeoVisionPro.Desktop
{
    public partial class MainWindow : Window
    {
        private ProjectViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new ProjectViewModel();
            DataContext = _viewModel;

            Loaded += (_, _) =>
            {
                var projectNameBox = FindName("ProjectNameBox") as TextBox;
                if (projectNameBox != null)
                {
                    projectNameBox.Text = _viewModel.ProjectName;
                }

                var boreholeList = FindName("BoreholeList") as ListBox;
                if (boreholeList != null)
                {
                    boreholeList.ItemsSource = _viewModel.Boreholes;
                    boreholeList.DisplayMemberPath = "Name";
                    boreholeList.SelectionChanged += (_, __) =>
                    {
                        var selected = boreholeList.SelectedItem as MICGeoVisionPro.Core.Models.Borehole;
                        _viewModel.SelectedBorehole = selected;
                        var logList = FindName("LogList") as ListBox;
                        if (logList != null)
                        {
                            logList.ItemsSource = _viewModel.Logs;
                            logList.DisplayMemberPath = "Name";
                        }
                    };
                }

                var logListView = FindName("LogList") as ListBox;
                if (logListView != null)
                {
                    logListView.ItemsSource = _viewModel.Logs;
                    logListView.DisplayMemberPath = "Name";
                }
            };

            var addBoreholeButton = FindName("AddBoreholeButton") as Button;
            if (addBoreholeButton != null)
            {
                addBoreholeButton.Click += (_, _) =>
                {
                    _viewModel.AddBorehole($"B-{_viewModel.BoreholesCount + 1:00}", 500 + _viewModel.BoreholesCount * 10, 600 + _viewModel.BoreholesCount * 10, 120, 40 + _viewModel.BoreholesCount * 10);

                    var boreholeList = FindName("BoreholeList") as ListBox;
                    if (boreholeList != null)
                    {
                        boreholeList.ItemsSource = _viewModel.Boreholes;
                        boreholeList.SelectedItem = _viewModel.SelectedBorehole;
                    }
                };
            }

            var addLogButton = FindName("AddLogButton") as Button;
            if (addLogButton != null)
            {
                addLogButton.Click += (_, _) =>
                {
                    var intervals = new List<MICGeoVisionPro.Core.Models.LithologyInterval>
                    {
                        new() { StartDepth = 0, EndDepth = 10, Lithology = "Topsoil" },
                        new() { StartDepth = 10, EndDepth = 25, Lithology = "Clay" },
                        new() { StartDepth = 25, EndDepth = 35, Lithology = "Sand" }
                    };

                    _viewModel.AddLithologyLog($"Lithology {_viewModel.LogsCount + 1}", intervals);

                    var logList = FindName("LogList") as ListBox;
                    if (logList != null)
                    {
                        logList.ItemsSource = _viewModel.Logs;
                        logList.SelectedItem = _viewModel.SelectedLog;
                    }
                };
            }

            var saveButton = FindName("SaveProjectButton") as Button;
            if (saveButton != null)
            {
                saveButton.Click += (_, _) =>
                {
                    _viewModel.Save();
                    MessageBox.Show("Project saved locally.", "MIC GeoVision Pro");
                };
            }
        }
    }
}
