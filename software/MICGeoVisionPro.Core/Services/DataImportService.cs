namespace MICGeoVisionPro.Core.Services;

using MICGeoVisionPro.Core.Models;

public class DataImportService
{
    public DataTable ImportCsv(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2)
        {
            throw new InvalidOperationException("CSV file must contain headers and at least one row.");
        }

        var headers = SplitCsvLine(lines[0]);
        var table = new DataTable { Name = Path.GetFileNameWithoutExtension(filePath) };

        foreach (var header in headers)
        {
            table.Columns.Add(new DataColumn
            {
                Name = header.Trim(),
                Type = DataColumnType.String
            });
        }

        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var columns = SplitCsvLine(lines[i]);
            var row = new Dictionary<string, object?>();

            for (var j = 0; j < table.Columns.Count; j++)
            {
                var value = j < columns.Length ? columns[j].Trim() : string.Empty;
                row[table.Columns[j].Name] = value;
            }

            table.Rows.Add(row);
        }

        return table;
    }

    private static string[] SplitCsvLine(string line)
    {
        return line.Split(',');
    }
}
