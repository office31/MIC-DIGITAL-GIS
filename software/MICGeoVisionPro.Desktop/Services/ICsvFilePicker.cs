namespace MICGeoVisionPro.Desktop.Services
{
    /// <summary>Provides a UI-independent contract for selecting a CSV file.</summary>
    public interface ICsvFilePicker
    {
        string? PickCsvFile();
    }
}
