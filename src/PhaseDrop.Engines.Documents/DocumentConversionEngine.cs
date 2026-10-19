using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
            // Convert CSV / TSV to JSON array of objects
            char delimiter = sourcePath.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : ',';
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                File.WriteAllText(stagingPath, "[]", Encoding.UTF8);
                return;
            }

            var headers = ParseDelimitedLine(lines[0], delimiter);
            var records = new List<Dictionary<string, string>>();

            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseDelimitedLine(lines[i], delimiter);
                var dict = new Dictionary<string, string>();
                for (int c = 0; c < headers.Count; c++)
                {
                    dict[headers[c]] = c < values.Count ? values[c] : "";
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
                htmlBody = ConvertCsvToHtmlTable(content, sourcePath.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : ',');
            }
            else
            {
                // Basic Markdown to HTML converter
                htmlBody = ConvertMarkdownToHtml(content);
            }

            string title = Path.GetFileNameWithoutExtension(sourcePath);
            string fullHtml = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>{{title}}</title>
                <style>
                    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; line-height: 1.6; padding: 2rem; max-width: 900px; margin: 0 auto; color: #24292f; background: #ffffff; }
                    table { border-collapse: collapse; width: 100%; margin: 1.5rem 0; font-size: 0.95rem; }
                    th, td { border: 1px solid #d0d7de; padding: 8px 12px; text-align: left; }
                    th { background: #f6f8fa; font-weight: 600; }
                    tr:nth-child(even) { background: #f6f8fa; }
                    code { background: #eff1f3; padding: 0.2em 0.4em; border-radius: 4px; font-family: Consolas, monospace; }
                    pre { background: #f6f8fa; padding: 1rem; border-radius: 6px; overflow-x: auto; }
                    h1, h2, h3 { border-bottom: 1px solid #d0d7de; padding-bottom: 0.3em; }
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
            // If source is JSON array of objects, convert to CSV
            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    var headers = new HashSet<string>();
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in item.EnumerateObject())
                                headers.Add(prop.Name);
                        }
                    }

                    var headerList = headers.ToList();
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
                // Passthrough content
            }
            File.WriteAllText(stagingPath, content, Encoding.UTF8);
        }
        else
        {
            File.WriteAllText(stagingPath, content, Encoding.UTF8);
        }
    }

    private static List<string> ParseDelimitedLine(string line, char delimiter)
    {
        var values = new List<string>();
        bool inQuotes = false;
        var cur = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    cur.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                values.Add(cur.ToString().Trim());
                cur.Clear();
            }
            else
            {
                cur.Append(c);
            }
        }

        values.Add(cur.ToString().Trim());
        return values;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static string ConvertCsvToHtmlTable(string csvContent, char delimiter)
    {
        var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return "<p>Empty dataset.</p>";

        var sb = new StringBuilder();
        sb.AppendLine("<table>");

        var headers = ParseDelimitedLine(lines[0], delimiter);
        sb.AppendLine("<thead><tr>");
        foreach (var h in headers)
            sb.AppendLine($"<th>{System.Net.WebUtility.HtmlEncode(h)}</th>");
        sb.AppendLine("</tr></thead>");

        sb.AppendLine("<tbody>");
        for (int i = 1; i < lines.Length; i++)
        {
            var row = ParseDelimitedLine(lines[i], delimiter);
            sb.AppendLine("<tr>");
            foreach (var cell in row)
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(cell)}</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table>");

        return sb.ToString();
    }

    private static string ConvertMarkdownToHtml(string markdown)
    {
        var sb = new StringBuilder();
        var lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.StartsWith("### "))
                sb.AppendLine($"<h3>{System.Net.WebUtility.HtmlEncode(line[4..])}</h3>");
            else if (line.StartsWith("## "))
                sb.AppendLine($"<h2>{System.Net.WebUtility.HtmlEncode(line[3..])}</h2>");
            else if (line.StartsWith("# "))
                sb.AppendLine($"<h1>{System.Net.WebUtility.HtmlEncode(line[2..])}</h1>");
            else if (line.StartsWith("- ") || line.StartsWith("* "))
                sb.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(line[2..])}</li>");
            else if (string.IsNullOrWhiteSpace(line))
                sb.AppendLine("<br/>");
            else
                sb.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(line)}</p>");
        }

        return sb.ToString();
    }
}
