using System.Text;
using System.Text.Json;
using PhaseDrop.Core.IO;
using PhaseDrop.Core.Models;
using PhaseDrop.Core.Queue;

namespace PhaseDrop.Engines.Documents;

public sealed partial class DocumentConversionEngine : IConversionEngine
{
    private static readonly HashSet<string> SupportedOperationIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "convert:csv", "convert:json", "convert:html", "convert:txt",
        "tool:table_to_json", "tool:md_to_html"
    };

    public bool CanHandle(FileOperation operation)
    {
        return SupportedOperationIds.Contains(operation.Id);
    }

    public async Task<JobResult> ExecuteAsync(ConversionJob job, IProgress<JobProgress> progress, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var outputFiles = new List<string>();
            int total = job.SourcePaths.Count;

            for (int i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string sourcePath = job.SourcePaths[i];
                string fileName = Path.GetFileName(sourcePath);
                progress.Report(new JobProgress($"Processing {fileName}", (double)i / total, i, total, fileName));

                string targetExt = GetTargetExtension(job.Operation, sourcePath);
                string targetDir = job.OutputDirectory ?? Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory;
                string outputPath = CollisionSafeWriter.GetSafeOutputPath(targetDir, Path.GetFileNameWithoutExtension(sourcePath), targetExt);
                string stagingPath = CollisionSafeWriter.CreateStagingPath(targetExt);

                try
                {
                    ProcessDocument(sourcePath, stagingPath, job.Operation);
                    CollisionSafeWriter.CommitStagingFile(stagingPath, outputPath);
                    outputFiles.Add(outputPath);
                }
                catch
                {
                    CollisionSafeWriter.CleanStagingFile(stagingPath);
                    throw;
                }

                progress.Report(new JobProgress($"Completed {fileName}", (double)(i + 1) / total, i + 1, total, fileName));
            }

            return new JobResult(true, outputFiles);
        }, cancellationToken);
    }

    private static string GetTargetExtension(FileOperation operation, string sourcePath)
    {
        return operation.Id switch
        {
            "convert:json" or "tool:table_to_json" => ".json",
            "convert:html" or "tool:md_to_html" => ".html",
            "convert:csv" => ".csv",
            "convert:txt" => ".txt",
            _ => operation.TargetExtension
        };
    }

    private static void ProcessDocument(string sourcePath, string stagingPath, FileOperation operation)
    {
        string content = File.ReadAllText(sourcePath, Encoding.UTF8);

        if (operation.Id is "convert:json" or "tool:table_to_json")
        {
            char delimiter = sourcePath.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : ',';
            var rows = ParseCsv(content, delimiter);

            if (rows.Count == 0)
            {
                File.WriteAllText(stagingPath, "[]", Encoding.UTF8);
                return;
            }

            var headers = rows[0];
            var records = new List<Dictionary<string, string>>();

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                // Skip empty trailing rows
                if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0]))
                    continue;

                var dict = new Dictionary<string, string>();
                for (int c = 0; c < headers.Count; c++)
                {
                    dict[headers[c]] = c < row.Count ? row[c] : "";
                }
                records.Add(dict);
            }

            string json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(stagingPath, json, Encoding.UTF8);
        }
        else if (operation.Id is "convert:html" or "tool:md_to_html")
        {
            string htmlBody;
            if (sourcePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) || sourcePath.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase))
            {
                char delimiter = sourcePath.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : ',';
                htmlBody = ConvertCsvToHtmlTable(content, delimiter);
            }
            else
            {
                htmlBody = ConvertMarkdownToHtml(content);
            }

            string safeTitle = System.Net.WebUtility.HtmlEncode(Path.GetFileNameWithoutExtension(sourcePath));
            string fullHtml = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>{{safeTitle}}</title>
                <style>
                    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; padding: 2rem; max-width: 960px; margin: 0 auto; color: #24292f; background: #ffffff; }
                    table { border-collapse: collapse; width: 100%; margin: 1.5rem 0; font-size: 0.95rem; }
                    th, td { border: 1px solid #d0d7de; padding: 8px 12px; text-align: left; }
                    th { background: #f6f8fa; font-weight: 600; }
                    tr:nth-child(even) { background: #f6f8fa; }
                    code { background: #eff1f3; padding: 0.2em 0.4em; border-radius: 4px; font-family: Consolas, monospace; font-size: 85%; }
                    pre { background: #f6f8fa; padding: 1rem; border-radius: 6px; overflow-x: auto; }
                    h1, h2, h3 { border-bottom: 1px solid #d0d7de; padding-bottom: 0.3em; margin-top: 1.5rem; }
                    blockquote { border-left: 4px solid #d0d7de; padding-left: 1rem; color: #57606a; margin: 1rem 0; }
                </style>
            </head>
            <body>
            {{htmlBody}}
            </body>
            </html>
            """;

            File.WriteAllText(stagingPath, fullHtml, Encoding.UTF8);
        }
        else if (operation.Id == "convert:csv")
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    var headerList = new List<string>();
                    var headerSet = new HashSet<string>(StringComparer.Ordinal);

                    // Preserve deterministic discovery order of headers across all objects
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in item.EnumerateObject())
                            {
                                if (headerSet.Add(prop.Name))
                                {
                                    headerList.Add(prop.Name);
                                }
                            }
                        }
                    }

                    sb.AppendLine(string.Join(",", headerList.Select(EscapeCsv)));

                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            var row = headerList.Select(h => item.TryGetProperty(h, out var v) ? EscapeCsv(v.ToString()) : "");
                            sb.AppendLine(string.Join(",", row));
                        }
                    }

                    File.WriteAllText(stagingPath, sb.ToString(), Encoding.UTF8);
                    return;
                }
            }
            catch
            {
                // Passthrough on non-JSON array
            }

            File.WriteAllText(stagingPath, content, Encoding.UTF8);
        }
        else
        {
            File.WriteAllText(stagingPath, content, Encoding.UTF8);
        }
    }

    /// <summary>
    /// RFC 4180 compliant CSV/TSV parser state machine handling quoted delimiters,
    /// embedded newlines, escaped quotes, Unicode BOM, empty cells, and unequal lengths.
    /// </summary>
    public static List<List<string>> ParseCsv(string text, char delimiter = ',')
    {
        var records = new List<List<string>>();
        if (string.IsNullOrEmpty(text))
            return records;

        // Strip UTF-8 Byte Order Mark (BOM) if present
        if (text[0] == '\uFEFF')
            text = text[1..];

        var currentRecord = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        // Escaped quote: "" -> "
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        // End of quoted field
                        inQuotes = false;
                    }
                }
                else
                {
                    currentField.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == delimiter)
                {
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                }
                else if (c == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                    records.Add(currentRecord);
                    currentRecord = new List<string>();
                }
                else if (c == '\n')
                {
                    currentRecord.Add(currentField.ToString());
                    currentField.Clear();
                    records.Add(currentRecord);
                    currentRecord = new List<string>();
                }
                else
                {
                    currentField.Append(c);
                }
            }
        }

        // Add final field and record if content remains
        if (currentField.Length > 0 || currentRecord.Count > 0)
        {
            currentRecord.Add(currentField.ToString());
            records.Add(currentRecord);
        }

        return records;
    }

    public static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        if (value.Contains(',') || value.Contains('\t') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static string ConvertCsvToHtmlTable(string csvContent, char delimiter)
    {
        var rows = ParseCsv(csvContent, delimiter);
        if (rows.Count == 0) return "<p>Empty dataset.</p>";

        var sb = new StringBuilder();
        sb.AppendLine("<table>");

        var headers = rows[0];
        sb.AppendLine("<thead><tr>");
        foreach (var h in headers)
            sb.AppendLine($"<th>{System.Net.WebUtility.HtmlEncode(h)}</th>");
        sb.AppendLine("</tr></thead>");

        sb.AppendLine("<tbody>");
        for (int r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0])) continue;

            sb.AppendLine("<tr>");
            for (int c = 0; c < headers.Count; c++)
            {
                string cell = c < row.Count ? row[c] : "";
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(cell)}</td>");
            }
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table>");

        return sb.ToString();
    }

    private static string ConvertMarkdownToHtml(string markdown)
    {
        var sb = new StringBuilder();
        var lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        bool inList = false;

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();

            if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                if (!inList)
                {
                    sb.AppendLine("<ul>");
                    inList = true;
                }
                sb.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(line[2..])}</li>");
                continue;
            }

            if (inList)
            {
                sb.AppendLine("</ul>");
                inList = false;
            }

            if (line.StartsWith("### "))
                sb.AppendLine($"<h3>{System.Net.WebUtility.HtmlEncode(line[4..])}</h3>");
            else if (line.StartsWith("## "))
                sb.AppendLine($"<h2>{System.Net.WebUtility.HtmlEncode(line[3..])}</h2>");
            else if (line.StartsWith("# "))
                sb.AppendLine($"<h1>{System.Net.WebUtility.HtmlEncode(line[2..])}</h1>");
            else if (line.StartsWith("> "))
                sb.AppendLine($"<blockquote>{System.Net.WebUtility.HtmlEncode(line[2..])}</blockquote>");
            else if (string.IsNullOrWhiteSpace(line))
                sb.AppendLine("<br/>");
            else
                sb.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(line)}</p>");
        }

        if (inList)
        {
            sb.AppendLine("</ul>");
        }

        return sb.ToString();
    }
}
