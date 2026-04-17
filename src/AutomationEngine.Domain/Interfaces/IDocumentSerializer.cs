namespace AutomationEngine.Domain.Interfaces;

public interface IDocumentSerializer
{
    /// <summary>
    /// Serializes markdown/plain-text content into a .docx byte array.
    /// </summary>
    byte[] SerializeToDocx(string content);

    /// <summary>
    /// Extracts plain text from the body of a .docx byte array.
    /// </summary>
    string ExtractTextFromDocx(byte[] docxBytes);
}
