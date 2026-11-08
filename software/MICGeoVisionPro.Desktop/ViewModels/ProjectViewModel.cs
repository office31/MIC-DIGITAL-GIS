using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MICGeoVisionPro.Core.Models;
using MICGeoVisionPro.Core.Services;
using MICGeoVisionPro.Desktop.Services;

namespace MICGeoVisionPro.Desktop.ViewModels
{
    public enum CloudActionResult
    {
        Succeeded,
        Cancelled,
        Failed
    }

    public class ProjectViewModel : INotifyPropertyChanged
    {
        private const double ViewHeight = 440;

        private const double MapCanvasWidth = 620;
        private const double MapCanvasHeight = 480;
        private const double MapPlotLeft = 56;
        private const double MapPlotTop = 28;
        private const double MapPlotRight = 24;
        private const double MapPlotBottom = 48;

        private const double SectionCanvasWidth = 620;
        private const double SectionCanvasHeight = 440;
        private const double SectionPlotLeft = 78;
        private const double SectionPlotTop = 54;
        private const double SectionPlotRight = 28;
        private const double SectionPlotBottom = 58;
        private const double SectionColumnWidth = 34;

        private readonly ProjectRepository _repository;
        private readonly string _dataDirectory;
        private readonly string _databasePath;
        private readonly CsvImportService _csvImportService = new();
        private readonly ICsvFilePicker _csvFilePicker;
        private readonly CloudSyncService _cloud = new();
        private Project _project;
        private CloudProject? _selectedCloudProject;
        private string _cloudEmail = string.Empty;
        private string _cloudStatus = "Sign in to link this app to your account.";
        private bool _cloudBusy;
        private string _projectName;
        private string _statusMessage = "Ready | Project loaded | SQLite active";
        private string _viewMessage = string.Empty;
        private string _mapMessage = string.Empty;
        private string _mapRangeLabel = string.Empty;
        private string _sectionMessage = string.Empty;
        private PointCollection _sectionSurfacePoints = new();
        private WorkspaceView _activeView = WorkspaceView.Borehole;
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
            _dataDirectory = directory;
            _databasePath = Path.Combine(directory, "project.db");
            _repository = new ProjectRepository(_databasePath);
            _project = _repository.LoadOrCreateDefault();
            _projectName = _project.Name;

            Boreholes = new ObservableCollection<Borehole>(_project.Boreholes);
            Logs = new ObservableCollection<Log>();
            LithologyIntervals = new ObservableCollection<LithologyIntervalRow>();
            Segments = new ObservableCollection<LithologySegment>();
            DepthTicks = new ObservableCollection<DepthTick>();
            MapMarkers = new ObservableCollection<MapMarker>();
            SectionColumns = new ObservableCollection<SectionColumn>();
            SectionLabels = new ObservableCollection<SectionLabel>();
            SectionDistanceTicks = new ObservableCollection<SectionTick>();
            SectionElevationTicks = new ObservableCollection<SectionTick>();
            ImportedTables = new ObservableCollection<DataTable>(_project.Tables);
            ImportedRowPreviews = new ObservableCollection<string>();
            CloudProjects = new ObservableCollection<CloudProject>();

            AddBoreholeCommand = new RelayCommand(_ => AddBorehole());
            DeleteBoreholeCommand = new RelayCommand(_ => DeleteSelectedBorehole(), _ => SelectedBorehole is not null);
            AddLogCommand = new RelayCommand(_ => AddLog(), _ => SelectedBorehole is not null);
            DeleteLogCommand = new RelayCommand(_ => DeleteSelectedLog(), _ => SelectedLog is not null);
            AddIntervalCommand = new RelayCommand(_ => AddInterval(), _ => SelectedLog is not null);
            RemoveIntervalCommand = new RelayCommand(_ => RemoveSelectedInterval(), _ => SelectedInterval is not null);
            SaveProjectCommand = new RelayCommand(_ => Save());
            ImportCsvCommand = new RelayCommand(_ => ImportCsvFromDialog());
            ExportCsvCommand = new RelayCommand(_ => ExportCsvFromDialog());
            ShowBoreholeViewCommand = new RelayCommand(_ => SetActiveView(WorkspaceView.Borehole));
            ShowMapViewCommand = new RelayCommand(_ => SetActiveView(WorkspaceView.Map));
            ShowCrossSectionViewCommand = new RelayCommand(_ => SetActiveView(WorkspaceView.CrossSection));

