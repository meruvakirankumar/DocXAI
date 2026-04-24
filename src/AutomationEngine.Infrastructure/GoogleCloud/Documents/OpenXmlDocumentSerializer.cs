using System.Text;
using AutomationEngine.Domain.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AutomationEngine.Infrastructure.GoogleCloud.Documents;

public sealed class OpenXmlDocumentSerializer : IDocumentSerializer
{
    /// <summary>
    /// Extracts all paragraph text from a .docx byte array and returns it as plain text.
    /// </summary>
    public string ExtractTextFromDocx(byte[] docxBytes)
    {
        using var ms = new MemoryStream(docxBytes);
        using var doc = WordprocessingDocument.Open(ms, isEditable: false);

        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var para in body.Descendants<Paragraph>())
        {
            var line = para.InnerText;
            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts Markdown-formatted plain text into a .docx byte array.
    /// Headings (# / ## / ###) are mapped to Word heading styles.
    /// Tables (|...|...|) are mapped to Word tables.
    /// </summary>
    public byte[] SerializeToDocx(string content)
    {
        using var ms = new MemoryStream();

        using (var document = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            var lines = content.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var trimmed = line.TrimStart();

                if (line.StartsWith("### ", StringComparison.Ordinal))
                {
                    AppendHeading(body, line[4..], "Heading3");
                }
                else if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    AppendHeading(body, line[3..], "Heading2");
                }
                else if (line.StartsWith("# ", StringComparison.Ordinal))
                {
                    AppendHeading(body, line[2..], "Heading1");
                }
                else if (trimmed.StartsWith("|") && trimmed.EndsWith("|"))
                {
                    var tableLines = new List<string>();
                    while (i < lines.Count)
                    {
                        var tLine = lines[i].Trim();
                        if (tLine.StartsWith("|") && tLine.EndsWith("|"))
                        {
                            tableLines.Add(tLine);
                            i++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    i--; // step back so the outer loop processes the non-table line

                    AppendTable(body, tableLines);
                }
                else
                {
                    AppendParagraph(body, line);
                }
            }

            mainPart.Document.Save();
        }

        return ms.ToArray();
    }

    private static void AppendHeading(Body body, string text, string styleId)
    {
        var para = body.AppendChild(new Paragraph());
        para.AppendChild(new ParagraphProperties())
            .AppendChild(new ParagraphStyleId { Val = styleId });
        para.AppendChild(new Run()).AppendChild(new Text(text));
    }

    private static void AppendParagraph(Body body, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            body.AppendChild(new Paragraph());
            return;
        }

        var para = body.AppendChild(new Paragraph());
        var run = para.AppendChild(new Run());
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static void AppendTable(Body body, List<string> tableLines)
    {
        var table = new Table();

        TableProperties tblProp = new TableProperties(
            new TableBorders(
                new TopBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new BottomBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new LeftBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new RightBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new InsideHorizontalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new InsideVerticalBorder() { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 }
            ),
            new TableCellMarginDefault(
                new TopMargin() { Width = "50", Type = TableWidthUnitValues.Dxa },
                new BottomMargin() { Width = "50", Type = TableWidthUnitValues.Dxa },
                new LeftMargin() { Width = "100", Type = TableWidthUnitValues.Dxa },
                new RightMargin() { Width = "100", Type = TableWidthUnitValues.Dxa }
            )
        );
        table.AppendChild(tblProp);

        foreach (var line in tableLines)
        {
            // Skip markdown table separators like |---|---| or |:---:|
            if (line.Replace("-", "").Replace("|", "").Replace(" ", "").Replace(":", "").Length == 0)
                continue;

            var row = new TableRow();
            // remove start and end pipes, then split by pipe
            var cells = line.Substring(1, line.Length - 2).Split('|');

            foreach (var cellText in cells)
            {
                var cell = new TableCell();
                var para = new Paragraph(new Run(new Text(cellText.Trim()) { Space = SpaceProcessingModeValues.Preserve }));
                cell.Append(para);
                row.Append(cell);
            }
            table.Append(row);
        }

        body.AppendChild(table);
        body.AppendChild(new Paragraph()); // blank line after table
    }
}
