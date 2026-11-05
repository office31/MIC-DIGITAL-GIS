using MICGeoVisionPro.Core.Models;

namespace MICGeoVisionPro.Core.Services;

/// <summary>Compatibility wrapper. Use <see cref="CsvImportService.ImportTable"/> for new code.</summary>
[Obsolete("Use CsvImportService.ImportTable instead.")]
public class DataImportService
{
    private readonly CsvImportService _csvImportService = new();

    public DataTable ImportCsv(string filePath) => _csvImportService.ImportTable(filePath);
}