            SelectedBorehole = Boreholes.FirstOrDefault();
            SelectedImportedTable = ImportedTables.FirstOrDefault();
            RefreshLogs();
        }

        public ObservableCollection<Borehole> Boreholes { get; }
        public ObservableCollection<Log> Logs { get; }
        public ObservableCollection<LithologyIntervalRow> LithologyIntervals { get; }
        public ObservableCollection<LithologySegment> Segments { get; }
        public ObservableCollection<DepthTick> DepthTicks { get; }
        public ObservableCollection<MapMarker> MapMarkers { get; }
        public ObservableCollection<SectionColumn> SectionColumns { get; }
        public ObservableCollection<SectionLabel> SectionLabels { get; }
        public ObservableCollection<SectionTick> SectionDistanceTicks { get; }
        public ObservableCollection<SectionTick> SectionElevationTicks { get; }
        public ObservableCollection<DataTable> ImportedTables { get; }
        public ObservableCollection<string> ImportedRowPreviews { get; }
        public ObservableCollection<CloudProject> CloudProjects { get; }

        public ICommand AddBoreholeCommand { get; }
        public ICommand DeleteBoreholeCommand { get; }
        public ICommand AddLogCommand { get; }
        public ICommand DeleteLogCommand { get; }
        public ICommand AddIntervalCommand { get; }
        public ICommand RemoveIntervalCommand { get; }
        public ICommand SaveProjectCommand { get; }
        public ICommand ImportCsvCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ShowBoreholeViewCommand { get; }
        public ICommand ShowMapViewCommand { get; }
        public ICommand ShowCrossSectionViewCommand { get; }

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

        public string DataDirectory => _dataDirectory;

        public string DatabasePath => _databasePath;

