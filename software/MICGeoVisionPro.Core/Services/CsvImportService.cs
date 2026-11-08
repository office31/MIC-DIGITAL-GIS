using System.Globalization;
using System.Text;
using MICGeoVisionPro.Core.Models;

namespace MICGeoVisionPro.Core.Services;

/// <summary>Reads CSV records and creates typed project tables.</summary>
public class CsvImportService
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly char[] CandidateDelimiters = [',', ';', '\t', '|'];

    public DataTable ImportTable(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}", filePath);
        }

        var text = ReadText(filePath);
        var dialect = DetectDialect(text);
        var records = ParseRecords(text, dialect.Delimiter);
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
                Type = InferType(values, dialect.PreferFrench),
                IsNullable = sourceRows.Any(row => string.IsNullOrWhiteSpace(row[columnIndex]))
            });
        }

        foreach (var sourceRow in sourceRows)
        {
            var row = new Dictionary<string, object?>();
            foreach (var column in table.Columns)
            {
                var value = sourceRow[column.Ordinal].Trim();
                row[column.Name] = string.IsNullOrEmpty(value) ? null : ConvertValue(value, column.Type, dialect.PreferFrench);
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

    private static string ReadText(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }

            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }
        }

        var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            // Excel "CSV (séparateur point-virgule)" often writes ANSI (Windows-1252) files.
            return Encoding.Latin1.GetString(bytes, offset, bytes.Length - offset);
        }
    }

    private static CsvDialect DetectDialect(string text)
    {
        Span<int> counts = stackalloc int[CandidateDelimiters.Length];
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];

            if (character == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (inQuotes) continue;
            if (character == '\r' || character == '\n') break;

            var index = Array.IndexOf(CandidateDelimiters, character);
            if (index >= 0) counts[index]++;
        }

        var bestIndex = 0;
        for (var i = 1; i < counts.Length; i++)
        {
            if (counts[i] > counts[bestIndex]) bestIndex = i;
        }

        var delimiter = counts[bestIndex] > 0 ? CandidateDelimiters[bestIndex] : ',';
        return new CsvDialect(delimiter, delimiter != ',');
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

    private static DataColumnType InferType(IEnumerable<string> sourceValues, bool preferFrench)
    {
        var values = sourceValues
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToList();
        if (values.Count == 0) return DataColumnType.String;

        if (values.All(value => bool.TryParse(value, out _))) return DataColumnType.Boolean;
        if (values.All(value => TryParseInteger(value, preferFrench, out _))) return DataColumnType.Integer;
        if (values.All(value => TryParseDecimal(value, preferFrench, out _))) return DataColumnType.Decimal;
        if (values.All(value => TryParseDouble(value, preferFrench, out var number) && double.IsFinite(number)))
            return DataColumnType.Double;
        if (values.All(value => TryParseDateTime(value, preferFrench, out _)))
            return DataColumnType.DateTime;

        return DataColumnType.String;
    }

    private static object ConvertValue(string value, DataColumnType type, bool preferFrench) => type switch
    {
        DataColumnType.Boolean => bool.Parse(value),
        DataColumnType.Integer => TryParseInteger(value, preferFrench, out var integer)
            ? integer
            : throw new FormatException($"Unable to parse '{value}' as an integer."),
        DataColumnType.Decimal => TryParseDecimal(value, preferFrench, out var decimalValue)
            ? decimalValue
            : throw new FormatException($"Unable to parse '{value}' as a decimal."),
        DataColumnType.Double => TryParseDouble(value, preferFrench, out var doubleValue)
            ? doubleValue
            : throw new FormatException($"Unable to parse '{value}' as a number."),
        DataColumnType.DateTime => ParseDateTime(value, preferFrench),
        _ => value
    };

    private static bool TryParseInteger(string value, bool preferFrench, out long result)
    {
        var text = value.Trim();
        if (preferFrench)
        {
            return long.TryParse(text, NumberStyles.Integer | NumberStyles.AllowThousands, FrenchCulture, out result);
        }

        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseDecimal(string value, bool preferFrench, out decimal result)
    {
        var text = value.Trim();

        if (preferFrench
            && decimal.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, FrenchCulture, out result))
        {
            return true;
        }

        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        if (decimal.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, FrenchCulture, out result))
        {
            return true;
        }

        var normalized = NormalizeNumber(text);
        return normalized is not null
               && decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseDouble(string value, bool preferFrench, out double result)
    {
        var text = value.Trim();

        if (preferFrench
            && double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, FrenchCulture, out result))
        {
            return true;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, FrenchCulture, out result))
        {
            return true;
        }

        var normalized = NormalizeNumber(text);
        return normalized is not null
               && double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static string? NormalizeNumber(string text)
    {
        var compact = text
            .Replace(" ", string.Empty)
            .Replace("\u00A0", string.Empty)
            .Replace("\u202F", string.Empty);
        if (compact.Length == 0) return null;

        var lastComma = compact.LastIndexOf(',');
        var lastDot = compact.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            compact = lastComma > lastDot
                ? compact.Replace(".", string.Empty).Replace(',', '.')
                : compact.Replace(",", string.Empty);
        }
        else if (lastComma >= 0)
        {
            compact = compact.Replace(',', '.');
        }

        return compact;
    }

    private static bool TryParseDateTime(string value, bool preferFrench, out DateTime result)
    {
        if (preferFrench
            && DateTime.TryParse(value, FrenchCulture, DateTimeStyles.RoundtripKind, out result))
        {
            return true;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result);
    }

    private static DateTime ParseDateTime(string value, bool preferFrench)
    {
        if (TryParseDateTime(value, preferFrench, out var result)) return result;
        throw new FormatException($"Unable to parse '{value}' as a date.");
    }

    private static List<List<string>> ParseRecords(string text, char delimiter)
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
                if (character == delimiter)
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
            else if (character == delimiter)
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

    private readonly record struct CsvDialect(char Delimiter, bool PreferFrench);
}
