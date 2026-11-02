using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

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
            return line.Split(',');
        }
    }
}