        public string CloudEmail
        {
            get => _cloudEmail;
            set
            {
                if (_cloudEmail == value) return;
                _cloudEmail = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public CloudProject? SelectedCloudProject
        {
            get => _selectedCloudProject;
            set
            {
                if (_selectedCloudProject == value) return;
                _selectedCloudProject = value;
                OnPropertyChanged();
            }
        }

        public bool IsCloudSignedIn => _cloud.IsSignedIn;
        public bool IsCloudSignedOut => !IsCloudSignedIn;
        public bool IsCloudBusy => _cloudBusy;
        public bool CanSignIn => IsCloudSignedOut && !IsCloudBusy;
        public bool CanUseCloud => IsCloudSignedIn && !IsCloudBusy;

        public string CloudAccountLabel => IsCloudSignedIn
            ? $"Signed in as {_cloud.SignedInEmail ?? "account"}"
            : "Not signed in. Credentials are never stored on this PC.";

        public string CloudStatus
        {
            get => _cloudStatus;
            private set
            {
                if (_cloudStatus == value) return;
                _cloudStatus = value;
                OnPropertyChanged();
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
                RefreshBoreholeView();
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

        public WorkspaceView ActiveView => _activeView;

        public bool IsBoreholeViewActive => _activeView == WorkspaceView.Borehole;
        public bool IsMapViewActive => _activeView == WorkspaceView.Map;
        public bool IsSectionViewActive => _activeView == WorkspaceView.CrossSection;

        public string ActiveViewTitle => _activeView switch
        {
            WorkspaceView.Map => "Map View",
            WorkspaceView.CrossSection => "Cross Section",
            _ => "Borehole View"
        };

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

        public string MapMessage
        {
            get => _mapMessage;
            private set
            {
                if (_mapMessage == value) return;
                _mapMessage = value;
                OnPropertyChanged();
            }
        }

        public string MapRangeLabel
        {
            get => _mapRangeLabel;
            private set
            {
                if (_mapRangeLabel == value) return;
                _mapRangeLabel = value;
                OnPropertyChanged();
            }
        }

        public string SectionMessage
        {
            get => _sectionMessage;
            private set
            {
                if (_sectionMessage == value) return;
                _sectionMessage = value;
                OnPropertyChanged();
            }
        }

        public PointCollection SectionSurfacePoints
        {
            get => _sectionSurfacePoints;
            private set
            {
                _sectionSurfacePoints = value;
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

        public bool Save()
        {
            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                StatusMessage = "Project name is required.";
                return false;
            }

            if (_project.Boreholes.Any(borehole => string.IsNullOrWhiteSpace(borehole.Name)))
            {
                StatusMessage = "Every borehole needs a name.";
                return false;
            }

            if (!TryApplyIntervals(out var error))
            {
                StatusMessage = error;
                return false;
            }

            _repository.SaveProject(_project);
            StatusMessage = "Project saved locally";
            RefreshBoreholeView();
            OnPropertyChanged(nameof(BoreholesCount));
            OnPropertyChanged(nameof(LogsCount));
            return true;
        }

        public bool BackupTo(string destinationPath)
        {
            if (!Save())
            {
                return false;
            }

            var target = Path.GetFullPath(destinationPath);
            if (string.Equals(target, Path.GetFullPath(_databasePath), StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "Choose a backup file other than the live database.";
                return false;
            }

            File.Copy(_databasePath, target, overwrite: true);
            StatusMessage = $"Backup saved to {Path.GetFileName(target)}";
            return true;
        }

        public void RestoreFrom(string backupPath)
        {
            var tempPath = _databasePath + ".restoring";
            try
            {
                File.Copy(backupPath, tempPath, overwrite: true);
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                File.Move(tempPath, _databasePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        public async Task<bool> SignInAsync(string password)
        {
            if (_cloudBusy) return false;

            SetCloudBusy(true);
            CloudStatus = "Signing in…";
            try
            {
                await _cloud.SignInAsync(CloudEmail, password);
                NotifyCloudState();
                StatusMessage = "Signed in to the cloud account";
                CloudStatus = $"Signed in as {CloudEmail.Trim()}.";

                try
                {
                    await LoadCloudProjectsAsync();
                }
                catch (Exception exception)
                {
                    CloudStatus = $"Signed in, but the project list could not be loaded: {exception.Message}";
                }

                return true;
            }
            catch (Exception exception)
            {
                CloudStatus = exception.Message;
                return false;
            }
            finally
            {
                SetCloudBusy(false);
            }
        }

        public async Task SignOutAsync()
        {
            if (_cloudBusy) return;

            SetCloudBusy(true);
            try
            {
                await _cloud.SignOutAsync();
                CloudProjects.Clear();
                SelectedCloudProject = null;
                NotifyCloudState();
                CloudStatus = "Signed out. Credentials are never stored on this PC.";
                StatusMessage = "Signed out of the cloud account";
            }
            finally
            {
                SetCloudBusy(false);
            }
        }

        public async Task RefreshCloudProjectsAsync()
        {
            if (_cloudBusy) return;
            if (!IsCloudSignedIn)
            {
                CloudStatus = "Sign in before loading your cloud projects.";
                return;
            }

            SetCloudBusy(true);
            try
            {
                await LoadCloudProjectsAsync();
            }
            catch (Exception exception)
            {
                CloudStatus = exception.Message;
            }
            finally
            {
                SetCloudBusy(false);
            }
        }

        public async Task<CloudActionResult> UploadCurrentProjectAsync()
        {
            if (_cloudBusy) return CloudActionResult.Failed;
            if (!IsCloudSignedIn)
            {
                CloudStatus = "Sign in before uploading a project.";
                return CloudActionResult.Failed;
            }

            if (!Save())
            {
                CloudStatus = StatusMessage;
                return CloudActionResult.Failed;
            }

            var documentJson = ProjectDocument.Serialize(_project);
            var byteCount = Encoding.UTF8.GetByteCount(documentJson);
            if (byteCount > CloudSyncService.MaxDocumentBytes)
            {
                CloudStatus = $"This project is {byteCount / 1024} KB and the account limit is 512 KB. " +
                              "Remove large imported CSV tables before uploading.";
                return CloudActionResult.Failed;
            }

            var cloudId = _project.Id.ToString("D");
            var existing = CloudProjects.FirstOrDefault(project =>
                string.Equals(project.Id, cloudId, StringComparison.OrdinalIgnoreCase));

            if (existing is { HasDocument: true })
            {
                var answer = MessageBox.Show(
                    $"Project \"{existing.Name}\" already exists on the account. Replace the stored version?",
                    "MIC GeoVision Pro",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);

                if (answer != MessageBoxResult.OK) return CloudActionResult.Cancelled;
            }

            SetCloudBusy(true);
            try
            {
                await _cloud.UploadProjectDocumentAsync(_project.Id, _project.Name, _project.Description, documentJson);
                await LoadCloudProjectsAsync();
                CloudStatus = $"Uploaded \"{_project.Name}\" ({byteCount / 1024} KB).";
                StatusMessage = "Project uploaded to the cloud account";
                return CloudActionResult.Succeeded;
            }
            catch (Exception exception)
            {
                CloudStatus = exception.Message;
                return CloudActionResult.Failed;
            }
            finally
            {
                SetCloudBusy(false);
            }
        }

        public async Task<CloudActionResult> DownloadSelectedProjectAsync()
        {
            if (_cloudBusy) return CloudActionResult.Failed;
            if (!IsCloudSignedIn)
            {
                CloudStatus = "Sign in before downloading a project.";
                return CloudActionResult.Failed;
            }

            var selection = SelectedCloudProject;
            if (selection is null)
            {
                CloudStatus = "Select a cloud project first.";
                return CloudActionResult.Failed;
            }

            if (!selection.HasDocument)
            {
                CloudStatus = "This cloud project has no saved document yet.";
                return CloudActionResult.Failed;
            }

            var answer = MessageBox.Show(
                $"Download \"{selection.Name}\" and replace the local project? Unsaved changes will be lost.",
                "MIC GeoVision Pro",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.OK) return CloudActionResult.Cancelled;

            SetCloudBusy(true);
            try
            {
                var documentJson = await _cloud.DownloadProjectDocumentAsync(selection.Id);
                var project = ProjectDocument.Deserialize(documentJson)
                    ?? throw new CloudSyncException("The cloud document could not be read.");

                if (!Guid.TryParse(selection.Id, out var cloudId))
                {
                    throw new CloudSyncException("The cloud project id is not valid.");
                }

                project.Id = cloudId;
                _repository.ReplaceAll(project);
                ApplyProject(project);
                StatusMessage = "Project downloaded from the cloud account";
                CloudStatus = $"Downloaded \"{project.Name}\".";
                return CloudActionResult.Succeeded;
            }
            catch (Exception exception)
            {
                CloudStatus = exception.Message;
                return CloudActionResult.Failed;
            }
            finally
            {
                SetCloudBusy(false);
            }
        }

        public void ApplyProject(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);

            foreach (var row in LithologyIntervals)
            {
                row.PropertyChanged -= OnIntervalRowChanged;
            }

            _project = project;
            _projectName = project.Name;
            OnPropertyChanged(nameof(ProjectName));

            _selectedBorehole = null;
            _selectedLog = null;
            _selectedInterval = null;
            _selectedImportedTable = null;

            Boreholes.Clear();
            foreach (var borehole in project.Boreholes)
            {
                Boreholes.Add(borehole);
            }

            ImportedTables.Clear();
            foreach (var table in project.Tables)
            {
                ImportedTables.Add(table);
            }

            LithologyIntervals.Clear();
            OnPropertyChanged(nameof(SelectedInterval));
            SelectedImportedTable = ImportedTables.FirstOrDefault();

            RefreshLogs();
            SelectedBorehole = Boreholes.FirstOrDefault();
            RefreshBoreholeView();

            OnPropertyChanged(nameof(BoreholesCount));
            OnPropertyChanged(nameof(LogsCount));
            RaiseCommandStates();
        }

        private async Task LoadCloudProjectsAsync()
        {
            var projects = await _cloud.GetProjectsAsync();

            CloudProjects.Clear();
            foreach (var project in projects)
            {
                CloudProjects.Add(project);
            }

            var previousId = SelectedCloudProject?.Id;
            SelectedCloudProject = previousId is null
                ? CloudProjects.FirstOrDefault()
                : CloudProjects.FirstOrDefault(project => project.Id == previousId) ?? CloudProjects.FirstOrDefault();

            CloudStatus = CloudProjects.Count == 0
                ? "No project is stored on this account yet. Use Upload to create one."
                : $"{CloudProjects.Count} project(s) stored on this account.";
        }

        private void NotifyCloudState()
        {
            OnPropertyChanged(nameof(IsCloudSignedIn));
            OnPropertyChanged(nameof(IsCloudSignedOut));
            OnPropertyChanged(nameof(CloudAccountLabel));
            OnPropertyChanged(nameof(CanSignIn));
            OnPropertyChanged(nameof(CanUseCloud));
        }

        private void SetCloudBusy(bool value)
        {
            if (_cloudBusy == value) return;
            _cloudBusy = value;
            OnPropertyChanged(nameof(IsCloudBusy));
            OnPropertyChanged(nameof(CanSignIn));
            OnPropertyChanged(nameof(CanUseCloud));
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

        private void SetActiveView(WorkspaceView view)
        {
            if (_activeView == view) return;

            _activeView = view;
            OnPropertyChanged(nameof(ActiveView));
            OnPropertyChanged(nameof(IsBoreholeViewActive));
            OnPropertyChanged(nameof(IsMapViewActive));
            OnPropertyChanged(nameof(IsSectionViewActive));
            OnPropertyChanged(nameof(ActiveViewTitle));
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
            RefreshMapView();
            RefreshSectionView();

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

        private void RefreshMapView()
        {
            MapMarkers.Clear();

            if (Boreholes.Count == 0)
            {
                MapRangeLabel = string.Empty;
                MapMessage = "No boreholes to display. Create a borehole to populate the map.";
                return;
            }

            var dataMinX = Boreholes.Min(borehole => borehole.X);
            var dataMaxX = Boreholes.Max(borehole => borehole.X);
            var dataMinY = Boreholes.Min(borehole => borehole.Y);
            var dataMaxY = Boreholes.Max(borehole => borehole.Y);

            var minX = dataMinX;
            var maxX = dataMaxX;
            var minY = dataMinY;
            var maxY = dataMaxY;

            if (maxX - minX < 1e-9)
            {
                minX -= 5;
                maxX += 5;
            }

            if (maxY - minY < 1e-9)
            {
                minY -= 5;
                maxY += 5;
            }

            var plotWidth = MapCanvasWidth - MapPlotLeft - MapPlotRight;
            var plotHeight = MapCanvasHeight - MapPlotTop - MapPlotBottom;
            var scale = Math.Min(plotWidth / (maxX - minX), plotHeight / (maxY - minY));
            var offsetX = MapPlotLeft + (plotWidth - (maxX - minX) * scale) / 2;
            var offsetY = MapPlotTop + (plotHeight - (maxY - minY) * scale) / 2;

            foreach (var borehole in Boreholes)
            {
                var size = borehole == SelectedBorehole ? 14 : 10;
                var centerX = offsetX + (borehole.X - minX) * scale;
                var centerY = offsetY + (maxY - borehole.Y) * scale;
                var toolTip = string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}\nX {1:0.##} · Y {2:0.##}\nElevation {3:0.##} m · Depth {4:0.##} m",
                    borehole.Name,
                    borehole.X,
                    borehole.Y,
                    borehole.Elevation,
                    borehole.TotalDepth);

                MapMarkers.Add(new MapMarker(
                    centerX - size / 2,
                    centerY - size / 2,
                    size,
                    borehole.Name,
                    borehole == SelectedBorehole,
                    toolTip));
            }

            MapRangeLabel = string.Format(
                CultureInfo.CurrentCulture,
                "X {0:0.##} – {1:0.##} m · Y {2:0.##} – {3:0.##} m",
                dataMinX,
                dataMaxX,
                dataMinY,
                dataMaxY);
            MapMessage = Boreholes.Count == 1 ? "Add more boreholes to compare their positions." : string.Empty;
        }

        private void RefreshSectionView()
        {
            SectionColumns.Clear();
            SectionLabels.Clear();
            SectionDistanceTicks.Clear();
            SectionElevationTicks.Clear();
            SectionSurfacePoints = new PointCollection();

            var boreholes = Boreholes.Where(borehole => borehole.TotalDepth > 0).ToList();
            if (boreholes.Count < 2)
            {
                SectionMessage = "Add at least two boreholes with a total depth to build a cross section.";
                return;
            }

            var west = boreholes.OrderBy(borehole => borehole.X).ThenBy(borehole => borehole.Y).First();
            var east = boreholes.OrderBy(borehole => borehole.X).ThenBy(borehole => borehole.Y).Last();
            var deltaX = east.X - west.X;
            var deltaY = east.Y - west.Y;
            var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            var directionX = length > 1e-9 ? deltaX / length : 1;
            var directionY = length > 1e-9 ? deltaY / length : 0;

            var ordered = boreholes
                .Select(borehole => new
                {
                    Borehole = borehole,
                    Distance = Math.Max(0, (borehole.X - west.X) * directionX + (borehole.Y - west.Y) * directionY)
                })
                .OrderBy(item => item.Distance)
                .ToList();

            var maxDistance = ordered.Max(item => item.Distance);
            var plotWidth = SectionCanvasWidth - SectionPlotLeft - SectionPlotRight;
            var plotHeight = SectionCanvasHeight - SectionPlotTop - SectionPlotBottom;
            var topElevation = ordered.Max(item => item.Borehole.Elevation);
            var bottomElevation = ordered.Min(item => item.Borehole.Elevation - item.Borehole.TotalDepth);
            if (topElevation - bottomElevation < 1e-9)
            {
                topElevation = bottomElevation + 1;
            }

            var verticalScale = plotHeight / (topElevation - bottomElevation);
            var horizontalScale = maxDistance > 1e-9 ? plotWidth / maxDistance : 0;
            var points = new PointCollection();

            for (var index = 0; index < ordered.Count; index++)
            {
                var item = ordered[index];
                var borehole = item.Borehole;
                var x = maxDistance > 1e-9
                    ? SectionPlotLeft + item.Distance * horizontalScale
                    : SectionPlotLeft + plotWidth * index / Math.Max(1, ordered.Count - 1);
                var groundY = SectionPlotTop + (topElevation - borehole.Elevation) * verticalScale;
                var columnHeight = borehole.TotalDepth * verticalScale;

                points.Add(new Point(x, groundY));

                var layers = new List<SectionLayer>();
                var log = borehole.Logs.FirstOrDefault(candidate => candidate.Type == LogType.Lithology)
                    ?? borehole.Logs.FirstOrDefault();
                if (log is not null)
                {
                    foreach (var point in log.Points.OrderBy(candidate => candidate.Depth))
                    {
                        var start = Math.Clamp(point.Depth, 0, borehole.TotalDepth);
                        var end = point.EndDepth > point.Depth ? point.EndDepth : point.Depth + Math.Max(point.Value, 0);
                        end = Math.Clamp(end, start, borehole.TotalDepth);
                        if (end - start <= 1e-9) continue;

                        var label = string.IsNullOrWhiteSpace(point.Label) ? "(unnamed)" : point.Label.Trim();
                        layers.Add(new SectionLayer(
                            start * verticalScale,
                            (end - start) * verticalScale,
                            LithologyPalette.ColorFor(label),
                            string.Format(CultureInfo.CurrentCulture, "{0}: {1:0.##}–{2:0.##} m", label, start, end)));
                    }
                }

                SectionColumns.Add(new SectionColumn(
                    x - SectionColumnWidth / 2,
                    SectionColumnWidth,
                    groundY,
                    columnHeight,
                    borehole == SelectedBorehole,
                    layers));
                SectionLabels.Add(new SectionLabel(
                    Math.Clamp(x - 40, 0, SectionCanvasWidth - 80),
                    Math.Max(2, groundY - 22),
                    borehole.Name));
            }

            SectionSurfacePoints = points;

            if (maxDistance > 1e-9)
            {
                var distanceStep = NiceStep(maxDistance);
                for (var distance = 0.0; distance <= maxDistance + 1e-9; distance += distanceStep)
                {
                    SectionDistanceTicks.Add(new SectionTick(
                        SectionPlotLeft + distance * horizontalScale - 35,
                        SectionPlotTop + plotHeight + 6,
                        distance.ToString("0.##", CultureInfo.CurrentCulture)));
                }
            }

            var elevationStep = NiceStep(topElevation - bottomElevation);
            var firstElevation = Math.Ceiling(bottomElevation / elevationStep) * elevationStep;
            for (var elevation = firstElevation; elevation <= topElevation + 1e-9; elevation += elevationStep)
            {
                var y = SectionPlotTop + (topElevation - elevation) * verticalScale;
                SectionElevationTicks.Add(new SectionTick(
                    SectionPlotLeft - 62,
                    y - 8,
                    elevation.ToString("0.##", CultureInfo.CurrentCulture)));
            }

            SectionMessage = maxDistance > 1e-9
                ? string.Empty
                : "All boreholes share the same coordinates: columns are spaced evenly for readability.";
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

        private void ExportCsvFromDialog()
        {
            var fileName = string.IsNullOrWhiteSpace(ProjectName) ? "MICGeoVision" : ProjectName.Trim();
            fileName = new string(fileName
                .Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)
                .ToArray());

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                DefaultExt = ".csv",
                FileName = $"{fileName}.csv"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(dialog.FileName, BuildBoreholeCsv(), new UTF8Encoding(true));
                StatusMessage = $"Exported {Boreholes.Count} borehole(s) to {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception exception)
            {
                StatusMessage = $"CSV export failed: {exception.Message}";
            }
        }

        private string BuildBoreholeCsv()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Borehole;X;Y;Elevation;TotalDepth;Log;FromDepth;ToDepth;Lithology");

            foreach (var borehole in Boreholes)
            {
                var prefix = new[]
                {
                    borehole.Name,
                    CsvNumber(borehole.X),
                    CsvNumber(borehole.Y),
                    CsvNumber(borehole.Elevation),
                    CsvNumber(borehole.TotalDepth)
                };

                if (borehole.Logs.Count == 0)
                {
                    builder.AppendLine(string.Join(";", prefix.Select(CsvField)));
                    continue;
                }

                foreach (var log in borehole.Logs)
                {
                    if (log.Points.Count == 0)
                    {
                        builder.AppendLine(string.Join(";", prefix.Concat([log.Name, string.Empty, string.Empty, string.Empty]).Select(CsvField)));
                        continue;
                    }

                    foreach (var point in log.Points)
                    {
                        var end = point.EndDepth > point.Depth ? point.EndDepth : point.Depth + point.Value;
                        var fields = prefix.Concat([log.Name, CsvNumber(point.Depth), CsvNumber(end), point.Label]);
                        builder.AppendLine(string.Join(";", fields.Select(CsvField)));
                    }
                }
            }

            return builder.ToString();
        }

        private static string CsvNumber(double value) => value.ToString("0.###", CultureInfo.CurrentCulture);

        private static string CsvField(string? value)
        {
            var text = value ?? string.Empty;
            if (!text.Contains(';') && !text.Contains('"') && !text.Contains('\n') && !text.Contains('\r'))
            {
                return text;
            }

            return "\"" + text.Replace("\"", "\"\"") + "\"";
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
