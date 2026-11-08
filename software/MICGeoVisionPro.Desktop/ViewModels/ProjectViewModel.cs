using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MICGeoVisionPro.Core.Models;
using MICGeoVisionPro.Core.Services;
using MICGeoVisionPro.Desktop.Services;

namespace MICGeoVisionPro.Desktop.ViewModels
{
    public class ProjectViewModel : INotifyPropertyChanged
    {
        private const double ViewHeight = 440;

        private readonly ProjectRepository _repository;
        private readonly Project _project;
        private readonly CsvImportService _csvImportService = new();
        private readonly ICsvFilePicker _csvFilePicker;
        private string _projectName;
        private string _statusMessage = "Ready | Project loaded | SQLite active";
        private string _viewMessage = string.Empty;
        private Borehole? _selectedBorehole;
        private Log? _selectedLog;
        private LithologyIntervalRow? _selectedInterval;
        private DataTable? _selectedImportedTable;

        public ProjectViewModel(ICsvFilePicker csvFilePicker, string? dataDirectory = null)
        {
            _csvFilePicker = csvFilePicker ?? throw new ArgumentNullException(nameof(csvFilePicker));

            var directory = dataDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MICGeoVisionPro");

            Directory.CreateDirectory(directory);
            _repository = new ProjectRepository(Path.Combine(directory, "project.db"));
            _project = _repository.LoadOrCreateDefault();
            _projectName = _project.Name;

            Boreholes = new ObservableCollection<Borehole>(_project.Boreholes);
            Logs = new ObservableCollection<Log>();
            LithologyIntervals = new ObservableCollection<LithologyIntervalRow>();
            Segments = new ObservableCollection<LithologySegment>();
            DepthTicks = new ObservableCollection<DepthTick>();
            ImportedTables = new ObservableCollection<DataTable>(_project.Tables);
            ImportedRowPreviews = new ObservableCollection<string>();

            AddBoreholeCommand = new RelayCommand(_ => AddBorehole());
            DeleteBoreholeCommand = new RelayCommand(_ => DeleteSelectedBorehole(), _ => SelectedBorehole is not null);
            AddLogCommand = new RelayCommand(_ => AddLog(), _ => SelectedBorehole is not null);
            DeleteLogCommand = new RelayCommand(_ => DeleteSelectedLog(), _ => SelectedLog is not null);
            AddIntervalCommand = new RelayCommand(_ => AddInterval(), _ => SelectedLog is not null);
            RemoveIntervalCommand = new RelayCommand(_ => RemoveSelectedInterval(), _ => SelectedInterval is not null);
            SaveProjectCommand = new RelayCommand(_ => Save());
            ImportCsvCommand = new RelayCommand(_ => ImportCsvFromDialog());

            SelectedBorehole = Boreholes.FirstOrDefault();
            SelectedImportedTable = ImportedTables.FirstOrDefault();
            RefreshLogs();
        }

        public ObservableCollection<Borehole> Boreholes { get; }
        public ObservableCollection<Log> Logs { get; }
        public ObservableCollection<LithologyIntervalRow> LithologyIntervals { get; }
        public ObservableCollection<LithologySegment> Segments { get; }
        public ObservableCollection<DepthTick> DepthTicks { get; }
        public ObservableCollection<DataTable> ImportedTables { get; }
        public ObservableCollection<string> ImportedRowPreviews { get; }

        public ICommand AddBoreholeCommand { get; }
        public ICommand DeleteBoreholeCommand { get; }
        public ICommand AddLogCommand { get; }
        public ICommand DeleteLogCommand { get; }
        public ICommand AddIntervalCommand { get; }
        public ICommand RemoveIntervalCommand { get; }
        public ICommand SaveProjectCommand { get; }
        public ICommand ImportCsvCommand { get; }

