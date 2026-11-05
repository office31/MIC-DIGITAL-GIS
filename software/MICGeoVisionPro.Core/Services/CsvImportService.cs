using System.Globalization;
using System.Text;
using MICGeoVisionPro.Core.Models;

namespace MICGeoVisionPro.Core.Services;

/// <summary>Reads CSV records and creates typed project tables.</summary>
public class CsvImportService
{
    public DataTable ImportTable(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}", filePath);
        }

        var records = ParseRecords(File.ReadAllText(filePath));
        if (records.Count == 0)
        {
            throw new InvalidOperationException("CSV file must contain a header row.");
        }

        var headers = NormalizeHeaders(records[0]);
        var sourceRows = records.Skip(1)
            .Where(record => record.Any(value => !string.IsNullOrWhiteSpace(value)))
            .ToList();

        for (var i = 0; i < sourceRows.Count; i++)
        {
            if (sourceRows[i].Count != headers.Count)
            {
                throw new FormatException(
                    $"CSV record {i + 2} has {sourceRows[i].Count} fields; expected {headers.Count}.");
            }
        }

        var table = new DataTable
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            SourceFileName = Path.GetFileName(filePath)
        };

        for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
        {
            var values = sourceRows.Select(row => row[columnIndex]);
            table.Columns.Add(new DataColumn
            {
                Name = headers[columnIndex],
                Ordinal = columnIndex,
                Type = InferType(values),
                IsNullable = sourceRows.Any(row => string.IsNullOrWhiteSpace(row[columnIndex]))
            });
        }

        foreach (var sourceRow in sourceRows)
        {
            var row = new Dictionary<string, object?>();
            foreach (var column in table.Columns)
            {
                var value = sourceRow[column.Ordinal].Trim();
                row[column.Name] = string.IsNullOrEmpty(value) ? null : ConvertValue(value, column.Type);
            }

            table.Rows.Add(row);
        }

        return table;
    }

    /// <summary>
    /// Compatibility API for callers that need string-valued records. New code should use ImportTable.
    /// </summary>
    public List<Dictionary<string, string>> Import(string filePath)
    {
        var table = ImportTable(filePath);
        return table.Rows.Select(row => table.Columns.ToDictionary(
            column => column.Name,
            column => Convert.ToString(row[column.Name], CultureInfo.InvariantCulture) ?? string.Empty)).ToList();
    }

    private static List<string> NormalizeHeaders(IReadOnlyList<string> sourceHeaders)
    {
        if (sourceHeaders.Count == 0)
        {
            throw new InvalidOperationException("CSV file must contain at least one column.");
        }

        var headers = new List<string>(sourceHeaders.Count);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < sourceHeaders.Count; i++)
        {
            var name = sourceHeaders[i].Trim();
            if (i == 0) name = name.TrimStart('\uFEFF');
            if (string.IsNullOrWhiteSpace(name)) name = $"Column{i + 1}";

            var uniqueName = name;
            var suffix = 2;
            while (!usedNames.Add(uniqueName))
            {
                uniqueName = $"{name}_{suffix++}";
            }

            headers.Add(uniqueName);
        }

        return headers;
    }

    private static DataColumnType InferType(IEnumerable<string> sourceValues)
    {
        var values = sourceValues.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        if (values.Count == 0) return DataColumnType.String;

        if (values.All(value => bool.TryParse(value, out _))) return DataColumnType.Boolean;
        if (values.All(value => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
            return DataColumnType.Integer;
        if (values.All(value => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
            return DataColumnType.Decimal;
        if (values.All(value => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                                && double.IsFinite(number)))
            return DataColumnType.Double;
        if (values.All(value => DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out _)))
            return DataColumnType.DateTime;

        return DataColumnType.String;
    }

    private static object ConvertValue(string value, DataColumnType type) => type switch
    {
        DataColumnType.Boolean => bool.Parse(value),
        DataColumnType.Integer => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
        DataColumnType.Decimal => decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
        DataColumnType.Double => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
        DataColumnType.DateTime => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        _ => value
    };

    private static List<List<string>> ParseRecords(string text)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var afterQuote = false;

        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];

            if (inQuotes)
            {
                if (character == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                        afterQuote = true;
                    }
                }
                else if (character == '\r' || character == '\n')
                {
                    if (character == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    field.Append('\n');
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            if (afterQuote)
            {
                if (character == ',')
                {
                    AddField(record, field);
                    afterQuote = false;
                }
                else if (character == '\r' || character == '\n')
                {
                    AddField(record, field);
                    AddRecord(records, record);
                    record = new List<string>();
                    afterQuote = false;
                    if (character == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
                else if (!char.IsWhiteSpace(character))
                {
                    throw new FormatException($"Unexpected character after a quoted CSV field at character {i + 1}.");
                }

                continue;
            }

            if (character == '"')
            {
                if (field.Length != 0)
                    throw new FormatException($"Unexpected quote inside an unquoted CSV field at character {i + 1}.");
                inQuotes = true;
            }
            else if (character == ',')
            {
                AddField(record, field);
            }
            else if (character == '\r' || character == '\n')
            {
                AddField(record, field);
                AddRecord(records, record);
                record = new List<string>();
                if (character == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            }
            else
            {
                field.Append(character);
            }
        }

        if (inQuotes)
        {
            throw new FormatException("CSV file ends inside a quoted field.");
        }

        if (field.Length > 0 || record.Count > 0 || afterQuote)
        {
            AddField(record, field);
            AddRecord(records, record);
        }

        return records;
    }

    private static void AddField(List<string> record, StringBuilder field)
    {
        record.Add(field.ToString());
        field.Clear();
    }

    private static void AddRecord(List<List<string>> records, List<string> record)
    {
        records.Add(record);
    }
}
