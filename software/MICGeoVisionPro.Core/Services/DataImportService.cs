using MICGeoVisionPro.Core.Models;
using MICGeoVisionPro.Core.Services;

namespace MICGeoVisionPro.Core.Services;

/// <summary>
/// Legacy compatibility wrapper kept only for older callers.
/// New code should use CsvImportService.ImportTable.
/// </summary>
[Obsolete("Use CsvImportService.ImportTable instead.")]
public sealed class DataImportService
{
    private readonly CsvImportService _csvImportService = new();

    public DataTable ImportCsv(string filePath) => _csvImportService.ImportTable(filePath);
}
