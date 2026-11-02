using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using MICGeoVisionPro.Core.Models;
using MICGeoVisionPro.Core.Services;

namespace MICGeoVisionPro.Desktop.ViewModels
{
    public class ProjectViewModel : INotifyPropertyChanged
    {
        private readonly ProjectRepository _repository;
        private Project _project;
        private string _projectName;
        private Borehole? _selectedBorehole;
        private Log? _selectedLog;

        public ProjectViewModel()
        {
            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MICGeoVisionPro");

            Directory.CreateDirectory(dataDirectory);
            _repository = new ProjectRepository(Path.Combine(dataDirectory, "project.db"));
            _project = _repository.LoadOrCreateDefault();
            _projectName = _project.Name;

            Boreholes = new ObservableCollection<Borehole>(_project.Boreholes);
            SelectedBorehole = Boreholes.FirstOrDefault();
            Logs = new ObservableCollection<Log>();
            RefreshLogs();
        }

        public ObservableCollection<Borehole> Boreholes { get; }
        public ObservableCollection<Log> Logs { get; }

        public Borehole? SelectedBorehole
        {
            get => _selectedBorehole;
            set
            {
                if (_selectedBorehole == value) return;
                _selectedBorehole = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedBoreholeName));
                OnPropertyChanged(nameof(SelectedBoreholeTotalDepth));
                RefreshLogs();
            }
        }

        public Log? SelectedLog
        {
            get => _selectedLog;
            set
            {
                if (_selectedLog == value) return;
                _selectedLog = value;
                OnPropertyChanged();
            }
        }

        public string ProjectName
        {
            get => _projectName;
            set
            {
                if (_projectName == value) return;
                _projectName = value;
                _project.Name = value;
                OnPropertyChanged();
            }
        }

        public string SelectedBoreholeName => SelectedBorehole?.Name ?? "No borehole";
        public string SelectedBoreholeTotalDepth => SelectedBorehole is null ? "0.00 m" : $"{SelectedBorehole.TotalDepth:0.00} m";

        public int BoreholesCount => Boreholes.Count;
        public int LogsCount => Logs.Count;

        public void AddBorehole(string name, double x, double y, double elevation, double totalDepth)
        {
            var borehole = new Borehole
            {
                Name = name,
                X = x,
                Y = y,
                Elevation = elevation,
                TotalDepth = totalDepth
            };

            _project.Boreholes.Add(borehole);
            Boreholes.Add(borehole);
            SelectedBorehole = borehole;
            Save();
        }

        public void AddLithologyLog(string name, List<LithologyInterval> intervals)
        {
            if (SelectedBorehole is null) return;

            var log = new Log
            {
                Name = name,
                Type = LogType.Lithology
            };

            foreach (var interval in intervals)
            {
                log.Points.Add(new LogPoint
                {
                    Depth = interval.StartDepth,
                    Label = interval.Lithology,
                    Value = interval.EndDepth - interval.StartDepth
                });
            }

            SelectedBorehole.Logs.Add(log);
            RefreshLogs();
            Save();
        }

        public void RefreshLogs()
        {
            Logs.Clear();
            if (SelectedBorehole is not null)
            {
                foreach (var log in SelectedBorehole.Logs)
                {
                    Logs.Add(log);
                }
            }

            if (Logs.Count > 0)
            {
                SelectedLog = Logs.First();
            }

            OnPropertyChanged(nameof(LogsCount));
        }

        public void Save()
        {
            _repository.SaveProject(_project);
            OnPropertyChanged(nameof(BoreholesCount));
            OnPropertyChanged(nameof(LogsCount));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
