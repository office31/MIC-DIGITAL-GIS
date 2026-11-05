using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using MICGeoVisionPro.Core.Models;
using MICGeoVisionPro.Core.Services;

namespace MICGeoVisionPro.Desktop.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ProjectRepository _repository;
    private readonly Project _project;

    public MainViewModel()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MICGeoVisionPro");

        Directory.CreateDirectory(dataDirectory);

        _repository = new ProjectRepository(Path.Combine(dataDirectory, "project.db"));
        _project = _repository.LoadOrCreateDefault();
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

    public int BoreholesCount => _project.Boreholes.Count;

    public int LogsCount => _project.Boreholes.Sum(b => b.Logs.Count);

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
