using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MICGeoVisionPro.Desktop.ViewModels;

public sealed class LithologyIntervalRow : INotifyPropertyChanged
{
    private double _startDepth;
    private double _endDepth;
    private string _lithology;

    public LithologyIntervalRow(double startDepth, double endDepth, string lithology)
    {
        _startDepth = startDepth;
        _endDepth = endDepth;
        _lithology = lithology;
    }

    public double StartDepth
    {
        get => _startDepth;
        set
        {
            if (_startDepth.Equals(value)) return;
            _startDepth = value;
            OnPropertyChanged();
        }
    }

    public double EndDepth
    {
        get => _endDepth;
        set
        {
            if (_endDepth.Equals(value)) return;
            _endDepth = value;
            OnPropertyChanged();
        }
    }

    public string Lithology
    {
        get => _lithology;
        set
        {
            var text = value ?? string.Empty;
            if (_lithology == text) return;
            _lithology = text;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