        public Borehole? SelectedBorehole
        {
            get => _selectedBorehole;
            set
            {
                if (_selectedBorehole == value) return;

                if (SelectedBorehole is not null && value is not null && !TryCommitPendingIntervals())
                {
                    OnPropertyChanged(nameof(SelectedBorehole));
                    return;
                }

                _selectedBorehole = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BoreholeName));
                OnPropertyChanged(nameof(BoreholeX));
                OnPropertyChanged(nameof(BoreholeY));
                OnPropertyChanged(nameof(BoreholeElevation));
                OnPropertyChanged(nameof(BoreholeTotalDepth));
                OnPropertyChanged(nameof(BoreholeSummary));
                OnPropertyChanged(nameof(HasSelectedBorehole));
                RefreshLogs();
                RefreshBoreholeView();
                RaiseCommandStates();
            }
        }

        public Log? SelectedLog
        {
            get => _selectedLog;
            set
            {
                if (_selectedLog == value) return;

                if (!TryCommitPendingIntervals())
                {
                    OnPropertyChanged(nameof(SelectedLog));
                    return;
                }

                _selectedLog = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedLog));
                LoadIntervals();
                RefreshBoreholeView();
                RaiseCommandStates();
            }
        }

        public LithologyIntervalRow? SelectedInterval
        {
            get => _selectedInterval;
            set
            {
                if (_selectedInterval == value) return;
                _selectedInterval = value;
                OnPropertyChanged();
                (RemoveIntervalCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public DataTable? SelectedImportedTable
        {
            get => _selectedImportedTable;
            set
            {
                if (_selectedImportedTable == value) return;
                _selectedImportedTable = value;
                OnPropertyChanged();
                RefreshImportedRowPreviews();
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
                MarkModified();
            }
        }

        public string BoreholeName
        {
            get => SelectedBorehole?.Name ?? string.Empty;
            set
            {
                if (SelectedBorehole is null) return;
                var name = (value ?? string.Empty).Trim();
                if (SelectedBorehole.Name == name) return;
                SelectedBorehole.Name = name;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BoreholeSummary));
                MarkModified();
            }
        }

        public double BoreholeX
        {
            get => SelectedBorehole?.X ?? 0;
            set => SetCoordinate(value, (borehole, number) => borehole.X = number);
        }

        public double BoreholeY
        {
            get => SelectedBorehole?.Y ?? 0;
            set => SetCoordinate(value, (borehole, number) => borehole.Y = number);
        }

        public double BoreholeElevation
        {
            get => SelectedBorehole?.Elevation ?? 0;
            set => SetCoordinate(value, (borehole, number) => borehole.Elevation = number);
        }

        public double BoreholeTotalDepth
        {
            get => SelectedBorehole?.TotalDepth ?? 0;
            set
            {
                if (SelectedBorehole is null || !double.IsFinite(value)) return;
                if (SelectedBorehole.TotalDepth.Equals(value)) return;
                SelectedBorehole.TotalDepth = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BoreholeSummary));
                MarkModified();
                RefreshBoreholeView();
            }
        }

        public bool HasSelectedBorehole => SelectedBorehole is not null;
        public bool HasSelectedLog => SelectedLog is not null;
        public bool HasScale => SelectedBorehole is { TotalDepth: > 0 };

        public string BoreholeSummary => SelectedBorehole is null
            ? "No borehole selected"
            : string.Format(
                CultureInfo.CurrentCulture,
                "X {0:0.##} · Y {1:0.##} · Elev. {2:0.##} m · Depth {3:0.##} m",
                SelectedBorehole.X,
                SelectedBorehole.Y,
                SelectedBorehole.Elevation,
                SelectedBorehole.TotalDepth);

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (_statusMessage == value) return;
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public string ViewMessage
        {
            get => _viewMessage;
            private set
            {
                if (_viewMessage == value) return;
                _viewMessage = value;
                OnPropertyChanged();
            }
        }

        public int BoreholesCount => Boreholes.Count;
        public int LogsCount => Logs.Count;

        public int ImportCsv(string filePath)
        {
            var table = _csvImportService.ImportTable(filePath);
            _project.Tables.Add(table);
            ImportedTables.Add(table);
            SelectedImportedTable = table;
            Save();
            return table.Rows.Count;
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

            var selection = SelectedLog is not null && Logs.Contains(SelectedLog)
                ? SelectedLog
                : Logs.FirstOrDefault();

            SelectedLog = selection;
            OnPropertyChanged(nameof(LogsCount));
        }

        public void Save()
        {
            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                StatusMessage = "Project name is required.";
                return;
            }

            if (_project.Boreholes.Any(borehole => string.IsNullOrWhiteSpace(borehole.Name)))
            {
                StatusMessage = "Every borehole needs a name.";
                return;
            }

            if (!TryApplyIntervals(out var error))
            {
                StatusMessage = error;
                return;
            }

            _repository.SaveProject(_project);
            StatusMessage = "Project saved locally";
            RefreshBoreholeView();
            OnPropertyChanged(nameof(BoreholesCount));
            OnPropertyChanged(nameof(LogsCount));
        }

        private void SetCoordinate(double value, Action<Borehole, double> assign)
        {
            if (SelectedBorehole is null || !double.IsFinite(value)) return;
            var current = SelectedBorehole;
            assign(current, value);
            OnPropertyChanged(nameof(BoreholeX));
            OnPropertyChanged(nameof(BoreholeY));
            OnPropertyChanged(nameof(BoreholeElevation));
            OnPropertyChanged(nameof(BoreholeSummary));
            MarkModified();
        }

        private void MarkModified()
        {
            StatusMessage = "Modified — press Save to persist";
        }

        private void AddBorehole()
        {
            var index = Boreholes.Count + 1;
            var name = $"B-{index:00}";
            while (Boreholes.Any(borehole => string.Equals(borehole.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                name = $"B-{++index:00}";
            }

            var borehole = new Borehole { Name = name };
            _project.Boreholes.Add(borehole);
            Boreholes.Add(borehole);
            SelectedBorehole = borehole;
            MarkModified();
            OnPropertyChanged(nameof(BoreholesCount));
            RaiseCommandStates();
        }

        private void DeleteSelectedBorehole()
        {
            var borehole = SelectedBorehole;
            if (borehole is null) return;

            var confirmation = MessageBox.Show(
                $"Delete borehole \"{borehole.Name}\" and all its logs?",
                "MIC GeoVision Pro",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.OK) return;

            _project.Boreholes.Remove(borehole);
            Boreholes.Remove(borehole);
            SelectedBorehole = Boreholes.FirstOrDefault();
            MarkModified();
            OnPropertyChanged(nameof(BoreholesCount));
            RaiseCommandStates();
        }

        private void AddLog()
        {
            if (SelectedBorehole is null) return;

            var log = new Log
            {
                Name = $"Lithology {SelectedBorehole.Logs.Count + 1}",
                Type = LogType.Lithology
            };

            SelectedBorehole.Logs.Add(log);
            RefreshLogs();
            SelectedLog = log;
            MarkModified();
        }

        private void DeleteSelectedLog()
        {
            var log = SelectedLog;
            if (log is null || SelectedBorehole is null) return;

            var confirmation = MessageBox.Show(
                $"Delete log \"{log.Name}\" and its layers?",
                "MIC GeoVision Pro",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.OK) return;

            SelectedBorehole.Logs.Remove(log);
            RefreshLogs();
            MarkModified();
        }

        private void AddInterval()
        {
            if (SelectedLog is null) return;

            var totalDepth = SelectedBorehole?.TotalDepth ?? 0;
            var start = LithologyIntervals.Count == 0
                ? 0
                : LithologyIntervals.Max(row => row.EndDepth);

            if (totalDepth > 0 && start >= totalDepth)
            {
                start = Math.Max(0, totalDepth - 5);
            }

            var end = totalDepth > start ? totalDepth : start + 5;

            var row = new LithologyIntervalRow(start, end, string.Empty);
            row.PropertyChanged += OnIntervalRowChanged;
            LithologyIntervals.Add(row);
            SelectedInterval = row;
            MarkModified();
            RefreshBoreholeView();
        }

        private void RemoveSelectedInterval()
        {
            var row = SelectedInterval;
            if (row is null) return;

            row.PropertyChanged -= OnIntervalRowChanged;
            var index = LithologyIntervals.IndexOf(row);
            LithologyIntervals.Remove(row);
            SelectedInterval = LithologyIntervals.Count == 0
                ? null
                : LithologyIntervals[Math.Min(index, LithologyIntervals.Count - 1)];

            MarkModified();
            RefreshBoreholeView();
        }

        private void OnIntervalRowChanged(object? sender, PropertyChangedEventArgs e)
        {
            MarkModified();
            RefreshBoreholeView();
        }

        private void LoadIntervals()
        {
            foreach (var row in LithologyIntervals)
            {
                row.PropertyChanged -= OnIntervalRowChanged;
            }

            LithologyIntervals.Clear();
            SelectedInterval = null;

            if (SelectedLog is not null)
            {
                foreach (var point in SelectedLog.Points.OrderBy(point => point.Depth))
                {
                    var end = point.EndDepth > point.Depth ? point.EndDepth : point.Depth + point.Value;
                    var row = new LithologyIntervalRow(point.Depth, end, point.Label);
                    row.PropertyChanged += OnIntervalRowChanged;
                    LithologyIntervals.Add(row);
                }

                SelectedInterval = LithologyIntervals.FirstOrDefault();
            }

            RefreshBoreholeView();
        }

        private bool TryCommitPendingIntervals()
        {
            if (_selectedLog is null || SelectedBorehole is null) return true;
            if (!_project.Boreholes.Contains(SelectedBorehole)) return true;
            if (!SelectedBorehole.Logs.Contains(_selectedLog)) return true;
            if (TryApplyIntervals(out var error)) return true;

            StatusMessage = error;
            return false;
        }

        private bool TryApplyIntervals(out string error)
        {
            error = string.Empty;
            if (SelectedLog is null) return true;

            var borehole = SelectedBorehole;
            if (borehole is null) return true;

            if (borehole.TotalDepth <= 0)
            {
                error = "Set a total depth before saving lithology layers.";
                return false;
            }

            var rows = LithologyIntervals.OrderBy(row => row.StartDepth).ToList();
            var previousEnd = 0.0;

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var number = i + 1;

                if (row.StartDepth < 0)
                {
                    error = $"Layer {number}: start depth cannot be negative.";
                    return false;
                }

                if (row.EndDepth <= row.StartDepth)
                {
                    error = $"Layer {number}: end depth must be greater than start depth.";
                    return false;
                }

                if (row.EndDepth > borehole.TotalDepth + 1e-6)
                {
                    error = $"Layer {number}: end depth {row.EndDepth:0.##} m exceeds the total depth {borehole.TotalDepth:0.##} m.";
                    return false;
                }

                if (row.StartDepth < previousEnd - 1e-6)
                {
                    error = $"Layer {number} overlaps the previous layer.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(row.Lithology))
                {
                    error = $"Layer {number}: a lithology name is required.";
                    return false;
                }

                previousEnd = row.EndDepth;
            }

            SelectedLog.Points.Clear();
            foreach (var row in rows)
            {
                SelectedLog.Points.Add(new LogPoint
                {
                    Depth = row.StartDepth,
                    EndDepth = row.EndDepth,
                    Label = row.Lithology.Trim(),
                    Value = row.EndDepth - row.StartDepth
                });
            }

            return true;
        }

        private void RefreshBoreholeView()
        {
            Segments.Clear();
            DepthTicks.Clear();
            OnPropertyChanged(nameof(HasScale));

            var borehole = SelectedBorehole;
            if (borehole is null)
            {
                ViewMessage = "Select or create a borehole.";
                return;
            }

            var totalDepth = borehole.TotalDepth;
            if (totalDepth <= 0)
            {
                ViewMessage = "Set a total depth to display the borehole.";
                return;
            }

            var step = NiceStep(totalDepth);
            for (var depth = 0.0; depth <= totalDepth + 1e-9; depth += step)
            {
                AddTick(depth, totalDepth);
            }

            if (totalDepth % step > 1e-9)
            {
                AddTick(totalDepth, totalDepth);
            }

            if (SelectedLog is null)
            {
                ViewMessage = "Add a lithology log to describe the layers.";
                return;
            }

            if (LithologyIntervals.Count == 0)
            {
                ViewMessage = "No layers yet. Use Add layer.";
                return;
            }

            foreach (var row in LithologyIntervals.OrderBy(row => row.StartDepth))
            {
                var start = Math.Clamp(row.StartDepth, 0, totalDepth);
                var end = Math.Clamp(row.EndDepth, 0, totalDepth);
                if (end <= start) continue;

                var label = string.IsNullOrWhiteSpace(row.Lithology) ? "(unnamed)" : row.Lithology.Trim();
                Segments.Add(new LithologySegment(
                    label,
                    ToTop(start, totalDepth),
                    (end - start) / totalDepth * ViewHeight,
                    LithologyPalette.ColorFor(label),
                    string.Format(CultureInfo.CurrentCulture, "{0}: {1:0.##}–{2:0.##} m", label, start, end)));
            }

            ViewMessage = string.Empty;
        }

        private void AddTick(double depth, double totalDepth)
        {
            var top = Math.Max(0, ToTop(depth, totalDepth) - 8);
            DepthTicks.Add(new DepthTick(top, depth.ToString("0.##", CultureInfo.CurrentCulture)));
        }

        private static double ToTop(double depth, double totalDepth) => depth / totalDepth * ViewHeight;

        private static double NiceStep(double totalDepth)
        {
            var target = totalDepth / 8.0;
            double[] steps = [1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000];

            foreach (var step in steps)
            {
                if (step >= target) return step;
            }

            return steps[^1];
        }

        private void ImportCsvFromDialog()
        {
            var filePath = _csvFilePicker.PickCsvFile();
            if (string.IsNullOrWhiteSpace(filePath)) return;

            try
            {
                var rowCount = ImportCsv(filePath);
                StatusMessage = $"Imported and saved {rowCount} rows from {Path.GetFileName(filePath)}";
            }
            catch (Exception exception)
            {
                StatusMessage = $"CSV import failed: {exception.Message}";
            }
        }

        private void RefreshImportedRowPreviews()
        {
            ImportedRowPreviews.Clear();
            if (SelectedImportedTable is null) return;

            foreach (var row in SelectedImportedTable.Rows)
            {
                ImportedRowPreviews.Add(string.Join("  |  ", SelectedImportedTable.Columns.Select(
                    column => $"{column.Name}: {row.GetValueOrDefault(column.Name) ?? string.Empty}")));
            }
        }

        private void RaiseCommandStates()
        {
            (DeleteBoreholeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddLogCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteLogCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddIntervalCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveIntervalCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
