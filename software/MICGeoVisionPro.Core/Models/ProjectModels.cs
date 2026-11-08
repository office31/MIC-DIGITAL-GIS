using System.ComponentModel;

namespace MICGeoVisionPro.Core.Models;

public enum LogType
{
    Depth,
    Lithology,
    LineSymbol,
    Bar,
    Percentage,
    CrossSection,
    Raster,
    Function
}

public enum DataColumnType
{
    String,
    Integer,
    Decimal,
    Double,
    DateTime,
    Boolean
}

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Project";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Borehole> Boreholes { get; set; } = new();
    public List<DataTable> Tables { get; set; } = new();
}

public class Borehole : INotifyPropertyChanged
{
    private string _name = "Borehole";

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public double X { get; set; }
    public double Y { get; set; }
    public double Elevation { get; set; }
    public double TotalDepth { get; set; }
    public List<Log> Logs { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class Log
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Log";
    public LogType Type { get; set; }
    public List<LogPoint> Points { get; set; } = new();
}

public class LogPoint
{
    public double Depth { get; set; }
    public double EndDepth { get; set; }
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
}

public class LithologyInterval
{
    public double StartDepth { get; set; }
    public double EndDepth { get; set; }
    public string Lithology { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class DataTable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Table";
    public string? SourceFileName { get; set; }
    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;
    public List<DataColumn> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
}

public class DataColumn
{
    public string Name { get; set; } = string.Empty;
    public int Ordinal { get; set; }
    public DataColumnType Type { get; set; } = DataColumnType.String;
    public bool IsNullable { get; set; }
}

public class MapLayer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Layer";
    public string Source { get; set; } = "Import";
    public bool Visible { get; set; } = true;
}
