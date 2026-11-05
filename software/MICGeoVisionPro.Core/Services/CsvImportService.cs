using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MICGeoVisionPro.Core.Services
{
    public class CsvImportService
    {
        public List<Dictionary<string, string>> Import(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            var lines = File.ReadAllLines(filePath);
            if (lines.Length < 2)
            {
                throw new InvalidOperationException("CSV file must contain headers and at least one row.");
            }

            var headers = SplitCsvLine(lines[0]);
            var rows = new List<Dictionary<string, string>>();

            for (var i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                var values = SplitCsvLine(lines[i]);
                var row = new Dictionary<string, string>();

                for (var j = 0; j < headers.Length; j++)
                {
                    var key = headers[j].Trim();
                    var value = j < values.Length ? values[j].Trim() : string.Empty;
                    row[key] = value;
                }

                rows.Add(row);
            }

            return rows;
        }

        private static string[] SplitCsvLine(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return Array.Empty<string>();
            }

            var values = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var character = line[i];

                if (character == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (character == ',' && !inQuotes)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(character);
                }
            }

            values.Add(current.ToString());
            return values.ToArray();
        }
    }
}
