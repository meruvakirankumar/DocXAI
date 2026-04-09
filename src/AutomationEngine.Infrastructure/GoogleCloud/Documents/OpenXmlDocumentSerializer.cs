using AutomationEngine.Domain.Interfaces;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AutomationEngine.Infrastructure.GoogleCloud.Documents;

public sealed class OpenXmlDocumentSerializer : IDocumentSerializer
{
    /// <summary>
    /// Converts Markdown-formatted plain text into a .docx byte array.
    /// Headings (# / ## / ###) are mapped to Word heading styles.
    /// </summary>
    public byte[] SerializeToDocx(string content)
    {
        using var ms = new MemoryStream();

        using (var document = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            foreach (var rawLine in content.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');

                if (line.StartsWith("### ", StringComparison.Ordinal))
                    AppendHeading(body, line[4..], "Heading3");
                else if (line.StartsWith("## ", StringComparison.Ordinal))
                    AppendHeading(body, line[3..], "Heading2");
                else if (line.StartsWith("# ", StringComparison.Ordinal))
                    AppendHeading(body, line[2..], "Heading1");
                else
                    AppendParagraph(body, line);
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
}
