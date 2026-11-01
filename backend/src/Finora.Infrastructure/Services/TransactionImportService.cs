using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Finora.Application.Features.Transactions;

namespace Finora.Infrastructure.Services;

public class TransactionImportService : ITransactionImportService
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public async Task<ParsedFileDto> ParseFileAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (ext == ".xlsx")
        {
            return await ParseExcelAsync(stream, fileName, cancellationToken);
        }

        if (ext is ".csv" or ".tsv" or ".txt")
        {
            return await ParseDelimitedAsync(stream, fileName, cancellationToken);
        }

        throw new NotSupportedException($"Unsupported file format: '{ext}'. Finora supports .xlsx, .csv, and .tsv files.");
    }

    private static async Task<ParsedFileDto> ParseExcelAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read, leaveOpen: false);

        // 1. Load Shared Strings if available
        var sharedStrings = new List<string>();
        var sstEntry = archive.GetEntry("xl/sharedStrings.xml") ?? archive.GetEntry("xl/SharedStrings.xml");
        if (sstEntry != null)
        {
            await using var sstStream = sstEntry.Open();
            var sstDoc = await XDocument.LoadAsync(sstStream, LoadOptions.None, cancellationToken);
            var siElements = sstDoc.Descendants().Where(e => e.Name.LocalName == "si");
            foreach (var si in siElements)
            {
                var tElements = si.Descendants().Where(e => e.Name.LocalName == "t");
                var text = string.Concat(tElements.Select(t => t.Value));
                sharedStrings.Add(text);
            }
        }

        // 2. Locate the first worksheet
        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")
            ?? archive.Entries.FirstOrDefault(e =>
                e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

        if (sheetEntry == null)
            throw new InvalidOperationException("No worksheet found in the provided Excel workbook.");

        await using var sheetStream = sheetEntry.Open();
        var sheetDoc = await XDocument.LoadAsync(sheetStream, LoadOptions.None, cancellationToken);

        var sheetData = sheetDoc.Descendants().FirstOrDefault(e => e.Name.LocalName == "sheetData");
        if (sheetData == null)
        {
            return new ParsedFileDto(fileName, 0, Array.Empty<string>(), Array.Empty<IReadOnlyDictionary<string, string>>(), Array.Empty<IReadOnlyDictionary<string, string>>());
        }

        var rawRows = new List<List<string>>();
        var maxColCount = 0;

        foreach (var rowEl in sheetData.Elements().Where(e => e.Name.LocalName == "row"))
        {
            var cellElements = rowEl.Elements().Where(e => e.Name.LocalName == "c").ToList();
            if (cellElements.Count == 0) continue;

            var cellsByIndex = new Dictionary<int, string>();
            var maxIndexInRow = -1;

            foreach (var c in cellElements)
            {
                var cellRef = c.Attribute("r")?.Value ?? string.Empty;
                var colIndex = CellReferenceToColumnIndex(cellRef);
                if (colIndex < 0) continue;

                var typeAttr = c.Attribute("t")?.Value;
                var rawVal = c.Elements().FirstOrDefault(e => e.Name.LocalName == "v")?.Value?.Trim() ?? string.Empty;

                string val;
                if (typeAttr == "s" && int.TryParse(rawVal, out var sstIdx) && sstIdx >= 0 && sstIdx < sharedStrings.Count)
                {
                    val = sharedStrings[sstIdx];
                }
                else if (typeAttr == "inlineStr")
                {
                    val = c.Descendants().FirstOrDefault(e => e.Name.LocalName == "t")?.Value ?? string.Empty;
                }
                else if (typeAttr == "b")
                {
                    val = rawVal == "1" ? "TRUE" : "FALSE";
                }
                else
                {
                    val = rawVal;
                }

                cellsByIndex[colIndex] = val;
                if (colIndex > maxIndexInRow) maxIndexInRow = colIndex;
            }

            if (maxIndexInRow < 0) continue;

            var rowCells = new List<string>();
            for (var i = 0; i <= maxIndexInRow; i++)
            {
                rowCells.Add(cellsByIndex.TryGetValue(i, out var v) ? v : string.Empty);
            }

            // Only add if at least one cell has content
            if (rowCells.Any(s => !string.IsNullOrWhiteSpace(s)))
            {
                rawRows.Add(rowCells);
                if (rowCells.Count > maxColCount) maxColCount = rowCells.Count;
            }
        }

        return BuildParsedResult(fileName, rawRows, maxColCount);
    }

    private static async Task<ParsedFileDto> ParseDelimitedAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
        {
            return new ParsedFileDto(fileName, 0, Array.Empty<string>(), Array.Empty<IReadOnlyDictionary<string, string>>(), Array.Empty<IReadOnlyDictionary<string, string>>());
        }

        // Auto-detect delimiter from first line: comma, semicolon, tab
        var firstLine = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var commaCount = firstLine.Count(c => c == ',');
        var semiCount = firstLine.Count(c => c == ';');
        var tabCount = firstLine.Count(c => c == '\t');

        var delimiter = ',';
        if (tabCount > commaCount && tabCount > semiCount) delimiter = '\t';
        else if (semiCount > commaCount) delimiter = ';';

        var rawRows = ParseCsvRecords(content, delimiter);
        var maxColCount = rawRows.Count > 0 ? rawRows.Max(r => r.Count) : 0;

        return BuildParsedResult(fileName, rawRows, maxColCount);
    }

    private static ParsedFileDto BuildParsedResult(string fileName, List<List<string>> rawRows, int maxColCount)
    {
        if (rawRows.Count == 0)
        {
            return new ParsedFileDto(fileName, 0, Array.Empty<string>(), Array.Empty<IReadOnlyDictionary<string, string>>(), Array.Empty<IReadOnlyDictionary<string, string>>());
        }

        // Row 0 is the headers
        var headerRow = rawRows[0];
        var headers = new List<string>();
        var seenNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < maxColCount; i++)
        {
            var rawHeader = i < headerRow.Count ? headerRow[i].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(rawHeader))
            {
                rawHeader = $"Column {i + 1}";
            }

            if (seenNames.TryGetValue(rawHeader, out var count))
            {
                seenNames[rawHeader] = count + 1;
                headers.Add($"{rawHeader} ({count + 1})");
            }
            else
            {
                seenNames[rawHeader] = 1;
                headers.Add(rawHeader);
            }
        }

        var allRows = new List<IReadOnlyDictionary<string, string>>();
        for (var r = 1; r < rawRows.Count; r++)
        {
            var row = rawRows[r];
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var c = 0; c < headers.Count; c++)
            {
                var val = c < row.Count ? row[c].Trim() : string.Empty;
                dict[headers[c]] = val;
            }

            // Exclude rows where every cell is completely empty
            if (dict.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
            {
                allRows.Add(dict);
            }
        }

        var preview = allRows.Take(10).ToList();
        return new ParsedFileDto(fileName, allRows.Count, headers, preview, allRows);
    }

    private static int CellReferenceToColumnIndex(string cellRef)
    {
        if (string.IsNullOrEmpty(cellRef)) return -1;
        var colLetters = new string(cellRef.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
        if (colLetters.Length == 0) return -1;

        var index = 0;
        foreach (var ch in colLetters)
        {
            if (ch < 'A' || ch > 'Z') return -1;
            index = (index * 26) + (ch - 'A' + 1);
        }
        return index - 1; // 0-based index
    }

    private static List<List<string>> ParseCsvRecords(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var currentCell = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        while (i < text.Length)
        {
            var ch = text[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    currentCell.Append('"');
                    i += 2;
                    continue;
                }
                inQuotes = !inQuotes;
                i++;
                continue;
            }

            if (!inQuotes && ch == delimiter)
            {
                currentRow.Add(currentCell.ToString().Trim());
                currentCell.Clear();
                i++;
                continue;
            }

            if (!inQuotes && (ch == '\r' || ch == '\n'))
            {
                currentRow.Add(currentCell.ToString().Trim());
                currentCell.Clear();

                if (currentRow.Any(s => !string.IsNullOrWhiteSpace(s)))
                {
                    rows.Add(currentRow);
                }
                currentRow = new List<string>();

                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i += 2;
                }
                else
                {
                    i++;
                }
                continue;
            }

            currentCell.Append(ch);
            i++;
        }

        // Flush last cell/row
        if (currentCell.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentCell.ToString().Trim());
            if (currentRow.Any(s => !string.IsNullOrWhiteSpace(s)))
            {
                rows.Add(currentRow);
            }
        }

        return rows;
    }
}

