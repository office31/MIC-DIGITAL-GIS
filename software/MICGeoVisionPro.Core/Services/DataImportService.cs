using System.IO;
using MICGeoVisionPro.Core.Models;

namespace MICGeoVisionPro.Core.Services;

/// <summary>
/// Imports CSV files into the application's table model using the shared CSV parser.
/// </summary>
public class DataImportService
{
    private readonly CsvImportService _csvImportService = new();

    public DataTable ImportCsv(string filePath)
    {
        var importedRows = _csvImportService.Import(filePath);
        var table = new DataTable
        {
            Name = Path.GetFileNameWithoutExtension(filePath)
        };

        if (importedRows.Count == 0)
        {
            return table;
        }

        foreach (var columnName in importedRows[0].Keys)
        {
            table.Columns.Add(new DataColumn
            {
                Name = columnName,
                Type = DataColumnType.String
            });
        }

        foreach (var importedRow in importedRows)
        {
            var row = new Dictionary<string, object?>();
            foreach (var column in table.Columns)
            {
                row[column.Name] = importedRow.TryGetValue(column.Name, out var value)
                    ? value
                    : string.Empty;
            }

            table.Rows.Add(row);
        }

        return table;
    }
}
