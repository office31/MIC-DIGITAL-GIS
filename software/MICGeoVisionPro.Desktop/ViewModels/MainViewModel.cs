using System.ComponentModel;
using System.Runtime.CompilerServices;
using MICGeoVisionPro.Core.Models;
using MICGeoVisionPro.Core.Services;

namespace MICGeoVisionPro.Desktop.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ProjectRepository _repository;
    private Project _project;

    public MainViewModel()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MICGeoVisionPro");

        Directory.CreateDirectory(dataDirectory);

        _repository = new ProjectRepository(Path.Combine(dataDirectory, "project.db"));
        _project = _repository.LoadOrCreateDefault();

        ProjectName = _project.Name;
        BoreholesCount = _project.Boreholes.Count;
        LogsCount = _project.Boreholes.Sum(b => b.Logs.Count);
    }

    public string ProjectName
    {
        get => _project.Name;
        set
        {
            if (_project.Name == value) return;
            _project.Name = value;
            OnPropertyChanged();
        }
    }

    public int BoreholesCount
    {
        get => _project.Boreholes.Count;
        set
        {
            if (_project.Boreholes.Count == value) return;
            OnPropertyChanged();
        }
    }

    public int LogsCount
    {
        get => _project.Boreholes.Sum(b => b.Logs.Count);
        set => OnPropertyChanged();
    }

    public Project Project => _project;

    public void SaveCurrentProject()
    {
        _repository.SaveProject(_project);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
